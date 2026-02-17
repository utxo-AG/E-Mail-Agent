using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.EmailProvider.Exchange;

public class ExchangeClass : IEmailProvider
{
    private readonly IConfiguration _config;

    public ExchangeClass(IConfiguration config, DefaultdbContext db)
    {
        _config = config;
    }

    public Task<ListNewEmailsClass[]?> GetEmailsAsync(Agent agent)
    {
        throw new NotImplementedException();
    }

    public Task<MailClass?> GetMail(ListNewEmailsClass email, Agent agent)
    {
        throw new NotImplementedException();
    }

    public async Task SendReplyResponseEmail(AiResponseClass emailResponse, MailClass mail, Agent agent, Conversation? conversation)
    {
        throw new NotImplementedException();
    }

}