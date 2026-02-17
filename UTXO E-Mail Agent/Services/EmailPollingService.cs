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
        Logger.Log("[EmailPollingService] Starting background email polling service...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAgentsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError($"[EmailPollingService] Error in polling loop: {ex.Message}", additionalData: ex.StackTrace);
            }

            Logger.Log($"[EmailPollingService] Waiting {_pollingIntervalSeconds} seconds until next check...");
            await Task.Delay(_pollingIntervalSeconds * 1000, stoppingToken);
        }
    }

    private async Task ProcessAgentsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultdbContext>();

        // Update server lifesign
        await ServerRegistrationService.UpdateLifesignAsync(db);

        // Load all active agents
        var agents = await db.Agents
            .Include(a => a.Customer)
            .Include(a => a.Mcpservers)
            .Where(a => a.State == "active" && a.Emailprovidertype == "polling")
            .ToListAsync();

        Logger.Log($"[EmailPollingService] Found {agents.Count} active agent(s) for polling");

        foreach (var agent in agents)
        {
            try
            {
                // Skip agent if it was polled less than 60 seconds ago (prevents duplicate processing on multiple servers)
                if (agent.Lastpoll.HasValue && (DateTime.UtcNow - agent.Lastpoll.Value).TotalSeconds < 60)
                {
                    Logger.Log($"[EmailPollingService] Skipping agent {agent.Id} - last polled {(int)(DateTime.UtcNow - agent.Lastpoll.Value).TotalSeconds}s ago", agent.Id);
                    continue;
                }

                // Mark agent as polled
                agent.Lastpoll = DateTime.UtcNow;
                await db.SaveChangesAsync();

                Logger.Log($"[EmailPollingService] Processing agent {agent.Id} ({agent.Emailaddress})", agent.Id);

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
                    Logger.Log($"[EmailPollingService] Found {emails.Length} new email(s) for agent {agent.Id}", agent.Id);

                    foreach (var email in emails)
                    {
                        Logger.Log($"[EmailPollingService] Email from: {email.From}, Subject: {email.Subject}", agent.Id);
                        var mail = await provider.GetMail(email, agent);

                        if (mail != null)
                        {
                            // Check if this email was already processed (prevent duplicate processing)
                            var alreadyProcessed = await db.Conversations
                                .AnyAsync(c => c.AgentId == agent.Id && c.Messageid == mail.Id);
                            if (alreadyProcessed)
                            {
                                Logger.Log($"[EmailPollingService] Skipping email {mail.Id} - already processed (conversation exists for agent {agent.Id})", agent.Id);
                                continue;
                            }

                            try
                            {
                                // Log email content as additional data
                                var emailData = $"From: {mail.From}\nSubject: {mail.Subject}\nDate: {mail.CreatedAt}\n\nContent:\n{mail.Text}";
                                Logger.Log($"[EmailPollingService] Processing email: {mail.Subject}", agent.Id, emailData);

                                // Process mail with AI
                                var processor = new ProcessMailsClass(db, _configuration);
                                var aiResponse = await processor.ProcessMailAsync(mail, agent);

                                Logger.Log($"[EmailPollingService] AI Response generated", agent.Id, aiResponse.AiExplanation);

                                // Only send reply if there's actual content
                                if (!string.IsNullOrEmpty(aiResponse.EmailResponseText) || !string.IsNullOrEmpty(aiResponse.EmailResponseHtml))
                                {
                                    await provider.SendReplyResponseEmail(aiResponse, mail, agent);
                                    Logger.Log($"[EmailPollingService] Reply sent to {mail.From}", agent.Id, aiResponse.EmailResponseText);
                                }
                                else
                                {
                                    Logger.Log($"[EmailPollingService] No reply sent (forwarded or no response required)", agent.Id);
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"[EmailPollingService] Error processing email '{mail.Subject}': {ex.Message}", agent.Id, ex.StackTrace);

                                // Mark email as unread so it gets picked up again next time
                                Logger.Log($"[EmailPollingService] Marking email as unread for retry", agent.Id);
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
                                    Logger.Log($"[EmailPollingService] Removed conversation {conversation.Id} due to processing error", agent.Id);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Logger.Log($"[EmailPollingService] No new emails for agent {agent.Id}", agent.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[EmailPollingService] Error processing agent {agent.Id}: {ex.Message}", agent.Id, ex.StackTrace);
            }
        }
    }
}
