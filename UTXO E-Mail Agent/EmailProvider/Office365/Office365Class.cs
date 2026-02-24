using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Me.SendMail;
using Microsoft.Kiota.Abstractions.Authentication;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.EmailProvider.Office365;

/// <summary>
/// Email provider implementation for Microsoft Office 365 using Microsoft Graph API
/// </summary>
public class Office365Class : IEmailProvider
{
    private readonly IConfiguration _config;
    private readonly DefaultdbContext _db;
    private readonly Office365TokenService _tokenService;

    public Office365Class(IConfiguration config, DefaultdbContext db)
    {
        _config = config;
        _db = db;
        _tokenService = new Office365TokenService(config, db);
    }

    /// <summary>
    /// Creates a GraphServiceClient with the agent's access token
    /// </summary>
    private GraphServiceClient CreateGraphClient(string accessToken)
    {
        var authProvider = new BaseBearerTokenAuthenticationProvider(
            new TokenProvider(accessToken));
        return new GraphServiceClient(authProvider);
    }

    public async Task<ListNewEmailsClass[]?> GetEmailsAsync(Agent agent)
    {
        try
        {
            var accessToken = await _tokenService.GetValidTokenAsync(agent);
            if (string.IsNullOrEmpty(accessToken))
            {
                Logger.LogError("[Office365] No valid access token available. User needs to re-authenticate.", agent.Id);
                return null;
            }

            var graphClient = CreateGraphClient(accessToken);

            // Get unread messages from inbox
            var messages = await graphClient.Me.Messages.GetAsync(config =>
            {
                config.QueryParameters.Filter = "isRead eq false";
                config.QueryParameters.Top = 50;
                config.QueryParameters.Select = new[] { "id", "from", "toRecipients", "subject", "receivedDateTime", "hasAttachments" };
                config.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
            });

            if (messages?.Value == null || !messages.Value.Any())
            {
                Logger.Log("[Office365] No unread emails found", agent.Id);
                return Array.Empty<ListNewEmailsClass>();
            }

            var allEmails = messages.Value.Select(msg => new ListNewEmailsClass
            {
                Id = msg.Id ?? "",
                Type = "received",
                From = msg.From?.EmailAddress?.Address ?? "unknown",
                To = msg.ToRecipients?.Select(r => r.EmailAddress?.Address ?? "").ToArray() ?? Array.Empty<string>(),
                Subject = msg.Subject ?? "(No Subject)",
                Status = "unread",
                CreatedAt = msg.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                HasAttachments = msg.HasAttachments
            }).ToList();

            // Filter out already processed emails
            var result = new List<ListNewEmailsClass>();
            foreach (var email in allEmails)
            {
                var alreadyProcessed = await _db.Conversations
                    .AsNoTracking()
                    .AnyAsync(c => c.AgentId == agent.Id && c.Messageid == email.Id);
                
                if (alreadyProcessed)
                {
                    // Mark as read in Office 365
                    await MarkAsReadAsync(email.Id, accessToken);
                    Logger.Log($"[Office365] Marked email {email.Id} as read - already processed", agent.Id);
                    continue;
                }
                result.Add(email);
            }

            Logger.Log($"[Office365] Found {allEmails.Count} unread, {result.Count} new email(s)", agent.Id);
            return result.ToArray();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Office365] Error fetching emails: {ex.Message}", agent.Id);
            throw;
        }
    }

    public async Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent)
    {
        try
        {
            var accessToken = await _tokenService.GetValidTokenAsync(agent);
            if (string.IsNullOrEmpty(accessToken))
            {
                Logger.LogError("[Office365] No valid access token available.", agent.Id);
                return null;
            }

            var graphClient = CreateGraphClient(accessToken);

            // Get full message details
            var message = await graphClient.Me.Messages[email.Id].GetAsync(config =>
            {
                config.QueryParameters.Select = new[] 
                { 
                    "id", "from", "toRecipients", "ccRecipients", "bccRecipients", "replyTo",
                    "subject", "body", "receivedDateTime", "hasAttachments"
                };
            });

            if (message == null)
            {
                Logger.LogError($"[Office365] Message {email.Id} not found", agent.Id);
                return null;
            }

            // Get attachments if any
            var attachmentNames = new List<string>();
            if (message.HasAttachments == true)
            {
                var attachments = await graphClient.Me.Messages[email.Id].Attachments.GetAsync();
                if (attachments?.Value != null)
                {
                    attachmentNames.AddRange(attachments.Value
                        .Where(a => a is FileAttachment)
                        .Select(a => a.Name ?? "unnamed"));
                }
            }

            var result = new MailClass
            {
                Id = message.Id ?? "",
                From = message.From?.EmailAddress?.Address ?? "unknown",
                To = message.ToRecipients?.Select(r => r.EmailAddress?.Address ?? "").ToArray() ?? Array.Empty<string>(),
                Cc = message.CcRecipients?.Select(r => r.EmailAddress?.Address ?? "").ToArray() ?? Array.Empty<string>(),
                Bcc = message.BccRecipients?.Select(r => r.EmailAddress?.Address ?? "").ToArray() ?? Array.Empty<string>(),
                ReplyTo = message.ReplyTo?.Select(r => r.EmailAddress?.Address ?? "").ToArray() ?? Array.Empty<string>(),
                Subject = message.Subject ?? "(No Subject)",
                Text = message.Body?.ContentType == BodyType.Text ? message.Body.Content : StripHtml(message.Body?.Content),
                Html = message.Body?.ContentType == BodyType.Html ? message.Body.Content : null,
                CreatedAt = message.ReceivedDateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Attachments = attachmentNames.ToArray(),
                HasAttachments = attachmentNames.Any()
            };

            // Mark as read
            await MarkAsReadAsync(email.Id, accessToken);

            Logger.Log($"[Office365] Successfully fetched email: {result.Subject}", agent.Id);
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Office365] Error fetching email details: {ex.Message}", agent.Id);
            throw;
        }
    }

    public async Task MarkAsUnreadAsync(ListNewEmailsClass email, Agent agent)
    {
        try
        {
            var accessToken = await _tokenService.GetValidTokenAsync(agent);
            if (string.IsNullOrEmpty(accessToken))
            {
                Logger.LogError("[Office365] No valid access token available.", agent.Id);
                return;
            }

            var graphClient = CreateGraphClient(accessToken);

            await graphClient.Me.Messages[email.Id].PatchAsync(new Message
            {
                IsRead = false
            });

            Logger.Log($"[Office365] Marked email {email.Id} as unread again", agent.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Office365] Error marking email as unread: {ex.Message}", agent.Id);
        }
    }

    public async Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent, UTXO_E_Mail_Agent_Shared.Models.Conversation? conversation)
    {
        try
        {
            var accessToken = await _tokenService.GetValidTokenAsync(agent);
            if (string.IsNullOrEmpty(accessToken))
            {
                Logger.LogError("[Office365] No valid access token available.", agent.Id);
                throw new InvalidOperationException("No valid Office 365 access token");
            }

            var graphClient = CreateGraphClient(accessToken);

            // Create the reply message
            var replyMessage = new Message
            {
                Subject = emailResponse.Subject ?? $"Re: {mail.Subject}",
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = emailResponse.Html ?? emailResponse.Text ?? ""
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = mail.From
                        }
                    }
                }
            };

            // Handle attachments if any
            if (emailResponse.Attachments != null && emailResponse.Attachments.Any())
            {
                replyMessage.Attachments = new List<Attachment>();
                foreach (var attachment in emailResponse.Attachments)
                {
                    if (!string.IsNullOrEmpty(attachment.Content))
                    {
                        replyMessage.Attachments.Add(new FileAttachment
                        {
                            Name = attachment.Filename,
                            ContentType = attachment.ContentType ?? "application/octet-stream",
                            ContentBytes = Convert.FromBase64String(attachment.Content)
                        });
                    }
                }
            }

            // Send the email
            await graphClient.Me.SendMail.PostAsync(new SendMailPostRequestBody
            {
                Message = replyMessage,
                SaveToSentItems = true
            });

            // Log to sent emails
            if (conversation != null)
            {
                var sentEmail = new Sentemail
                {
                    ConversationId = conversation.Id,
                    Emailreceiver = mail.From,
                    Subject = replyMessage.Subject,
                    Emailtext = emailResponse.Text ?? emailResponse.Html ?? "",
                    Created = DateTime.UtcNow
                };
                _db.Sentemails.Add(sentEmail);
                await _db.SaveChangesAsync();
            }

            Logger.Log($"[Office365] Successfully sent reply to {mail.From}", agent.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Office365] Error sending reply: {ex.Message}", agent.Id);
            throw;
        }
    }

    /// <summary>
    /// Marks a message as read in Office 365
    /// </summary>
    private async Task MarkAsReadAsync(string messageId, string accessToken)
    {
        try
        {
            var graphClient = CreateGraphClient(accessToken);
            await graphClient.Me.Messages[messageId].PatchAsync(new Message
            {
                IsRead = true
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"[Office365] Error marking message as read: {ex.Message}");
        }
    }

    /// <summary>
    /// Simple HTML tag stripper for extracting plain text from HTML content
    /// </summary>
    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
    }
}

/// <summary>
/// Simple token provider for Microsoft Graph authentication
/// </summary>
internal class TokenProvider : IAccessTokenProvider
{
    private readonly string _accessToken;

    public TokenProvider(string accessToken)
    {
        _accessToken = accessToken;
    }

    public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_accessToken);
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new();
}
