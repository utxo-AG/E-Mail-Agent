using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Models;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Api;

public static class ProcessTextEndpoint
{
    public static void MapProcessTextEndpoints(this WebApplication app)
    {
        app.MapPost("/api/processtext", async (ProcessTextRequestClass request, DefaultdbContext db, IConfiguration configuration) =>
        {
            try
            {
                // Get agent (default to first active agent if not specified)
                Agent? agent;
                if (request.AgentId.HasValue)
                {
                    agent = await db.Agents
                        .Include(a => a.Customer)
                        .Include(a => a.Mcpservers)
                        .Include(a => a.Skills)
                        .Where(a => a.Id == request.AgentId.Value && a.State == "active")
                        .FirstOrDefaultAsync();
                }
                else
                {
                    agent = await db.Agents
                        .Include(a => a.Customer)
                        .Include(a => a.Mcpservers)
                        .Include(a => a.Skills)
                        .Where(a => a.State == "active")
                        .FirstOrDefaultAsync();
                }

                if (agent == null)
                {
                    return Results.BadRequest(new ProcessEmailResponse
                    {
                        Success = false,
                        Error = "No active agent found"
                    });
                }

                // Create mail object from request
                var mail = new MailClass
                {
                    Id = "API-" + Guid.NewGuid().ToString(),
                    Type = "email",
                    From = "api@example.com",
                    To = [agent.Emailaddress],
                    Subject = "API Request",
                    Status = "unread",
                    CreatedAt = DateTime.Now.ToString("o"),
                    Text = request.TextContent,
                    Html = string.Empty,
                    Cc = Array.Empty<string>(),
                    Bcc = Array.Empty<string>(),
                    ReplyTo = Array.Empty<string>(),
                    Attachments = Array.Empty<string>()
                };

                // Process with AI
                var processor = new ProcessMailsClass(db, configuration);
                var aiResponse = await processor.ProcessMailAsync(mail, agent);

                // Build response
                var response = new ProcessEmailResponse
                {
                    Success = true,
                    EmailResponseText = aiResponse.EmailResponseText,
                    EmailResponseSubject = aiResponse.EmailResponseSubject,
                    EmailResponseHtml = aiResponse.EmailResponseHtml,
                    AiExplanation = aiResponse.AiExplanation
                };

                // Add attachments to response
                if (aiResponse.Attachments != null)
                {
                    foreach (var att in aiResponse.Attachments)
                    {
                        response.Attachments.Add(new AttachmentResponse
                        {
                            Filename = att.Filename,
                            ContentType = att.ContentType,
                            Content = att.Content
                        });
                    }
                }

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                Logger.LogError($"[API] Error processing text: {ex.Message}");
                return Results.BadRequest(new ProcessEmailResponse
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        })
        .WithName("ProcessText")
        .WithSummary("Process text with AI")
        .Produces<ProcessEmailResponse>(StatusCodes.Status200OK)
        .Produces<ProcessEmailResponse>(StatusCodes.Status400BadRequest);
    }
}
