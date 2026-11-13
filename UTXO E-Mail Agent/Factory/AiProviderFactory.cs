using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.AiProvider.Claude;
using UTXO_E_Mail_Agent.AiProvider.ClaudeCode;
using UTXO_E_Mail_Agent.AiProvider.RstCloud;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Factory;

public static class AiProviderFactory
{
    public static IAiProvider? GetProvider(Agent agent, IConfiguration configuration)
    {
        return agent.Aiprovider?.ToLower() switch
        {
            "claude" or "anthropic" => new ClaudeClass(
                configuration["Claude:ApiKey"]
                    ?? throw new InvalidOperationException("Claude:ApiKey not configured in appsettings.json."),
                DatabaseConfig.ResolveConnectionString(
                    configuration.GetConnectionString("DefaultConnection")),
                configuration),
            
            "claudecode" or "claude-code" => new ClaudeCodeClass(configuration),

            "rstcloud" => new RstCloudClass(configuration),

            _ => null
        };
    }
}
