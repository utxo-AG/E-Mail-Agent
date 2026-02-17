using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.EmailProvider.Pop3;

public class Pop3Class : IEmailProvider
{
    private readonly IConfiguration _config;

    public Pop3Class(IConfiguration config, DefaultdbContext db)
    {
        _config = config;
    }

    public async Task<ListNewEmailsClass[]?> GetEmailsAsync(Agent agent)
    {
        try
        {
            using var client = new Pop3Client();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Connect to POP3 server
            var server = agent.Emailserver ?? throw new InvalidOperationException("Email server not configured");
            var useSsl = agent.Emailusessl ?? true;
            var port = agent.Emailport ?? (useSsl ? 995 : 110); // Default: 995 for SSL, 110 for non-SSL

            await client.ConnectAsync(server, port, useSsl);

            // Authenticate
            var username = agent.Emailusername ?? throw new InvalidOperationException("Email username not configured");
            var password = agent.Emailpassword ?? throw new InvalidOperationException("Email password not configured");
            await client.AuthenticateAsync(username, password);

            var count = await client.GetMessageCountAsync();

            if (count == 0)
            {
                await client.DisconnectAsync(true);
                return Array.Empty<ListNewEmailsClass>();
            }

            // Fetch basic info for all messages
            var messages = new List<ListNewEmailsClass>();

            for (int i = 0; i < count; i++)
            {
                var message = await client.GetMessageAsync(i);

                messages.Add(new ListNewEmailsClass
                {
                    Id = i.ToString(),
                    Type = "received",
                    From = message.From.Mailboxes.FirstOrDefault()?.Address ?? "unknown",
                    To = new[] { message.To.Mailboxes.FirstOrDefault()?.Address ?? agent.Emailaddress ?? "unknown" },
                    Subject = message.Subject ?? "(No Subject)",
                    Status = "unread",
                    CreatedAt = message.Date.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            await client.DisconnectAsync(true);

            Logger.Log($"[POP3] Found {messages.Count} email(s)");
            return messages.ToArray();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[POP3] Error fetching emails: {ex.Message}");
            throw;
        }
    }

    public async Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent)
    {
        try
        {
            using var client = new Pop3Client();
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

            // Connect to POP3 server
            var server = agent.Emailserver ?? throw new InvalidOperationException("Email server not configured");
            var useSsl = agent.Emailusessl ?? true;
            var port = agent.Emailport ?? (useSsl ? 995 : 110); // Default: 995 for SSL, 110 for non-SSL

            await client.ConnectAsync(server, port, useSsl);

            // Authenticate
            var username = agent.Emailusername ?? throw new InvalidOperationException("Email username not configured");
            var password = agent.Emailpassword ?? throw new InvalidOperationException("Email password not configured");
            await client.AuthenticateAsync(username, password);

            // Get message by index
            var index = int.Parse(email.Id);
            var message = await client.GetMessageAsync(index);

            // Extract text content
            var textBody = message.TextBody ?? message.HtmlBody ?? "";

            // Extract attachments - store as filenames
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

            // Note: POP3 doesn't support marking messages as read like IMAP
            // Messages will be deleted from server after retrieval if agent wants that behavior
            // For now, we leave them on the server

            await client.DisconnectAsync(true);

            Logger.Log($"[POP3] Successfully fetched email: {result.Subject}");
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[POP3] Error fetching email details: {ex.Message}");
            throw;
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

            // Send via SMTP using agent's SMTP settings
            using var smtp = new SmtpClient();
            smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var smtpServer = agent.Smtpserver ?? throw new InvalidOperationException("SMTP server not configured");
            var smtpPort = agent.Smtpport ?? 465;
            var useSsl = agent.Smtpusessl ?? true;

            await smtp.ConnectAsync(smtpServer, smtpPort, useSsl);

            var username = agent.Smtpusername ?? throw new InvalidOperationException("SMTP username not configured");
            var password = agent.Smtppassword ?? throw new InvalidOperationException("SMTP password not configured");
            await smtp.AuthenticateAsync(username, password);

            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            Logger.Log($"[POP3/SMTP] Successfully sent reply to: {mail.From}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"[POP3/SMTP] Error sending reply: {ex.Message}");
            throw;
        }
    }
}
