using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace UTXO_E_Mail_Agent_Admintool.Services;

public class CustomAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private readonly DateTime _circuitStarted = DateTime.UtcNow;
    private DateTime _lastActivity = DateTime.UtcNow;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(30);

    public CustomAuthenticationStateProvider(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    // Check every minute if session is still valid
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        if (!(authenticationState.User.Identity?.IsAuthenticated ?? false))
            return Task.FromResult(false);

        // Check if session has exceeded the timeout (matches cookie expiration)
        if (DateTime.UtcNow - _circuitStarted > SessionTimeout)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
