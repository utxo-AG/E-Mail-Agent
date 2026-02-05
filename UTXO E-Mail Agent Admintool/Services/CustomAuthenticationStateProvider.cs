using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace UTXO_E_Mail_Agent_Admintool.Services;

public class CustomAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    public CustomAuthenticationStateProvider(ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
    }

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // Return whether the principal should be accepted as valid
        return Task.FromResult(authenticationState.User.Identity?.IsAuthenticated ?? false);
    }
}
