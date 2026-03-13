using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.McpServers;

/// <summary>
/// Shared logic for searching conversations and retrieving attachments.
/// Used by both REST API endpoints (Claude Code) and built-in tools (Anthropic SDK).
/// </summary>
public static class ConversationSearchMcpServer
{
    /// <summary>
    /// Search conversations by agent, email address, search term, and time range.
    /// Returns conversation metadata with attachment info (no Base64 content).
    /// </summary>
    public static async Task<string> SearchConversations(
        int agentId, string? emailAddress, string? searchTerm,
        int daysBack = 7, int limit = 10, string? connectionString = null, DefaultdbContext? existingDb = null)
    {
        var db = existingDb;
        DefaultdbContext? ownedDb = null;

        if (db == null && !string.IsNullOrEmpty(connectionString))
        {
            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            ownedDb = new DefaultdbContext(optionsBuilder.Options);
            db = ownedDb;
        }

        if (db == null)
            return "Error: No database connection available.";

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysBack);

            var query = db.Conversations
                .Where(c => c.AgentId == agentId && c.Emailreceived >= cutoff);

            if (!string.IsNullOrEmpty(emailAddress))
            {
                query = query.Where(c => c.Emailfrom.Contains(emailAddress));
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c =>
                    c.Subject.Contains(searchTerm) ||
                    (c.Text != null && c.Text.Contains(searchTerm)) ||
                    (c.Agentresponsetext != null && c.Agentresponsetext.Contains(searchTerm)));
            }

            var conversations = await query
                .OrderByDescending(c => c.Emailreceived)
                .Take(limit)
                .Select(c => new
                {
                    c.Id,
                    c.Emailfrom,
                    c.Subject,
                    Text = c.Text != null ? c.Text.Substring(0, Math.Min(500, c.Text.Length)) : null,
                    AgentResponse = c.Agentresponsetext != null
                        ? c.Agentresponsetext.Substring(0, Math.Min(500, c.Agentresponsetext.Length))
                        : null,
                    c.Emailreceived,
                    Attachments = c.ConversationAttachments.Select(a => new
                    {
                        a.Id,
                        a.Filename,
                        a.ContentType
                    }).ToList()
                })
                .AsNoTracking()
                .ToListAsync();

            if (!conversations.Any())
            {
                return "Keine Conversations gefunden.";
            }

            var result = $"Gefunden: {conversations.Count} Conversation(s):\n\n";
            foreach (var conv in conversations)
            {
                result += $"--- Conversation #{conv.Id} ---\n";
                result += $"Von: {conv.Emailfrom}\n";
                result += $"Betreff: {conv.Subject}\n";
                result += $"Datum: {conv.Emailreceived:dd.MM.yyyy HH:mm}\n";
                if (!string.IsNullOrEmpty(conv.Text))
                    result += $"Nachricht: {conv.Text}\n";
                if (!string.IsNullOrEmpty(conv.AgentResponse))
                    result += $"Agent-Antwort: {conv.AgentResponse}\n";
                if (conv.Attachments.Any())
                {
                    result += "Attachments:\n";
                    foreach (var att in conv.Attachments)
                    {
                        result += $"  - ID: {att.Id}, Datei: {att.Filename} ({att.ContentType})\n";
                    }
                }
                result += "\n";
            }

            return result;
        }
        finally
        {
            if (ownedDb != null)
                await ownedDb.DisposeAsync();
        }
    }

    /// <summary>
    /// Get an attachment by ID, verify it belongs to the given agent, and save it to outputDirectory.
    /// Returns the file path of the saved attachment.
    /// </summary>
    public static async Task<string> GetAttachment(
        int agentId, int attachmentId, string? outputDirectory = null,
        string? connectionString = null, DefaultdbContext? existingDb = null)
    {
        var db = existingDb;
        DefaultdbContext? ownedDb = null;

        if (db == null && !string.IsNullOrEmpty(connectionString))
        {
            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            ownedDb = new DefaultdbContext(optionsBuilder.Options);
            db = ownedDb;
        }

        if (db == null)
            return "Error: No database connection available.";

        try
        {
            var attachment = await db.ConversationAttachments
                .Include(a => a.Conversation)
                .Where(a => a.Id == attachmentId && a.Conversation.AgentId == agentId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (attachment == null)
            {
                return $"Attachment mit ID {attachmentId} nicht gefunden oder gehört nicht zu diesem Agent.";
            }

            // If outputDirectory is provided, save the file there
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                var filePath = Path.Combine(outputDirectory, attachment.Filename);
                var fileBytes = Convert.FromBase64String(attachment.Content);
                await File.WriteAllBytesAsync(filePath, fileBytes);

                return $"Attachment gespeichert: {filePath}\n" +
                       $"Dateiname: {attachment.Filename}\n" +
                       $"Typ: {attachment.ContentType}\n" +
                       $"Größe: {fileBytes.Length} Bytes";
            }

            // Otherwise return metadata with Base64 content
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                id = attachment.Id,
                filename = attachment.Filename,
                contentType = attachment.ContentType,
                content = attachment.Content
            });
        }
        finally
        {
            if (ownedDb != null)
                await ownedDb.DisposeAsync();
        }
    }
}
