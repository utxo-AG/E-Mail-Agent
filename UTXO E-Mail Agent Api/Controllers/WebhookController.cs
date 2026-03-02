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

    public WebhookController(DefaultdbContext db, IConfiguration configuration, ILogger<WebhookController> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Receive inbound email via webhook (not publicly documented)
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

        // Check if email was already processed (by MessageId)
        var messageId = dto.Id ?? Guid.NewGuid().ToString();
        var existingConversation = await _db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Messageid == messageId && c.AgentId == agentId);

        if (existingConversation != null)
        {
            _logger.LogInformation("Email {MessageId} already processed for agent {AgentId}, skipping", messageId, agentId);
            return Ok(new { message = "Email already processed", conversationId = existingConversation.Id });
        }

        // Create conversation entry (claiming the email)
        var conversation = new Conversation
        {
            AgentId = agentId,
            Messageid = messageId,
            Emailfrom = dto.From ?? "(Unknown)",
            Subject = dto.Subject ?? "(No Subject)",
            Text = dto.Text,
            Htmltext = dto.Html,
            Emailreceived = dto.CreatedAt ?? DateTime.UtcNow
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Webhook received email for agent {AgentId}: {Subject} from {From}", 
            agentId, dto.Subject, dto.From);

        // Return conversation ID for tracking
        return Ok(new 
        { 
            message = "Email received", 
            conversationId = conversation.Id,
            status = "pending_processing"
        });
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
