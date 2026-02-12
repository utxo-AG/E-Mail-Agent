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
        Console.WriteLine("[EmailPollingService] Starting background email polling service...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAgentsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailPollingService] Error in polling loop: {ex.Message}");
            }

            Console.WriteLine($"[EmailPollingService] Waiting {_pollingIntervalSeconds} seconds until next check...");
            await Task.Delay(_pollingIntervalSeconds * 1000, stoppingToken);
        }
    }

    private async Task ProcessAgentsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DefaultdbContext>();

        // Load all active agents
        var agents = await db.Agents
            .Include(a => a.Mcpservers)
            .Where(a => a.State == "active" && a.Emailprovidertype == "polling")
            .ToListAsync();

        Console.WriteLine($"[EmailPollingService] Found {agents.Count} active agent(s) for polling");

        foreach (var agent in agents)
        {
            try
            {
                Console.WriteLine($"[EmailPollingService] Processing agent {agent.Id} ({agent.Emailaddress}) with provider: {agent.Emailprovider}");

                // Select email provider by type
                var provider = EmailProviderFactory.GetProvider(agent.Emailprovider, _configuration, db);

                if (provider == null)
                {
                    Console.WriteLine($"[EmailPollingService] Unknown email provider: {agent.Emailprovider}");
                    continue;
                }

                var emails = await provider.GetEmailsAsync(agent);

                if (emails != null && emails.Length > 0)
                {
                    Console.WriteLine($"[EmailPollingService] Found {emails.Length} new email(s) for agent {agent.Id}");

                    foreach (var email in emails)
                    {
                        Console.WriteLine($"[EmailPollingService]   - Email from: {email.From}, Subject: {email.Subject}");
                        var mail = await provider.GetMail(email, agent);

                        if (mail != null)
                        {
                            // Show preview of email content
                            var preview = mail.Text?.Length > 100
                                ? mail.Text.Substring(0, 100) + "..."
                                : mail.Text ?? "[No text content]";
                            Console.WriteLine($"[EmailPollingService]   - Email content preview: {preview}");

                            // Process mail with AI
                            var processor = new ProcessMailsClass(db, _configuration);
                            var aiResponse = await processor.ProcessMailAsync(mail, agent);

                            Console.WriteLine("[EmailPollingService]   - AI Response generated");

                            // Send reply
                            await provider.SendReplyResponseEmail(aiResponse, mail, agent);
                            Console.WriteLine("[EmailPollingService]   - Reply sent");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[EmailPollingService] No new emails for agent {agent.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailPollingService] Error processing agent {agent.Id}: {ex.Message}");
            }
        }
    }
}