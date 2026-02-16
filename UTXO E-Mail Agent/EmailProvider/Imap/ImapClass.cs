using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using Microsoft.Extensions.Configuration;
using MimeKit;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;
using Attachment = UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes.Attachment;
using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.EmailProvider.Imap;

public class ImapClass : IEmailProvider
{
    private readonly IConfiguration _config;

    public ImapClass(IConfiguration config, DefaultdbContext db)
    {
        _config = config;
    }

    public async Task<ListNewEmailsClass[]?> GetEmailsAsync(Agent agent)
    {
        try
        {
            using var client = new ImapClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Connect to IMAP server
            var server = agent.Emailserver ?? throw new InvalidOperationException("Email server not configured");
            var useSsl = agent.Emailusessl ?? true;
            var port = agent.Emailport ?? (useSsl ? 993 : 143); // Default: 993 for SSL, 143 for non-SSL

            await client.ConnectAsync(server, port, useSsl);

            // Authenticate
            var username = agent.Emailusername ?? throw new InvalidOperationException("Email username not configured");
            var password = agent.Emailpassword ?? throw new InvalidOperationException("Email password not configured");
            await client.AuthenticateAsync(username, password);

            // Open INBOX
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);

            // Search for unread messages
            var uids = await inbox.SearchAsync(SearchQuery.NotSeen);

            if (!uids.Any())
            {
                await client.DisconnectAsync(true);
                return Array.Empty<ListNewEmailsClass>();
            }

            // Fetch basic info for unread messages
            var messages = await inbox.FetchAsync(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId);

            var result = messages.Select(msg => new ListNewEmailsClass
            {
                Id = msg.UniqueId.ToString(),
                Type = "received",
                From = msg.Envelope.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown",
                To = new[] { msg.Envelope.To.Mailboxes.FirstOrDefault()?.Address ?? agent.Emailaddress ?? "unknown" },
                Subject = msg.Envelope.Subject ?? "(No Subject)",
                Status = "unread",
                CreatedAt = msg.Envelope.Date?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToArray();

            await client.DisconnectAsync(true);

            Logger.Log($"[IMAP] Found {result.Length} unread email(s)");
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[IMAP] Error fetching emails: {ex.Message}");
            throw;
        }
    }

    public async Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent)
    {
        try
        {
            using var client = new ImapClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Connect to IMAP server
            var server = agent.Emailserver ?? throw new InvalidOperationException("Email server not configured");
            var useSsl = agent.Emailusessl ?? true;
            var port = agent.Emailport ?? (useSsl ? 993 : 143); // Default: 993 for SSL, 143 for non-SSL

            await client.ConnectAsync(server, port, useSsl);

            // Authenticate
            var username = agent.Emailusername ?? throw new InvalidOperationException("Email username not configured");
            var password = agent.Emailpassword ?? throw new InvalidOperationException("Email password not configured");
            await client.AuthenticateAsync(username, password);

            // Open INBOX
            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);

            // Get message by UID
            var uid = new UniqueId(uint.Parse(email.Id));
            var message = await inbox.GetMessageAsync(uid);

            // Extract text content
            var textBody = message.TextBody ?? message.HtmlBody ?? "";

            // Extract attachments - store as filenames for now
            var attachmentNames = new List<string>();
            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart mimePart && !string.IsNullOrEmpty(mimePart.FileName))
                {
                    attachmentNames.Add(mimePart.FileName);
                }
            }

            var result = new MailClass
            {
                Id = email.Id,
                From = message.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown",
                To = new[] { message.To.Mailboxes.FirstOrDefault()?.Address ?? agent.Emailaddress ?? "unknown" },
                Subject = message.Subject ?? "(No Subject)",
                Text = textBody,
                Html = message.HtmlBody,
                CreatedAt = message.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                Attachments = attachmentNames.ToArray(),
                HasAttachments = attachmentNames.Any()
            };

            // Mark as read
            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);

            await client.DisconnectAsync(true);

            Logger.Log($"[IMAP] Successfully fetched email: {result.Subject}");
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[IMAP] Error fetching email details: {ex.Message}");
            throw;
        }
    }

    public async Task MarkAsUnreadAsync(ListNewEmailsClass email, Agent agent)
    {
        try
        {
            using var client = new ImapClient();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var server = agent.Emailserver ?? throw new InvalidOperationException("Email server not configured");
            var useSsl = agent.Emailusessl ?? true;
            var port = agent.Emailport ?? (useSsl ? 993 : 143);

            await client.ConnectAsync(server, port, useSsl);
            await client.AuthenticateAsync(
                agent.Emailusername ?? throw new InvalidOperationException("Email username not configured"),
                agent.Emailpassword ?? throw new InvalidOperationException("Email password not configured"));

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadWrite);

            var uid = new UniqueId(uint.Parse(email.Id));
            await inbox.RemoveFlagsAsync(uid, MessageFlags.Seen, true);

            await client.DisconnectAsync(true);
            Logger.Log($"[IMAP] Marked email {email.Id} as unread again", agent.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[IMAP] Error marking email as unread: {ex.Message}", agent.Id);
        }
    }

    public async Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(agent.Emailaddress, agent.Emailaddress));
            message.To.Add(MailboxAddress.Parse(mail.From));
            message.Subject = mail.Subject.StartsWith("Re: ") ? mail.Subject : $"Re: {mail.Subject}";

            // Build body
            var builder = new BodyBuilder
            {
                TextBody = emailResponse.EmailResponseText
            };

            // Add attachments if any
            if (emailResponse.Attachments != null && emailResponse.Attachments.Length > 0)
            {
                foreach (var attachment in emailResponse.Attachments)
                {
                    var data = Convert.FromBase64String(attachment.Content);
                    builder.Attachments.Add(attachment.Filename, data, MimeKit.ContentType.Parse(attachment.ContentType));
                }
            }

            message.Body = builder.ToMessageBody();

            // Add In-Reply-To and References headers for threading
            if (!string.IsNullOrEmpty(mail.Id))
            {
                message.InReplyTo = mail.Id;
                message.References.Add(mail.Id);
            }

            // Send via SMTP
            using var smtp = new SmtpClient();
            smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Use SMTP server (usually same domain but smtp. prefix)
            var smtpServer = agent.Emailserver?.Replace("imap.", "smtp.") ?? throw new InvalidOperationException("Email server not configured");
            var smtpPort = 587; // Standard SMTP TLS port
            var useSsl = agent.Emailusessl ?? true;

            await smtp.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);

            var username = agent.Emailusername ?? throw new InvalidOperationException("Email username not configured");
            var password = agent.Emailpassword ?? throw new InvalidOperationException("Email password not configured");
            await smtp.AuthenticateAsync(username, password);

            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            Logger.Log($"[IMAP/SMTP] Successfully sent reply to: {mail.From}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[IMAP/SMTP] Error sending reply: {ex.Message}");
            throw;
        }
    }
}
