using UTXO_E_Mail_Agent_Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace UTXO_E_Mail_Agent.McpServers;

/// <summary>
/// MCP Server für E-Mail-bezogene Tools
/// </summary>
public static class EmailMcpServer
{
    /// <summary>
    /// Sucht nach früheren E-Mails eines Kunden
    /// </summary>
    public static async Task<string> SearchCustomerEmails(string emailAddress, string connectionString, int limit = 5)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
        optionsBuilder.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString));
        await using var db = new DefaultdbContext(optionsBuilder.Options);

        var conversations = await db.Conversations
            .Where(c => c.Emailfrom.Contains(emailAddress))
            .OrderByDescending(c => c.Emailreceived)
            .Take(limit)
            .Select(c => new
            {
                c.Subject,
                c.Emailreceived,
                c.Text,
                c.Agentresponsetext
            })
            .ToListAsync();

        if (!conversations.Any())
        {
            return $"Keine früheren E-Mails von {emailAddress} gefunden.";
        }

        var result = $"Frühere E-Mails von {emailAddress}:\n\n";
        foreach (var conv in conversations)
        {
            result += $"- Datum: {conv.Emailreceived:dd.MM.yyyy HH:mm}\n";
            result += $"  Betreff: {conv.Subject}\n";
            result += $"  Nachricht: {conv.Text?.Substring(0, Math.Min(200, conv.Text?.Length ?? 0))}...\n";
            result += $"  Unsere Antwort: {conv.Agentresponsetext?.Substring(0, Math.Min(200, conv.Agentresponsetext?.Length ?? 0))}...\n\n";
        }

        return result;
    }

    /// <summary>
    /// Sucht nach Konversationen zu einem bestimmten Thema/Betreff
    /// </summary>
    public static async Task<string> SearchBySubject(string searchTerm, string connectionString, int limit = 5)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
        optionsBuilder.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString));
        await using var db = new DefaultdbContext(optionsBuilder.Options);

        var conversations = await db.Conversations
            .Where(c => c.Subject.Contains(searchTerm))
            .OrderByDescending(c => c.Emailreceived)
            .Take(limit)
            .Select(c => new
            {
                c.Subject,
                c.Emailreceived,
                c.Emailfrom,
                c.Agentresponsetext
            })
            .ToListAsync();

        if (!conversations.Any())
        {
            return $"Keine Konversationen zum Thema '{searchTerm}' gefunden.";
        }

        var result = $"Konversationen zum Thema '{searchTerm}':\n\n";
        foreach (var conv in conversations)
        {
            result += $"- Von: {conv.Emailfrom}\n";
            result += $"  Datum: {conv.Emailreceived:dd.MM.yyyy HH:mm}\n";
            result += $"  Betreff: {conv.Subject}\n\n";
        }

        return result;
    }

    /// <summary>
    /// Gibt Statistiken über E-Mail-Konversationen zurück
    /// </summary>
    public static async Task<string> GetEmailStats(int agentId, string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
        optionsBuilder.UseMySql(connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString));
        await using var db = new DefaultdbContext(optionsBuilder.Options);

        var stats = await db.Conversations
            .Where(c => c.AgentId == agentId)
            .GroupBy(c => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Last7Days = g.Count(c => c.Emailreceived >= DateTime.Now.AddDays(-7)),
                Last30Days = g.Count(c => c.Emailreceived >= DateTime.Now.AddDays(-30))
            })
            .FirstOrDefaultAsync();

        if (stats == null)
        {
            return "Keine Statistiken verfügbar.";
        }

        return $"E-Mail Statistiken für Agent {agentId}:\n" +
               $"- Gesamt: {stats.Total} E-Mails\n" +
               $"- Letzte 7 Tage: {stats.Last7Days} E-Mails\n" +
               $"- Letzte 30 Tage: {stats.Last30Days} E-Mails";
    }
}
