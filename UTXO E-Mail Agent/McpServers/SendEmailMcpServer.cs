using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.McpServers;

/// <summary>
/// MCP Server for sending emails via Inbound API
/// This tool is always available to agents for forwarding or sending new emails
/// </summary>
public class SendEmailMcpServer
{
    private readonly string _apiUrl;
    private readonly string _bearerToken;
    private readonly string _fromAddress;
    private readonly int _agentId;
    private static readonly HttpClient _httpClient = new();

    public SendEmailMcpServer(IConfiguration config, string fromAddress, int agentId)
    {
        _apiUrl = config["Email:ApiUrl"] ?? throw new InvalidOperationException("Email:ApiUrl not configured");
        _bearerToken = config["Email:BearerToken"] ?? throw new InvalidOperationException("Email:BearerToken not configured");
        _fromAddress = fromAddress;
        _agentId = agentId;
    }

    /// <summary>
    /// Sends an email via Inbound API
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="text">Plain text content of the email</param>
    /// <param name="html">HTML content of the email (optional)</param>
    /// <param name="replyTo">Reply-to address (optional) - use this when forwarding so replies go to the original sender</param>
    /// <returns>Result message indicating success or failure</returns>
    public async Task<string> SendEmailAsync(string to, string subject, string text, string? html = null, string? replyTo = null)
    {
        Logger.Log($"[SendEmail MCP] ========================================", _agentId);
        Logger.Log($"[SendEmail MCP] Sending email to: {to}", _agentId);
        Logger.Log($"[SendEmail MCP] Subject: {subject}", _agentId);
        Logger.Log($"[SendEmail MCP] From: {_fromAddress}", _agentId);
        if (!string.IsNullOrEmpty(replyTo))
            Logger.Log($"[SendEmail MCP] Reply-To: {replyTo}", _agentId);

        try
        {
            object emailPayload;
            if (!string.IsNullOrEmpty(replyTo))
            {
                emailPayload = new
                {
                    from = _fromAddress,
                    to = to,
                    subject = subject,
                    text = text,
                    html = html ?? $"<html><body>{System.Web.HttpUtility.HtmlEncode(text).Replace("\n", "<br/>")}</body></html>",
                    reply_to = replyTo
                };
            }
            else
            {
                emailPayload = new
                {
                    from = _fromAddress,
                    to = to,
                    subject = subject,
                    text = text,
                    html = html ?? $"<html><body>{System.Web.HttpUtility.HtmlEncode(text).Replace("\n", "<br/>")}</body></html>"
                };
            }

            var jsonPayload = JsonConvert.SerializeObject(emailPayload, Formatting.Indented);
            Logger.Log($"[SendEmail MCP] Payload: {jsonPayload}", _agentId);

            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl + "emails");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_bearerToken}");
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Logger.Log($"[SendEmail MCP] Success: {response.StatusCode}", _agentId);
                Logger.Log($"[SendEmail MCP] Response: {responseContent}", _agentId);
                Logger.Log($"[SendEmail MCP] ========================================", _agentId);
                return $"Email successfully sent to {to} with subject '{subject}'";
            }
            else
            {
                Logger.LogError($"[SendEmail MCP] Error: {response.StatusCode}", _agentId);
                Logger.LogError($"[SendEmail MCP] Response: {responseContent}", _agentId);
                Logger.LogError($"[SendEmail MCP] ========================================", _agentId);
                return $"ERROR: Failed to send email. Status: {response.StatusCode}, Response: {responseContent}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"[SendEmail MCP] Exception: {ex.Message}", _agentId);
            Logger.LogError($"[SendEmail MCP] ========================================", _agentId);
            return $"ERROR: {ex.Message}";
        }
    }

    /// <summary>
    /// Forwards an email to another recipient
    /// </summary>
    /// <param name="to">Recipient email address to forward to</param>
    /// <param name="originalSubject">Original email subject</param>
    /// <param name="originalFrom">Original sender</param>
    /// <param name="originalText">Original email content</param>
    /// <param name="additionalMessage">Optional message to add before the forwarded content</param>
    /// <returns>Result message indicating success or failure</returns>
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

/// <summary>
/// Parameter class for the send_email tool
/// </summary>
public class SendEmailParameters
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Html { get; set; }
}

/// <summary>
/// Parameter class for the forward_email tool
/// </summary>
public class ForwardEmailParameters
{
    public string To { get; set; } = string.Empty;
    public string OriginalSubject { get; set; } = string.Empty;
    public string OriginalFrom { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public string? AdditionalMessage { get; set; }
}
