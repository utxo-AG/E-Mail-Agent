namespace UTXO_E_Mail_Agent.Interfaces;

/// <summary>
/// Interface for sending emails via different providers (Inbound API, SMTP, Exchange, etc.)
/// </summary>
public interface ISendEmailMcpServer
{
    /// <summary>
    /// Sends an email to a recipient
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="text">Plain text content of the email</param>
    /// <param name="html">HTML content of the email (optional)</param>
    /// <param name="replyTo">Reply-to address (optional) - use this when forwarding so replies go to the original sender</param>
    /// <returns>Result message indicating success or failure</returns>
    Task<string> SendEmailAsync(string to, string subject, string text, string? html = null, string? replyTo = null);

    /// <summary>
    /// Forwards an email to another recipient
    /// </summary>
    /// <param name="to">Recipient email address to forward to</param>
    /// <param name="originalSubject">Original email subject</param>
    /// <param name="originalFrom">Original sender</param>
    /// <param name="originalText">Original email content</param>
    /// <param name="additionalMessage">Optional message to add before the forwarded content</param>
    /// <returns>Result message indicating success or failure</returns>
    Task<string> ForwardEmailAsync(string to, string originalSubject, string originalFrom, string originalText, string? additionalMessage = null);
}
