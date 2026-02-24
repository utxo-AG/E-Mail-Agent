using Microsoft.Extensions.Configuration;
using UTXO_E_Mail_Agent_Shared.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace UTXO_E_Mail_Agent.EmailProvider.Office365;

/// <summary>
/// Service for managing Office 365 OAuth tokens including automatic refresh
/// </summary>
public class Office365TokenService
{
    private readonly Office365Configuration _config;
    private readonly DefaultdbContext _db;
    private readonly HttpClient _httpClient;

    public Office365TokenService(IConfiguration configuration, DefaultdbContext db)
    {
        _db = db;
        _config = new Office365Configuration();
        configuration.GetSection("AzureAd").Bind(_config);
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Gets a valid access token for the agent, refreshing if necessary
    /// </summary>
    public async Task<string?> GetValidTokenAsync(Agent agent)
    {
        // Check if we have an access token
        if (string.IsNullOrEmpty(agent.Office365AccessToken))
        {
            // Try to refresh if we have a refresh token
            if (!string.IsNullOrEmpty(agent.Office365RefreshToken))
            {
                return await RefreshTokenAsync(agent);
            }
            return null;
        }

        // Check if token is still valid (with 5 minute buffer)
        if (agent.Office365TokenExpiresAt.HasValue && 
            agent.Office365TokenExpiresAt.Value > DateTime.UtcNow.AddMinutes(5))
        {
            return agent.Office365AccessToken;
        }

        // Token expired or expiring soon, try to refresh
        if (!string.IsNullOrEmpty(agent.Office365RefreshToken))
        {
            return await RefreshTokenAsync(agent);
        }

        return null;
    }

    /// <summary>
    /// Refreshes the access token using the refresh token via OAuth2 token endpoint
    /// </summary>
    private async Task<string?> RefreshTokenAsync(Agent agent)
    {
        try
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{_config.TenantId}/oauth2/v2.0/token";
            
            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", _config.ClientId },
                { "client_secret", _config.ClientSecret },
                { "refresh_token", agent.Office365RefreshToken! },
                { "grant_type", "refresh_token" },
                { "scope", string.Join(" ", _config.Scopes) }
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, requestBody);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Office365TokenService] Token refresh failed: {error}");
                
                // Clear invalid tokens
                agent.Office365AccessToken = null;
                agent.Office365RefreshToken = null;
                agent.Office365TokenExpiresAt = null;
                await _db.SaveChangesAsync();
                
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse == null)
            {
                return null;
            }

            // Update agent with new tokens
            agent.Office365AccessToken = tokenResponse.AccessToken;
            agent.Office365TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            
            // Update refresh token if a new one was provided
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                agent.Office365RefreshToken = tokenResponse.RefreshToken;
            }
            
            await _db.SaveChangesAsync();

            return tokenResponse.AccessToken;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Office365TokenService] Token refresh exception: {ex.Message}");
            
            // Clear invalid tokens
            agent.Office365AccessToken = null;
            agent.Office365RefreshToken = null;
            agent.Office365TokenExpiresAt = null;
            await _db.SaveChangesAsync();
            
            return null;
        }
    }

    /// <summary>
    /// Exchanges an authorization code for tokens via OAuth2 token endpoint
    /// </summary>
    public async Task<TokenResponse?> ExchangeCodeForTokensAsync(string authorizationCode, string redirectUri)
    {
        try
        {
            var tokenEndpoint = $"https://login.microsoftonline.com/{_config.TenantId}/oauth2/v2.0/token";
            
            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", _config.ClientId },
                { "client_secret", _config.ClientSecret },
                { "code", authorizationCode },
                { "redirect_uri", redirectUri },
                { "grant_type", "authorization_code" },
                { "scope", string.Join(" ", _config.Scopes) }
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, requestBody);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Office365TokenService] Code exchange failed: {error}");
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TokenResponse>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Office365TokenService] Code exchange exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Saves OAuth tokens to the agent entity
    /// </summary>
    public async Task SaveTokensAsync(Agent agent, TokenResponse tokenResponse)
    {
        agent.Office365AccessToken = tokenResponse.AccessToken;
        agent.Office365RefreshToken = tokenResponse.RefreshToken;
        agent.Office365TokenExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// OAuth2 token response
/// </summary>
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
    
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
