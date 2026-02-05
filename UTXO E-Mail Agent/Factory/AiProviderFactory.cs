using UTXO_E_Mail_Agent.AiProvider.Claude;
using UTXO_E_Mail_Agent.Interfaces;

namespace UTXO_E_Mail_Agent.Factory;

public static class AiProviderFactory
{
    public static IAiProvider? GetProvider(string providerType, string connectionString)
    {
        return providerType.ToLower() switch
        {
            "claude" => new ClaudeClass(connectionString),
            _ => null
        };
    }
}
