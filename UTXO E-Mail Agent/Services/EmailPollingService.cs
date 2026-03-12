using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Factory;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Services;

/// <summary>
/// Background service for polling emails
/// </summary>
public class EmailPollingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly int _pollingIntervalSeconds;
    private readonly string _connectionString;

    public EmailPollingService(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _pollingIntervalSeconds = int.Parse(_configuration["AppSettings:PollingIntervalSeconds"] ?? "60");
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Logger.LogAsync("[EmailPollingService] Starting background email polling service...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAgentsAsync(ServerRegistrationService.ServerId);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[EmailPollingService] Error in polling loop: {ex.Message}", additionalData: ex.StackTrace);
            }

            await Logger.LogAsync($"[EmailPollingService] Waiting {_pollingIntervalSeconds} seconds until next check...");
            await Task.Delay(_pollingIntervalSeconds * 1000, stoppingToken);
        }
    }

    private async Task ProcessAgentsAsync(int? serverid)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultdbContext>();

        // Update server lifesign
        await ServerRegistrationService.UpdateLifesignAsync(db);

        // Load all active agents
        var agents = await db.Agents
            .Include(a => a.Customer)
            .Include(a => a.Mcpservers)
            .Include(a => a.Skills)
            .Where(a => a.State == "active" && a.Emailprovidertype == "polling" && (a.ServerId==null || a.ServerId == serverid))
            .ToListAsync();

        await Logger.LogAsync($"[EmailPollingService] Found {agents.Count} active agent(s) for polling");

        foreach (var agent in agents)
        {
            // Wait 1 second between agents to avoid potential rate limiting
            await Task.Delay(1000);
            
            try
            {
                // Skip agent if it was polled less than 60 seconds ago (prevents duplicate processing on multiple servers)
                if (agent.Lastpoll.HasValue && (DateTime.UtcNow - agent.Lastpoll.Value).TotalSeconds < 60)
                {
                    await Logger.LogAsync($"[EmailPollingService] Skipping agent {agent.Id} - last polled {(int)(DateTime.UtcNow - agent.Lastpoll.Value).TotalSeconds}s ago", agent.Id);
                    continue;
                }

                // Mark agent as polled
                agent.Lastpoll = DateTime.UtcNow;
                await db.SaveChangesAsync();

                await Logger.LogAsync($"[EmailPollingService] Processing agent {agent.Id} ({agent.Agentname})", agent.Id, null, false);

                // Select email provider by type
                var provider = EmailProviderFactory.GetProvider(agent.Emailprovider, _configuration, db);

                if (provider == null)
                {
                    Logger.LogWarning($"[EmailPollingService] Unknown email provider: {agent.Emailprovider}", agent.Id);
                    continue;
                }

                var emails = await provider.GetEmailsAsync(agent);

                if (emails != null && emails.Length > 0)
                {
                    await Logger.LogAsync($"[EmailPollingService] Found {emails.Length} new email(s) for agent {agent.Id}", agent.Id);

                    foreach (var email in emails)
                    {
                        await Logger.LogAsync($"[EmailPollingService] Email from: {email.From}, Subject: {email.Subject}", agent.Id);
                        
                        // Wait 1 second before fetching email details
                        await Task.Delay(1000);
                        
                        var mail = await provider.GetMail(email, agent);

                        if (mail == null)
                        {
                            Logger.LogWarning($"[EmailPollingService] GetMail returned null for email '{email.Subject}' - skipping", agent.Id);
                            continue;
                        }

                        // Generate a consistent message ID if not provided
                        var messageId = mail.Id ?? Guid.NewGuid().ToString();
                        
                        // Check if this email is already being processed by another agent (prevent duplicates)
                        var existingConversation = await db.Conversations
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.Messageid == messageId && c.AgentId == agent.Id);
                        
                        if (existingConversation != null)
                        {
                            await Logger.LogAsync($"[EmailPollingService] Email '{mail.Subject}' (ID: {messageId}) already processed - skipping", agent.Id);
                            continue;
                        }
                        
                        // Immediately create a conversation entry to "claim" this email
                        var conversation1 = new Conversation
                        {
                            Subject = mail.Subject ?? "(No Subject)",
                            Text = mail.Text,
                            Htmltext = mail.Html,
                            AgentId = agent.Id,
                            Emailfrom = mail.From ?? "(Unknown)",
                            Messageid = messageId,
                            Emailreceived = DateTime.Now,
                        };
                        
                        try
                        {
                            await db.Conversations.AddAsync(conversation1);
                            await db.SaveChangesAsync();
                            await Logger.LogAsync($"[EmailPollingService] Claimed email '{mail.Subject}' (ID: {messageId})", agent.Id);
                        }
                        catch (DbUpdateException)
                        {
                            // Another agent claimed this email first (race condition)
                            await Logger.LogAsync($"[EmailPollingService] Email '{mail.Subject}' already claimed by another agent - skipping", agent.Id);
                            continue;
                        }

                        try
                        {
                            // Log email content as additional data
                            var emailData = $"From: {mail.From}\nSubject: {mail.Subject}\nDate: {mail.CreatedAt}\n\nContent:\n{mail.Text}";
                            await Logger.LogAsync($"[EmailPollingService] Processing email: {mail.Subject} (MessageId: {mail.Id})", agent.Id, emailData);

                            // Process mail with AI (pass existing conversation)
                            var processor = new ProcessMailsClass(db, _configuration);
                            var aiResponse = await processor.ProcessMailAsync(mail, agent, conversation1);

                            await Logger.LogAsync($"[EmailPollingService] AI Response generated", agent.Id, aiResponse.AiExplanation);

                            // Process based on Action field
                            var action = aiResponse.Action?.ToLowerInvariant() ?? "respond";
                            
                            switch (action)
                            {
                                case "redirect":
                                    // Forward the ORIGINAL email with all content to the specified recipients
                                    if (aiResponse.RedirectTo != null && aiResponse.RedirectTo.Length > 0)
                                    {
                                        await provider.RedirectEmail(mail, agent, aiResponse.RedirectTo, aiResponse.RedirectCc, aiResponse.RedirectMessage);
                                        await Logger.LogAsync($"[EmailPollingService] Email redirected to: {string.Join(", ", aiResponse.RedirectTo)}", agent.Id);
                                    }
                                    else
                                    {
                                        await Logger.LogAsync($"[EmailPollingService] Redirect action but no RedirectTo specified", agent.Id);
                                    }
                                    break;
                                    
                                case "delete":
                                    await Logger.LogAsync($"[EmailPollingService] Email marked as spam/deleted - no action taken", agent.Id);
                                    break;
                                    
                                case "ignore":
                                    await Logger.LogAsync($"[EmailPollingService] Action=ignore - AI handled the task independently", agent.Id);
                                    break;
                                    
                                case "respond":
                                default:
                                    // Original behavior: send reply if there's content
                                    if (!string.IsNullOrEmpty(aiResponse.EmailResponseText) || !string.IsNullOrEmpty(aiResponse.EmailResponseHtml))
                                    {
                                        if (!string.IsNullOrEmpty(mail.From) && aiResponse.AlreadySentTo.Contains(mail.From))
                                        {
                                            await Logger.LogAsync($"[EmailPollingService] Skipping reply to {mail.From} - already sent via send_email tool", agent.Id);
                                        }
                                        else
                                        {
                                            await provider.SendReplyResponseEmail(aiResponse, mail, agent, aiResponse.Conversation);
                                            await Logger.LogAsync($"[EmailPollingService] Reply sent to {mail.From}", agent.Id, aiResponse.EmailResponseText);
                                        }
                                    }
                                    else
                                    {
                                        await Logger.LogAsync($"[EmailPollingService] No reply sent (no response content)", agent.Id);
                                    }
                                    break;
                            }

                            // Cleanup: Delete entire working directory after sending
                            if (!string.IsNullOrEmpty(aiResponse.WorkingDirectory) && Directory.Exists(aiResponse.WorkingDirectory))
                            {
                                try
                                {
                                    Directory.Delete(aiResponse.WorkingDirectory, recursive: true);
                                    await Logger.LogAsync($"[Cleanup] Deleted working directory: {aiResponse.WorkingDirectory}", agent.Id);
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogWarning($"[Cleanup] Could not delete working directory {aiResponse.WorkingDirectory}: {ex.Message}", agent.Id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[EmailPollingService] Error processing email '{mail.Subject}': {ex.Message}", agent.Id, ex.StackTrace);

                            // Mark email as unread so it gets picked up again next time
                            await Logger.LogAsync($"[EmailPollingService] Marking email as unread for retry", agent.Id);
                            await provider.MarkAsUnreadAsync(email, agent);

                            // Remove the conversation record that was created before the error
                            var conversation = await db.Conversations
                                .Where(c => c.AgentId == agent.Id && c.Messageid == mail.Id)
                                .OrderByDescending(c => c.Id)
                                .FirstOrDefaultAsync();
                            if (conversation != null)
                            {
                                db.Conversations.Remove(conversation);
                                await db.SaveChangesAsync();
                                await Logger.LogAsync($"[EmailPollingService] Removed conversation {conversation.Id} due to processing error", agent.Id);
                            }
                        }
                    }
                }
                else
                {
                    await Logger.LogAsync($"[EmailPollingService] No new emails for agent {agent.Id}", agent.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[EmailPollingService] Error processing agent {agent.Id}: {ex.Message}", agent.Id, ex.StackTrace);
            }
        }
    }
}
