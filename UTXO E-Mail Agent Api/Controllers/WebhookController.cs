using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Api.DTOs;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Api.Controllers;

/// <summary>
/// Internal webhook controller for receiving inbound emails.
/// This controller is NOT documented in Swagger and requires a secret header for authentication.
/// </summary>
[ApiController]
[Route("webhook")]
[ApiExplorerSettings(IgnoreApi = true)] // Hide from Swagger
public class WebhookController : ControllerBase
{
    private readonly DefaultdbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebhookController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebhookController(DefaultdbContext db, IConfiguration configuration, ILogger<WebhookController> logger, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Receive inbound email via webhook (not publicly documented)
    /// Forwards the email to the agent's /api/processemail endpoint
    /// </summary>
    [HttpPost("inbound/{agentId}")]
    public async Task<ActionResult> ReceiveInboundEmail(int agentId, [FromBody] InboundEmailWebhookDto dto)
    {
        // Validate webhook secret
        var expectedSecret = _configuration["WebhookSecret"];
        if (!string.IsNullOrEmpty(expectedSecret))
        {
            if (!Request.Headers.TryGetValue("X-Webhook-Secret", out var secretHeader) ||
                secretHeader.ToString() != expectedSecret)
            {
                _logger.LogWarning("Webhook request with invalid or missing secret for agent {AgentId}", agentId);
                return Unauthorized(new { message = "Invalid webhook secret" });
            }
        }

        // Validate agent exists
        var agent = await _db.Agents
            .Include(a => a.Customer)
            .FirstOrDefaultAsync(a => a.Id == agentId);

        if (agent == null)
        {
            _logger.LogWarning("Webhook request for non-existent agent {AgentId}", agentId);
            return NotFound(new { message = "Agent not found" });
        }

        var messageId = dto.Id ?? Guid.NewGuid().ToString();
        
        _logger.LogInformation("Webhook received email for agent {AgentId}: {Subject} from {From}, forwarding to processemail", 
            agentId, dto.Subject, dto.From);

        // Forward to agent's /api/processemail endpoint
        var agentApiUrl = _configuration["AgentApiUrl"] ?? "http://localhost:5051";
        var processEmailUrl = $"{agentApiUrl.TrimEnd('/')}/api/processemail";

        // Build ProcessMailRequestClass-compatible payload
        var processEmailRequest = new
        {
            MessageId = messageId,
            AgentName = agent.Agentname,
            From = dto.From ?? "(Unknown)",
            To = new[] { agent.Email ?? "" },
            Subject = dto.Subject ?? "(No Subject)",
            Status = "unread",
            CreatedAt = (dto.CreatedAt ?? DateTime.UtcNow).ToString("o"),
            Html = dto.Html,
            Text = dto.Text,
            Attachments = dto.Attachments?.Select(a => a.Filename).Where(f => f != null).ToArray() ?? Array.Empty<string>(),
            HasAttachments = dto.Attachments?.Any() ?? false
        };

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var jsonContent = new StringContent(
                JsonSerializer.Serialize(processEmailRequest),
                Encoding.UTF8,
                "application/json"
            );

            var response = await httpClient.PostAsync(processEmailUrl, jsonContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully forwarded email {MessageId} to agent {AgentId} processemail endpoint", 
                    messageId, agentId);
                
                return Ok(new 
                { 
                    message = "Email forwarded for processing", 
                    messageId = messageId,
                    status = "queued"
                });
            }
            else
            {
                _logger.LogError("Failed to forward email {MessageId} to processemail: {StatusCode} - {Response}", 
                    messageId, response.StatusCode, responseContent);
                
                return StatusCode(502, new 
                { 
                    message = "Failed to forward email to agent", 
                    error = responseContent 
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while forwarding email {MessageId} to processemail", messageId);
            return StatusCode(500, new { message = "Error forwarding email", error = ex.Message });
        }
    }

    /// <summary>
    /// Health check for webhook endpoint
    /// </summary>
    [HttpGet("health")]
    public ActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
