using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Admintool.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Admintool.Controllers;

/// <summary>
/// Controller for handling Office 365 OAuth callback
/// </summary>
[Route("api/office365")]
[ApiController]
public class Office365AuthController : ControllerBase
{
    private readonly Office365AuthService _authService;
    private readonly IDbContextFactory<DefaultdbContext> _dbFactory;
    private readonly ILogger<Office365AuthController> _logger;

    public Office365AuthController(
        Office365AuthService authService,
        IDbContextFactory<DefaultdbContext> dbFactory,
        ILogger<Office365AuthController> logger)
    {
        _authService = authService;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Initiates the Office 365 OAuth flow by redirecting to Microsoft login
    /// </summary>
    [HttpGet("login/{agentId}")]
    public IActionResult Login(int agentId)
    {
        try
        {
            var authUrl = _authService.GetAuthorizationUrl(agentId);
            return Redirect(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating Office 365 login for agent {AgentId}", agentId);
            return Redirect($"/agents/edit/{agentId}?error=auth_init_failed");
        }
    }

    /// <summary>
    /// OAuth callback endpoint - receives the authorization code from Microsoft
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription)
    {
        // Handle error response from Microsoft
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("Office 365 OAuth error: {Error} - {Description}", error, errorDescription);
            
            if (int.TryParse(state, out var errorAgentId))
            {
                return Redirect($"/agents/edit/{errorAgentId}?error={Uri.EscapeDataString(error)}");
            }
            return Redirect("/clients/manage?error=oauth_failed");
        }

        // Validate required parameters
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            _logger.LogWarning("Office 365 OAuth callback missing code or state");
            return Redirect("/clients/manage?error=missing_parameters");
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var result = await _authService.ExchangeCodeForTokensAsync(code, state, db);

            if (result.Success)
            {
                _logger.LogInformation("Office 365 connected successfully for agent {AgentId}, user {Email}", 
                    result.AgentId, result.UserEmail);
                return Redirect($"/agents/edit/{result.AgentId}?office365=connected");
            }
            else
            {
                _logger.LogError("Office 365 token exchange failed: {Error}", result.Error);
                
                if (int.TryParse(state, out var agentId))
                {
                    return Redirect($"/agents/edit/{agentId}?error={Uri.EscapeDataString(result.Error ?? "unknown_error")}");
                }
                return Redirect("/clients/manage?error=token_exchange_failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during Office 365 callback processing");
            
            if (int.TryParse(state, out var agentId))
            {
                return Redirect($"/agents/edit/{agentId}?error=callback_exception");
            }
            return Redirect("/clients/manage?error=callback_exception");
        }
    }

    /// <summary>
    /// Disconnects Office 365 from an agent
    /// </summary>
    [HttpPost("disconnect/{agentId}")]
    public async Task<IActionResult> Disconnect(int agentId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            await _authService.DisconnectAsync(agentId, db);
            
            _logger.LogInformation("Office 365 disconnected for agent {AgentId}", agentId);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting Office 365 for agent {AgentId}", agentId);
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
