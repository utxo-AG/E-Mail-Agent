using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Interfaces;

public interface IEmailProvider
{
    public Task<ListNewEmailsClass[]?> GetEmailsAsync(Agent agent);
    public Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent);
    public Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent, Conversation? conversation);

    /// <summary>
    /// Redirects/forwards the original email with full content to specified recipients.
    /// </summary>
    /// <param name="mail">The original email with full content</param>
    /// <param name="agent">The agent performing the redirect</param>
    /// <param name="to">Array of recipient email addresses</param>
    /// <param name="cc">Optional array of CC recipients</param>
    /// <param name="message">Optional message to prepend to the forwarded email</param>
    public Task RedirectEmail(MailClass mail, Agent agent, string[] to, string[]? cc = null, string? message = null);

    /// <summary>
    /// Marks an email as unread again (e.g. after a processing error).
    /// Not all providers support this - default implementation does nothing.
    /// </summary>
    public Task MarkAsUnreadAsync(ListNewEmailsClass email, Agent agent) => Task.CompletedTask;
}