using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent.McpServers;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Api;

public static class SearchConversationsEndpoint
{
    public static void MapSearchConversationsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/search_conversations", async (SearchConversationsRequest request, DefaultdbContext db) =>
        {
            try
            {
                if (string.IsNullOrEmpty(request.AgentName))
                {
                    return Results.BadRequest(new { success = false, error = "AgentName is required" });
                }

                var agentNameLower = request.AgentName.ToLower();
                var agent = await db.Agents
                    .Where(a => a.Agentname.ToLower() == agentNameLower && a.State == "active")
                    .FirstOrDefaultAsync();

                if (agent == null)
                {
                    return Results.BadRequest(new { success = false, error = $"Agent '{request.AgentName}' not found" });
                }

                var result = await ConversationSearchMcpServer.SearchConversations(
                    agent.Id, request.EmailAddress, request.SearchTerm,
                    request.DaysBack, request.Limit, existingDb: db);

                Logger.Log($"[API] search_conversations by agent '{agent.Agentname}': email={request.EmailAddress}, term={request.SearchTerm}", agent.Id);

                return Results.Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[API] search_conversations error: {ex.Message}");
                return Results.StatusCode(500);
            }
        });

        app.MapPost("/api/get_attachment", async (GetAttachmentRequest request, DefaultdbContext db) =>
        {
            try
            {
                if (string.IsNullOrEmpty(request.AgentName))
                {
                    return Results.BadRequest(new { success = false, error = "AgentName is required" });
                }

                var agentNameLower = request.AgentName.ToLower();
                var agent = await db.Agents
                    .Where(a => a.Agentname.ToLower() == agentNameLower && a.State == "active")
                    .FirstOrDefaultAsync();

                if (agent == null)
                {
                    return Results.BadRequest(new { success = false, error = $"Agent '{request.AgentName}' not found" });
                }

                var result = await ConversationSearchMcpServer.GetAttachment(
                    agent.Id, request.AttachmentId, request.SaveToDirectory, existingDb: db);

                Logger.Log($"[API] get_attachment by agent '{agent.Agentname}': attachmentId={request.AttachmentId}", agent.Id);

                return Results.Ok(new { success = true, result });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[API] get_attachment error: {ex.Message}");
                return Results.StatusCode(500);
            }
        });
    }
}

public class SearchConversationsRequest
{
    public string AgentName { get; set; } = null!;
    public string? EmailAddress { get; set; }
    public string? SearchTerm { get; set; }
    public int DaysBack { get; set; } = 7;
    public int Limit { get; set; } = 10;
}

public class GetAttachmentRequest
{
    public string AgentName { get; set; } = null!;
    public int AttachmentId { get; set; }
    public string? SaveToDirectory { get; set; }
}
