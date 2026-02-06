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
    // Version information - update this with each release
    private const string Version = "1.2.0";
    private const string BuildDate = "2026-02-05";

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

        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  UTXO E-Mail Agent");
        Console.WriteLine($"  Version: {Version} (Build: {BuildDate})");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"Polling interval: {_pollingIntervalSeconds} seconds");
        Console.WriteLine("Press 't' during wait to run test mode");
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

            Console.WriteLine($"Waiting {_pollingIntervalSeconds} seconds until next check... (Press 't' for test mode)");

            // Warte auf Timeout oder Tastendruck
            var waitStart = DateTime.Now;
            var waitSeconds = _pollingIntervalSeconds;

            while ((DateTime.Now - waitStart).TotalSeconds < waitSeconds)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 't' || key.KeyChar == 'T')
                    {
                        Console.WriteLine("\n[TEST MODE] Starting test scenario...");
                        try
                        {
                            await RunTestScenarioAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[TEST MODE] Error: {ex.Message}");
                        }
                        Console.WriteLine("[TEST MODE] Test completed. Resuming normal operation...");
                    }
                }
                await Task.Delay(100); // Check every 100ms
            }
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

    private static async Task RunTestScenarioAsync()
    {
        var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
        optionsBuilder.UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString));

        await using var db = new DefaultdbContext(optionsBuilder.Options);

        // Lade den ersten aktiven Agent
        var agent = await db.Agents
            .Where(a => a.State == "active")
            .FirstOrDefaultAsync();

        if (agent == null)
        {
            Console.WriteLine("[TEST MODE] No active agent found for testing.");
            return;
        }

        Console.WriteLine($"[TEST MODE] Using agent {agent.Id} ({agent.Emailaddress})");

        // Erstelle eine Test-Mail
        var testMail = new MailClass
        {
            Id = "TEST-" + Guid.NewGuid().ToString(),
            Type = "email",
            From = "test@example.com",
            To = new[] { agent.Emailaddress },
            Subject = "internetverfügbarkeit",
            Status = "unread",
            CreatedAt = DateTime.Now.ToString("o"),
            Text = @"Sehr geehrte Damen und Herren,

ich würde gerne wissen, ob sie auch Internet in Küssaberg, im Freudenspiel 70 anbieten.

Viele Grüße
Reimund Schilder",
            Html = @"<p>Sehr geehrte Damen und Herren,</p>
<p>ich würde gerne wissen, ob sie auch Internet in Küssaberg, im Freudenspiel 70 anbieten.</p>
<p>Viele Grüße<br>Reimund Schilder</p>",
            Cc = Array.Empty<string>(),
            Bcc = Array.Empty<string>(),
            ReplyTo = Array.Empty<string>(),
            Attachments = Array.Empty<string>()
        };

        Console.WriteLine($"[TEST MODE] Test email from: {testMail.From}");
        Console.WriteLine($"[TEST MODE] Subject: {testMail.Subject}");
        Console.WriteLine($"[TEST MODE] Content preview: {testMail.Text?.Substring(0, Math.Min(100, testMail.Text.Length))}...");

        // Verarbeite die Test-Mail mit AI
        var processor = new ProcessMailsClass(db, _connectionString);
        var aiResponse = await processor.ProcessMailAsync(testMail, agent);

        Console.WriteLine("[TEST MODE] ========================================");
        Console.WriteLine("[TEST MODE] AI Response:");
        Console.WriteLine(aiResponse.EmailResponseText);
        Console.WriteLine("[TEST MODE] ========================================");

        if (!string.IsNullOrEmpty(aiResponse.AiExplanation))
        {
            Console.WriteLine("[TEST MODE] AI Explanation:");
            Console.WriteLine(aiResponse.AiExplanation);
            Console.WriteLine("[TEST MODE] ========================================");
        }

        Console.WriteLine("[TEST MODE] Email would be sent to: " + testMail.From);
        Console.WriteLine("[TEST MODE] (Not actually sending in test mode)");
    }
}