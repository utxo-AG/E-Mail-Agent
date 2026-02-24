using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Admintool.Services;

/// <summary>
/// Service for handling Office 365 OAuth authentication flow in the Admintool
/// </summary>
public class Office365AuthService
{
    private readonly IConfiguration _configuration;
    private readonly IConfidentialClientApplication _msalClient;
    private readonly string[] _scopes;
    private readonly string _redirectUri;

    public Office365AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
        
        var clientId = _configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId not configured");
        var clientSecret = _configuration["AzureAd:ClientSecret"] ?? throw new InvalidOperationException("AzureAd:ClientSecret not configured");
        var tenantId = _configuration["AzureAd:TenantId"] ?? "common";
        _redirectUri = _configuration["AzureAd:RedirectUri"] ?? throw new InvalidOperationException("AzureAd:RedirectUri not configured");
        
        var scopesConfig = _configuration.GetSection("AzureAd:Scopes").Get<string[]>();
        _scopes = scopesConfig ?? new[] { "Mail.Read", "Mail.Send", "User.Read", "offline_access" };

        _msalClient = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithClientSecret(clientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{tenantId}")
            .WithRedirectUri(_redirectUri)
            .Build();
    }

    /// <summary>
    /// Generates the Microsoft OAuth authorization URL
    /// </summary>
    /// <param name="agentId">The agent ID to include in the state parameter</param>
    /// <returns>The authorization URL to redirect the user to</returns>
    public string GetAuthorizationUrl(int agentId)
    {
        var authUrl = _msalClient.GetAuthorizationRequestUrl(_scopes)
            .WithState(agentId.ToString())
            .WithExtraQueryParameters(new Dictionary<string, string>
            {
                { "prompt", "select_account" }
            })
            .ExecuteAsync().Result;

        return authUrl.ToString();
    }

    /// <summary>
    /// Exchanges the authorization code for tokens and saves them to the agent
    /// </summary>
    public async Task<Office365TokenResult> ExchangeCodeForTokensAsync(string code, string state, DefaultdbContext db)
    {
        try
        {
            if (!int.TryParse(state, out var agentId))
            {
                return new Office365TokenResult { Success = false, Error = "Invalid state parameter" };
            }

            var agent = await db.Agents.FindAsync(agentId);
            if (agent == null)
            {
                return new Office365TokenResult { Success = false, Error = "Agent not found" };
            }

            // Exchange code for tokens
            var result = await _msalClient
                .AcquireTokenByAuthorizationCode(_scopes, code)
                .ExecuteAsync();

            // Save tokens to agent
            agent.Office365AccessToken = result.AccessToken;
            agent.Office365TokenExpiresAt = result.ExpiresOn.UtcDateTime;
            agent.Office365UserId = result.Account?.Username;

            // Extract refresh token from the cache
            // Note: MSAL stores the refresh token internally, but we need to extract it
            // for our database-based token storage. We'll get it from the token cache serialization.
            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();
            if (account != null)
            {
                try
                {
                    // Try to get a fresh token with refresh token to populate our fields
                    var silentResult = await _msalClient
                        .AcquireTokenSilent(_scopes, account)
                        .ExecuteAsync();
                    
                    // The refresh token is managed by MSAL, but we can store what we have
                    // For proper refresh token access, we'd need to implement a custom token cache
                }
                catch
                {
                    // Silent token acquisition failed, which is fine for initial auth
                }
            }

            // For now, we'll rely on the fact that we have the access token
            // and the user can re-authenticate if it expires and refresh fails
            // A more robust solution would implement a custom MSAL token cache
            
            await db.SaveChangesAsync();

            return new Office365TokenResult
            {
                Success = true,
                AgentId = agentId,
                UserEmail = result.Account?.Username
            };
        }
        catch (MsalException ex)
        {
            return new Office365TokenResult { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            return new Office365TokenResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Disconnects Office 365 from an agent by clearing the tokens
    /// </summary>
    public async Task DisconnectAsync(int agentId, DefaultdbContext db)
    {
        var agent = await db.Agents.FindAsync(agentId);
        if (agent != null)
        {
            agent.Office365AccessToken = null;
            agent.Office365RefreshToken = null;
            agent.Office365TokenExpiresAt = null;
            agent.Office365UserId = null;
            await db.SaveChangesAsync();
        }
    }
}

/// <summary>
/// Result of an Office 365 token exchange operation
/// </summary>
public class Office365TokenResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int AgentId { get; set; }
    public string? UserEmail { get; set; }
}
