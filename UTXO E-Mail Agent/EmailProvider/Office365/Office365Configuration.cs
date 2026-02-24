namespace UTXO_E_Mail_Agent.EmailProvider.Office365;

/// <summary>
/// Configuration class for Azure AD / Microsoft Graph API settings
/// </summary>
public class Office365Configuration
{
    public string TenantId { get; set; } = "common";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = new[] { "Mail.Read", "Mail.Send", "User.Read", "offline_access" };
    
    /// <summary>
    /// The Microsoft Graph API endpoint
    /// </summary>
    public string GraphApiEndpoint { get; set; } = "https://graph.microsoft.com/v1.0";
    
    /// <summary>
    /// The Microsoft identity platform authorization endpoint
    /// </summary>
    public string AuthorityUrl => $"https://login.microsoftonline.com/{TenantId}";
}
