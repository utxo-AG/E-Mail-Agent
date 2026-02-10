using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent.AiProvider.Claude;
using UTXO_E_Mail_Agent.Interfaces;

namespace UTXO_E_Mail_Agent.Factory;

public static class AiProviderFactory
{
    public static IAiProvider? GetProvider(string providerType, IConfiguration configuration)
    {
        return providerType.ToLower() switch
        {
            "claude" => new ClaudeClass(
                configuration["Claude:ApiKey"]
                    ?? throw new InvalidOperationException("Claude:ApiKey not configured in appsettings.json."),
                configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.")),
            _ => null
        };
    }
}
