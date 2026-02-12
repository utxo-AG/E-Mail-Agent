using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.AiProvider.Claude;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Factory;

public static class AiProviderFactory
{
    public static IAiProvider? GetProvider(Agent agent, IConfiguration configuration)
    {
        return agent.Aiprovider?.ToLower() switch
        {
            "claude" => new ClaudeClass(
                configuration["Claude:ApiKey"]
                    ?? throw new InvalidOperationException("Claude:ApiKey not configured in appsettings.json."),
                configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json."),
                configuration),
            _ => null
        };
    }
}
