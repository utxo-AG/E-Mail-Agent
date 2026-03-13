using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Api;

public static class ProcessEmailEndpoint
{
    public static void MapProcessEmailEndpoints(this WebApplication app)
    {
        app.MapPost("/api/processemail", async (ProcessMailRequestClass request, DefaultdbContext db, IBackgroundTaskQueue taskQueue) =>
        {
            try
            {
                // Validate agent exists before queuing
                Agent? agent;
                if (!string.IsNullOrEmpty(request.AgentName))
                {
                    // Find agent by name (case-insensitive)
                    var agentNameLower = request.AgentName.ToLower();
                    agent = await db.Agents
                        .Where(a => a.Agentname.ToLower() == agentNameLower && a.State == "active")
                        .FirstOrDefaultAsync();
                }
                else
                {
                    return Results.BadRequest(new { success = false, error = "No Agent specified" });
                }

                if (agent == null)
                {
                    return Results.BadRequest(new { success = false, error = "Agent not found" });
                }

                var agentId = agent.Id;
                var taskId = "API-" + Guid.NewGuid().ToString();
                
                Logger.Log($"[API] Queuing email for agent '{request.AgentName}' (TaskId: {taskId}, OriginalMessageId: {request.MessageId ?? "none"})");

                // Queue the work item for background processing
                taskQueue.QueueBackgroundWorkItem(async (serviceProvider, cancellationToken) =>
                {
                    var scopedDb = serviceProvider.GetRequiredService<DefaultdbContext>();
                    var config = serviceProvider.GetRequiredService<IConfiguration>();

                    // Re-fetch agent with includes in the new scope
                    var scopedAgent = await scopedDb.Agents
                        .Include(a => a.Customer)
                        .Include(a => a.Mcpservers)
                        .Include(a => a.Skills)
                        .Where(a => a.Id == agentId && a.State == "active")
                        .FirstOrDefaultAsync(cancellationToken);

                    if (scopedAgent == null)
                    {
                        Logger.LogError($"[API Background] Agent {agentId} not found for task {taskId}");
                        return;
                    }

                    // Save attachment data to temp directory if provided
                    var attachmentFilenames = request.Attachments ?? Array.Empty<string>();
                    if (request.AttachmentData != null && request.AttachmentData.Length > 0)
                    {
                        var attachmentsDir = Path.Combine(Path.GetTempPath(), "attachments", agentId.ToString(), taskId);
                        Directory.CreateDirectory(attachmentsDir);
                        
                        Logger.Log($"[API Background] Saving {request.AttachmentData.Length} attachment(s) to {attachmentsDir}");
                        
                        var savedFilenames = new List<string>();
                        foreach (var attachment in request.AttachmentData)
                        {
                            if (string.IsNullOrEmpty(attachment.Filename) || string.IsNullOrEmpty(attachment.Content))
                                continue;
                                
                            try
                            {
                                var fileBytes = Convert.FromBase64String(attachment.Content);
                                var filePath = Path.Combine(attachmentsDir, attachment.Filename);
                                await File.WriteAllBytesAsync(filePath, fileBytes, cancellationToken);
                                savedFilenames.Add(attachment.Filename);
                                Logger.Log($"[API Background] Saved attachment: {attachment.Filename} ({fileBytes.Length} bytes)");
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"[API Background] Failed to save attachment {attachment.Filename}: {ex.Message}");
                            }
                        }
                        
                        // Use saved filenames instead of request.Attachments
                        attachmentFilenames = savedFilenames.ToArray();
                    }

                    var mail = new MailClass
                    {
                        Id = taskId,
                        OriginalMessageId = request.MessageId, // Preserve original Inbound ID for reply
                        Type = "internal",
                        From = request.From,
                        To = request.To,
                        Subject = request.Subject,
                        Status = "unread",
                        CreatedAt = request.CreatedAt,
                        Text = request.Text,
                        Html = request.Html,
                        Cc = request.Cc,
                        Bcc = request.Bcc,
                        ReplyTo = request.ReplyTo,
                        Attachments = attachmentFilenames,
                        HasAttachments = attachmentFilenames.Length > 0,
                    };

                    try
                    {
                        var processor = new ProcessMailsClass(scopedDb, config);
                        var aiResponse = await processor.ProcessMailAsync(mail, scopedAgent);
                        
                        // Get email provider for this agent
                        var provider = Factory.EmailProviderFactory.GetProvider(scopedAgent.Emailprovider, config, scopedDb);
                        
                        // Process based on Action field (same logic as EmailPollingService)
                        var action = aiResponse.Action?.ToLowerInvariant() ?? "respond";
                        
                        switch (action)
                        {
                            case "redirect":
                                // Forward the ORIGINAL email with all content to the specified recipients
                                if (aiResponse.RedirectTo != null && aiResponse.RedirectTo.Length > 0)
                                {
                                    if (provider != null)
                                    {
                                        await provider.RedirectEmail(mail, scopedAgent, aiResponse.RedirectTo, aiResponse.RedirectCc, aiResponse.RedirectMessage, aiResponse.Attachments);
                                        Logger.Log($"[API Background] Email redirected to: {string.Join(", ", aiResponse.RedirectTo)}");
                                    }
                                    else
                                    {
                                        Logger.LogWarning($"[API Background] No email provider found for redirect");
                                    }
                                }
                                else
                                {
                                    Logger.Log($"[API Background] Redirect action but no RedirectTo specified");
                                }
                                break;
                                
                            case "delete":
                                Logger.Log($"[API Background] Email marked as spam/deleted - no action taken");
                                break;
                                
                            case "ignore":
                                Logger.Log($"[API Background] Action=ignore - AI handled the task independently");
                                break;
                                
                            case "respond":
                            default:
                                // Original behavior: send reply if there's content
                                if (!string.IsNullOrEmpty(aiResponse.EmailResponseText) || !string.IsNullOrEmpty(aiResponse.EmailResponseHtml))
                                {
                                    // Check if recipient was already emailed via send_email tool
                                    if (!string.IsNullOrEmpty(mail.From) && aiResponse.AlreadySentTo.Contains(mail.From))
                                    {
                                        Logger.Log($"[API Background] Skipping reply to {mail.From} - already sent via send_email tool");
                                    }
                                    else
                                    {
                                        if (provider != null)
                                        {
                                            await provider.SendReplyResponseEmail(aiResponse, mail, scopedAgent, aiResponse.Conversation);
                                            Logger.Log($"[API Background] Reply sent to {mail.From}");
                                        }
                                        else
                                        {
                                            Logger.LogWarning($"[API Background] No email provider found for agent {scopedAgent.Id}");
                                        }
                                    }
                                }
                                else
                                {
                                    Logger.Log($"[API Background] No reply sent (delegated or no response required)");
                                }
                                break;
                        }
                        
                        Logger.Log($"[API Background] Task {taskId} completed successfully");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[API Background] Task {taskId} failed: {ex.Message}");
                    }
                });

                return Results.Accepted(value: new { success = true, taskId = taskId, message = "Email queued for processing" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[API] Error queuing email: {ex.Message}");
                return Results.BadRequest(new { success = false, error = ex.Message });
            }
        })
        .WithName("ProcessEmail")
        .WithSummary("Queue email for AI processing (fire-and-forget)")
        .Produces(StatusCodes.Status202Accepted)
        .Produces(StatusCodes.Status400BadRequest);
    }
}
