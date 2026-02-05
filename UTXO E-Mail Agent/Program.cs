using Claude.AgentSdk;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Factory;
using UTXO_E_Mail_Agent_Shared.Models;

// To update models from database:
// dotnet ef dbcontext scaffold "server=YOUR_SERVER;port=YOUR_PORT;user=YOUR_USER;password=YOUR_PASSWORD;database=YOUR_DB" Pomelo.EntityFrameworkCore.MySql -o Models --project "../UTXO E-Mail Agent Shared" --force --no-onconfiguring

// The --no-onconfiguring flag prevents hardcoding credentials in DefaultdbContext.cs

namespace UTXO_E_Mail_Agent;

public class Program
{
    private static IConfiguration _configuration = null!;
    private static int _pollingIntervalSeconds;
    private static string _connectionString = null!;

    public static async Task Main(string[] args)
    {
        // Configuration laden
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        _pollingIntervalSeconds = int.Parse(_configuration["AppSettings:PollingIntervalSeconds"] ?? "60");
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        Console.WriteLine("NMKR E-Mail Agent");
        Console.WriteLine($"Polling interval: {_pollingIntervalSeconds} seconds");
        Console.WriteLine("Starting main loop...");

        // Dauerschleife
        while (true)
        {
            try
            {
                await ProcessAgentsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in main loop: {ex.Message}");
            }

            Console.WriteLine($"Waiting {_pollingIntervalSeconds} seconds until next check...");
            await Task.Delay(_pollingIntervalSeconds * 1000);
        }
    }

    private static async Task ProcessAgentsAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
        optionsBuilder.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString));

        await using var db = new DefaultdbContext(optionsBuilder.Options);

        // Alle aktiven Agents laden
        var agents = await db.Agents
            .Where(a => a.State == "active" && a.Emailprovidertype=="polling")
            .ToListAsync();

        Console.WriteLine($"Found {agents.Count} active agent(s)");

        foreach (var agent in agents)
        {
            try
            {
                Console.WriteLine($"Processing agent {agent.Id} ({agent.Emailaddress}) with provider: {agent.Emailprovider}");

                // EmailProvider anhand des Typs auswählen
                var provider = EmailProviderFactory.GetProvider(agent.Emailprovider, _configuration, db);

                if (provider == null)
                {
                    Console.WriteLine($"Unknown email provider: {agent.Emailprovider}");
                    continue;
                }


                var emails = await provider.GetEmailsAsync(agent);

                if (emails != null && emails.Length > 0)
                {
                    Console.WriteLine($"Found {emails.Length} new email(s) for agent {agent.Id}");

                   
                    foreach (var email in emails)
                    {
                        Console.WriteLine($"  - Email from: {email.From}, Subject: {email.Subject}");
                        var mail = await provider.GetMail(email, agent);

                        if (mail != null)
                        {
                            // Zeige Vorschau des E-Mail-Inhalts
                            var preview = mail.Text?.Length > 100
                                ? mail.Text.Substring(0, 100) + "..."
                                : mail.Text ?? "[Kein Text-Inhalt]";
                            Console.WriteLine($"  - Email content preview: {preview}");

                            // Mail mit AI verarbeiten
                            var processor = new ProcessMailsClass(db, _connectionString);
                            var aiResponse = await processor.ProcessMailAsync(mail, agent);

                            Console.WriteLine("  - AI Response:");
                            Console.WriteLine($"    {aiResponse.EmailResponseText}");
                            
                            await provider.SendReplyResponseEmail(aiResponse,mail, agent);
                            
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"No new emails for agent {agent.Id}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing agent {agent.Id}: {ex.Message}");
            }
        }
    }
}