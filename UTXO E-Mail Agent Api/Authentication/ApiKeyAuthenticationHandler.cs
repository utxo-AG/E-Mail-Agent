using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly DefaultdbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DefaultdbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for Authorization header
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.Fail("Missing Authorization header");
        }

        var headerValue = authHeader.ToString();
        
        // Support both "Bearer <token>" and just "<token>"
        string apiKey;
        if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            apiKey = headerValue.Substring(7).Trim();
        }
        else
        {
            apiKey = headerValue.Trim();
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            return AuthenticateResult.Fail("Invalid API key format");
        }

        // Validate API key against database
        var apiKeyRecord = await _db.Apikeys
            .Where(k => k.Apikey1 == apiKey)
            .FirstOrDefaultAsync();

        if (apiKeyRecord == null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        // Check if API key is active
        if (apiKeyRecord.State != "active")
        {
            return AuthenticateResult.Fail("API key is not active");
        }

        // Check if API key is expired
        if (apiKeyRecord.Expires.HasValue && apiKeyRecord.Expires.Value < DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("API key has expired");
        }

        // Create claims
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKeyRecord.CustomerId.ToString()),
            new Claim("CustomerId", apiKeyRecord.CustomerId.ToString()),
            new Claim("ApiKeyId", apiKeyRecord.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
