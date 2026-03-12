using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;
using UTXO_E_Mail_Agent.Factory;
using UTXO_E_Mail_Agent.Models;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Api;

public static class SendEmailEndpoint
{
    public static void MapSendEmailEndpoints(this WebApplication app)
    {
        app.MapPost("/api/send_email", async (SendEmailRequest request, DefaultdbContext db, IConfiguration config) =>
        {
            try
            {
                // Validate agent
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
                
                Logger.Log($"[API] send_email called by agent '{agent.Agentname}': to={request.To}, subject={request.Subject}", agent.Id);
                
                // Use agent's email address as from, or override if specified
                var fromAddress = request.From ?? agent.Emailaddress;
                
                // Get the appropriate email provider for this agent
                var provider = EmailProviderFactory.GetProvider(agent.Emailprovider, config, db);
                if (provider == null)
                {
                    return Results.BadRequest(new { success = false, error = $"No email provider configured for agent '{agent.Agentname}'" });
                }
                
                // Create a mail object - From is used as the recipient in SendReplyResponseEmail
                var mail = new MailClass
                {
                    Id = "send-" + Guid.NewGuid().ToString(),
                    From = request.To, // SendReplyResponseEmail sends TO this address
                    To = new[] { request.To },
                    Subject = request.Subject,
                    ReplyTo = !string.IsNullOrEmpty(request.ReplyTo) ? new[] { request.ReplyTo } : null,
                };
                
                // Create AI response object to use existing send infrastructure
                var aiResponse = new AiResponseClass
                {
                    EmailResponseText = request.Text,
                    EmailResponseSubject = request.Subject,
                    EmailResponseHtml = request.Html ?? $"<html><body>{System.Web.HttpUtility.HtmlEncode(request.Text ?? "").Replace("\n", "<br/>")}</body></html>",
                };
                
                // Load attachments from files if MessageId, AgentId and Attachments are provided
                if (request.Attachments != null && request.Attachments.Length > 0 && !string.IsNullOrEmpty(request.MessageId))
                {
                    // Use provided AgentId or fall back to agent.Id
                    var agentIdForPath = request.AgentId ?? agent.Id;
                    var attachmentsDir = Path.Combine(Path.GetTempPath(), "attachments", agentIdForPath.ToString(), request.MessageId);
                    
                    Logger.Log($"[API] Loading {request.Attachments.Length} attachment(s) from {attachmentsDir}", agent.Id);
                    
                    if (Directory.Exists(attachmentsDir))
                    {
                        var attachments = new List<Attachment>();
                        foreach (var filename in request.Attachments)
                        {
                            var filePath = Path.Combine(attachmentsDir, filename);
                            if (File.Exists(filePath))
                            {
                                try
                                {
                                    var fileContent = await File.ReadAllBytesAsync(filePath);
                                    var base64Content = Convert.ToBase64String(fileContent);
                                    var contentType = GetMimeType(filename);
                                    
                                    attachments.Add(new Attachment
                                    {
                                        Filename = filename,
                                        Content = base64Content,
                                        ContentType = contentType
                                    });
                                    Logger.Log($"[API] Loaded attachment from file: {filename} ({contentType}, {fileContent.Length} bytes)", agent.Id);
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogError($"[API] Error reading attachment file {filename}: {ex.Message}", agent.Id);
                                }
                            }
                            else
                            {
                                Logger.Log($"[API] Attachment file not found: {filePath}", agent.Id);
                            }
                        }
                        aiResponse.Attachments = attachments.ToArray();
                    }
                    else
                    {
                        Logger.Log($"[API] Attachments directory not found: {attachmentsDir}", agent.Id);
                    }
                }
                
                // Use the provider's send method (works for both Inbound and IMAP/SMTP)
                await provider.SendReplyResponseEmail(aiResponse, mail, agent, null);
                
                Logger.Log($"[API] send_email success (agent: {agent.Agentname}, provider: {agent.Emailprovider}, to: {request.To})", agent.Id);
                return Results.Ok(new { success = true, message = $"Email sent from {agent.Emailaddress} to {request.To}" });
            }
            catch (Exception ex)
            {
                Logger.LogError($"[API] send_email error: {ex.Message}");
                return Results.BadRequest(new { success = false, error = ex.Message });
            }
        })
        .WithName("SendEmail")
        .WithSummary("Send an email using the agent's configured email provider")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }
    
    /// <summary>
    /// Get MIME type from file extension
    /// </summary>
    private static string GetMimeType(string filename)
    {
        var extension = Path.GetExtension(filename)?.ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".html" => "text/html",
            ".htm" => "text/html",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".eml" => "message/rfc822",
            _ => "application/octet-stream"
        };
    }
}
