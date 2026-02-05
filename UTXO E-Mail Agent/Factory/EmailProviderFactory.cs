using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.EmailProvider.Exchange;
using UTXO_E_Mail_Agent.EmailProvider.Imap;
using UTXO_E_Mail_Agent.EmailProvider.Inbound;
using UTXO_E_Mail_Agent.EmailProvider.Pop3;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Factory;

public static class EmailProviderFactory
{
    public static IEmailProvider? GetProvider(string providerType, IConfiguration config, DefaultdbContext db)
    {
        return providerType.ToLower() switch
        {
            "inbound" => new InboundClass(config, db),
            "imap" => new ImapClass(config, db),
            "pop3" => new Pop3Class(config,db ),
            "exchange" => new ExchangeClass(config,db),
            _ => null
        };
    }
}
