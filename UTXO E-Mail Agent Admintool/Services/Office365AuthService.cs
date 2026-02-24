using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent_Shared.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace UTXO_E_Mail_Agent_Admintool.Services;

/// <summary>
/// Service for handling Office 365 OAuth authentication flow in the Admintool
/// </summary>
public class Office365AuthService
{
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tenantId;
    private readonly string _redirectUri;
    private readonly string[] _scopes;
    private readonly HttpClient _httpClient;

    public Office365AuthService(IConfiguration configuration)
    {
        _clientId = configuration["AzureAd:ClientId"] ?? throw new InvalidOperationException("AzureAd:ClientId not configured");
        _clientSecret = configuration["AzureAd:ClientSecret"] ?? throw new InvalidOperationException("AzureAd:ClientSecret not configured");
        _tenantId = configuration["AzureAd:TenantId"] ?? "common";
        _redirectUri = configuration["AzureAd:RedirectUri"] ?? throw new InvalidOperationException("AzureAd:RedirectUri not configured");
        
        var scopesConfig = configuration.GetSection("AzureAd:Scopes").Get<string[]>();
        _scopes = scopesConfig ?? new[] { "Mail.Read", "Mail.Send", "User.Read", "offline_access" };
        
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Generates the Microsoft OAuth authorization URL
    /// </summary>
    /// <param name="agentId">The agent ID to include in the state parameter</param>
    /// <returns>The authorization URL to redirect the user to</returns>
    public string GetAuthorizationUrl(int agentId)
    {
        var scope = string.Join(" ", _scopes);
        var authUrl = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/authorize?" +
                      $"client_id={HttpUtility.UrlEncode(_clientId)}&" +
                      $"response_type=code&" +
                      $"redirect_uri={HttpUtility.UrlEncode(_redirectUri)}&" +
                      $"scope={HttpUtility.UrlEncode(scope)}&" +
                      $"state={agentId}&" +
                      $"prompt=select_account";

        return authUrl;
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

            // Exchange code for tokens via OAuth2 token endpoint
            var tokenEndpoint = $"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token";
            
            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", _clientId },
                { "client_secret", _clientSecret },
                { "code", code },
                { "redirect_uri", _redirectUri },
                { "grant_type", "authorization_code" },
                { "scope", string.Join(" ", _scopes) }
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, requestBody);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return new Office365TokenResult { Success = false, Error = $"Token exchange failed: {error}" };
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse == null)
            {
                return new Office365TokenResult { Success = false, Error = "Failed to parse token response" };
            }

            // Save tokens to agent
            agent.Office365AccessToken = tokenResponse.AccessToken;
            agent.Office365RefreshToken = tokenResponse.RefreshToken;
            agent.Office365TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            
            // Get user info from Microsoft Graph to get the email
            var userEmail = await GetUserEmailAsync(tokenResponse.AccessToken);
            agent.Office365UserId = userEmail;
            
            await db.SaveChangesAsync();

            return new Office365TokenResult
            {
                Success = true,
                AgentId = agentId,
                UserEmail = userEmail
            };
        }
        catch (Exception ex)
        {
            return new Office365TokenResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Gets the user's email address from Microsoft Graph
    /// </summary>
    private async Task<string?> GetUserEmailAsync(string accessToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<GraphUser>();
                return user?.Mail ?? user?.UserPrincipalName;
            }
        }
        catch
        {
            // Ignore errors getting user info
        }
        return null;
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

/// <summary>
/// OAuth2 token response
/// </summary>
internal class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
}

/// <summary>
/// Microsoft Graph user response
/// </summary>
internal class GraphUser
{
    [JsonPropertyName("mail")]
    public string? Mail { get; set; }
    
    [JsonPropertyName("userPrincipalName")]
    public string? UserPrincipalName { get; set; }
}
