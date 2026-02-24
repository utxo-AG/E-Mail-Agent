using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.EmailProvider.Office365;

/// <summary>
/// Service for managing Office 365 OAuth tokens including automatic refresh
/// </summary>
public class Office365TokenService
{
    private readonly Office365Configuration _config;
    private readonly DefaultdbContext _db;
    private readonly IConfidentialClientApplication _msalClient;

    public Office365TokenService(IConfiguration configuration, DefaultdbContext db)
    {
        _db = db;
        _config = new Office365Configuration();
        configuration.GetSection("AzureAd").Bind(_config);

        _msalClient = ConfidentialClientApplicationBuilder
            .Create(_config.ClientId)
            .WithClientSecret(_config.ClientSecret)
            .WithAuthority(_config.AuthorityUrl)
            .Build();
    }

    /// <summary>
    /// Gets a valid access token for the agent, refreshing if necessary
    /// </summary>
    public async Task<string?> GetValidTokenAsync(Agent agent)
    {
        if (string.IsNullOrEmpty(agent.Office365RefreshToken))
        {
            return null;
        }

        // Check if token is still valid (with 5 minute buffer)
        if (agent.Office365TokenExpiresAt.HasValue && 
            agent.Office365TokenExpiresAt.Value > DateTime.UtcNow.AddMinutes(5) &&
            !string.IsNullOrEmpty(agent.Office365AccessToken))
        {
            return agent.Office365AccessToken;
        }

        // Token expired or expiring soon, refresh it
        return await RefreshTokenAsync(agent);
    }

    /// <summary>
    /// Refreshes the access token using the refresh token
    /// </summary>
    private async Task<string?> RefreshTokenAsync(Agent agent)
    {
        try
        {
            var result = await _msalClient
                .AcquireTokenByRefreshToken(_config.Scopes, agent.Office365RefreshToken)
                .ExecuteAsync();

            // Update agent with new tokens
            agent.Office365AccessToken = result.AccessToken;
            agent.Office365TokenExpiresAt = result.ExpiresOn.UtcDateTime;
            
            // Note: Microsoft may return a new refresh token
            // The refresh token rotation is handled by MSAL internally
            
            await _db.SaveChangesAsync();

            return result.AccessToken;
        }
        catch (MsalException ex)
        {
            Console.WriteLine($"[Office365TokenService] Token refresh failed: {ex.Message}");
            
            // Clear invalid tokens
            agent.Office365AccessToken = null;
            agent.Office365RefreshToken = null;
            agent.Office365TokenExpiresAt = null;
            await _db.SaveChangesAsync();
            
            return null;
        }
    }

    /// <summary>
    /// Exchanges an authorization code for tokens (used during initial OAuth flow)
    /// </summary>
    public async Task<AuthenticationResult?> ExchangeCodeForTokensAsync(string authorizationCode, string redirectUri)
    {
        try
        {
            var result = await _msalClient
                .AcquireTokenByAuthorizationCode(_config.Scopes, authorizationCode)
                .WithRedirectUri(redirectUri)
                .ExecuteAsync();

            return result;
        }
        catch (MsalException ex)
        {
            Console.WriteLine($"[Office365TokenService] Code exchange failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves OAuth tokens to the agent entity
    /// </summary>
    public async Task SaveTokensAsync(Agent agent, AuthenticationResult result)
    {
        agent.Office365AccessToken = result.AccessToken;
        agent.Office365TokenExpiresAt = result.ExpiresOn.UtcDateTime;
        agent.Office365UserId = result.Account?.Username;
        
        // Get refresh token from the token cache
        // Note: MSAL handles refresh token storage internally, but we need to extract it
        // for our database-based token storage approach
        
        await _db.SaveChangesAsync();
    }
}
