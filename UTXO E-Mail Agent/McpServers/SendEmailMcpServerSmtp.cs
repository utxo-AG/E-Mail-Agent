using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.McpServers;

/// <summary>
/// MCP Server for sending emails via SMTP
/// Used when agent's email provider is "imap" or "pop3"
/// </summary>
public class SendEmailMcpServerSmtp : ISendEmailMcpServer
{
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly bool _useSsl;
    private readonly string _fromAddress;
    private readonly int _agentId;

    public SendEmailMcpServerSmtp(Agent agent)
    {
        _smtpServer = agent.Smtpserver ?? throw new InvalidOperationException("SMTP server not configured for this agent");
        _smtpPort = agent.Smtpport ?? 587;
        _smtpUsername = agent.Smtpusername ?? throw new InvalidOperationException("SMTP username not configured for this agent");
        _smtpPassword = agent.Smtppassword ?? throw new InvalidOperationException("SMTP password not configured for this agent");
        _useSsl = agent.Smtpusessl ?? true;
        _fromAddress = agent.Emailaddress;
        _agentId = agent.Id;
    }

    public async Task<string> SendEmailAsync(string to, string subject, string text, string? html = null, string? replyTo = null)
    {
        Logger.Log($"[SendEmail SMTP] ========================================", _agentId);
        Logger.Log($"[SendEmail SMTP] Sending email to: {to}", _agentId);
        Logger.Log($"[SendEmail SMTP] Subject: {subject}", _agentId);
        Logger.Log($"[SendEmail SMTP] From: {_fromAddress}", _agentId);
        Logger.Log($"[SendEmail SMTP] Server: {_smtpServer}:{_smtpPort} (SSL: {_useSsl})", _agentId);
        if (!string.IsNullOrEmpty(replyTo))
            Logger.Log($"[SendEmail SMTP] Reply-To: {replyTo}", _agentId);

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_fromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            if (!string.IsNullOrEmpty(replyTo))
                message.ReplyTo.Add(MailboxAddress.Parse(replyTo));

            var htmlContent = html ?? $"<html><body>{System.Web.HttpUtility.HtmlEncode(text).Replace("\n", "<br/>")}</body></html>";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = text,
                HtmlBody = htmlContent
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_smtpServer, _smtpPort, _useSsl);
            await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Logger.Log($"[SendEmail SMTP] Success: Email sent to {to}", _agentId);
            Logger.Log($"[SendEmail SMTP] ========================================", _agentId);
            return $"Email successfully sent to {to} with subject '{subject}'";
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SendEmail SMTP] Exception: {ex.Message}", _agentId);
            Logger.LogError($"[SendEmail SMTP] ========================================", _agentId);
            return $"ERROR: {ex.Message}";
        }
    }

    public async Task<string> ForwardEmailAsync(string to, string originalSubject, string originalFrom, string originalText, string? additionalMessage = null)
    {
        var forwardSubject = originalSubject.StartsWith("Fwd:", StringComparison.OrdinalIgnoreCase) ||
                            originalSubject.StartsWith("FW:", StringComparison.OrdinalIgnoreCase)
            ? originalSubject
            : $"Fwd: {originalSubject}";

        var forwardText = new StringBuilder();
        if (!string.IsNullOrEmpty(additionalMessage))
        {
            forwardText.AppendLine(additionalMessage);
            forwardText.AppendLine();
            forwardText.AppendLine("---------- Forwarded message ----------");
        }
        else
        {
            forwardText.AppendLine("---------- Forwarded message ----------");
        }
        forwardText.AppendLine($"From: {originalFrom}");
        forwardText.AppendLine($"Subject: {originalSubject}");
        forwardText.AppendLine();
        forwardText.AppendLine(originalText);

        return await SendEmailAsync(to, forwardSubject, forwardText.ToString());
    }
}
