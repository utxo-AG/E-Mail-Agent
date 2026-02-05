using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Interfaces;

public interface IEmailProvider
{
    public Task<ListNewEmailsClass[]?> GetEmailsAsync(Agent agent);
    public Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent);
    public Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent);
}