using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Api.DTOs;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Api.Controllers;

[ApiController]
[Route("api/agents/{agentId}/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly DefaultdbContext _db;

    public ConversationsController(DefaultdbContext db)
    {
        _db = db;
    }

    private int GetCustomerId()
    {
        var claim = User.FindFirst("CustomerId");
        return claim != null ? int.Parse(claim.Value) : 0;
    }

    /// <summary>
    /// Verify that the agent belongs to the authenticated customer
    /// </summary>
    private async Task<Agent?> GetAgentIfAuthorized(int agentId)
    {
        var customerId = GetCustomerId();
        return await _db.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId && a.CustomerId == customerId);
    }

    /// <summary>
    /// Get all conversations for a specific agent (paginated)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ConversationListResponseDto>> GetConversations(
        int agentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var agent = await GetAgentIfAuthorized(agentId);
        if (agent == null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;
        if (page < 1) page = 1;

        var query = _db.Conversations
            .Where(c => c.AgentId == agentId);

        var totalCount = await query.CountAsync();

        var conversations = await query
            .OrderByDescending(c => c.Emailreceived)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationResponseDto
            {
                Id = c.Id,
                AgentId = c.AgentId,
                Messageid = c.Messageid,
                Emailfrom = c.Emailfrom,
                Subject = c.Subject,
                Text = c.Text,
                Htmltext = c.Htmltext,
                Agentresponsetext = c.Agentresponsetext,
                Agentresponsehtml = c.Agentresponsehtml,
                Agentresponsesubject = c.Agentresponsesubject,
                Emailreceived = c.Emailreceived,
                Aiexplanation = c.Aiexplanation,
                Attachments = c.ConversationAttachments.Select(a => new ConversationAttachmentDto
                {
                    Id = a.Id,
                    Filename = a.Filename,
                    ContentType = a.ContentType
                }).ToList()
            })
            .ToListAsync();

        return Ok(new ConversationListResponseDto
        {
            Conversations = conversations,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get conversations between a specific agent and email address (paginated)
    /// </summary>
    [HttpGet("email/{email}")]
    public async Task<ActionResult<ConversationListResponseDto>> GetConversationsByEmail(
        int agentId,
        string email,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var agent = await GetAgentIfAuthorized(agentId);
        if (agent == null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        if (pageSize > 100) pageSize = 100;
        if (pageSize < 1) pageSize = 20;
        if (page < 1) page = 1;

        // Decode email (in case it's URL encoded)
        var decodedEmail = Uri.UnescapeDataString(email);

        var query = _db.Conversations
            .Where(c => c.AgentId == agentId && c.Emailfrom == decodedEmail);

        var totalCount = await query.CountAsync();

        var conversations = await query
            .OrderByDescending(c => c.Emailreceived)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConversationResponseDto
            {
                Id = c.Id,
                AgentId = c.AgentId,
                Messageid = c.Messageid,
                Emailfrom = c.Emailfrom,
                Subject = c.Subject,
                Text = c.Text,
                Htmltext = c.Htmltext,
                Agentresponsetext = c.Agentresponsetext,
                Agentresponsehtml = c.Agentresponsehtml,
                Agentresponsesubject = c.Agentresponsesubject,
                Emailreceived = c.Emailreceived,
                Aiexplanation = c.Aiexplanation,
                Attachments = c.ConversationAttachments.Select(a => new ConversationAttachmentDto
                {
                    Id = a.Id,
                    Filename = a.Filename,
                    ContentType = a.ContentType
                }).ToList()
            })
            .ToListAsync();

        return Ok(new ConversationListResponseDto
        {
            Conversations = conversations,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Get a specific conversation by ID
    /// </summary>
    [HttpGet("{conversationId}")]
    public async Task<ActionResult<ConversationResponseDto>> GetConversation(int agentId, int conversationId)
    {
        var agent = await GetAgentIfAuthorized(agentId);
        if (agent == null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        var conversation = await _db.Conversations
            .Include(c => c.ConversationAttachments)
            .Where(c => c.Id == conversationId && c.AgentId == agentId)
            .Select(c => new ConversationResponseDto
            {
                Id = c.Id,
                AgentId = c.AgentId,
                Messageid = c.Messageid,
                Emailfrom = c.Emailfrom,
                Subject = c.Subject,
                Text = c.Text,
                Htmltext = c.Htmltext,
                Agentresponsetext = c.Agentresponsetext,
                Agentresponsehtml = c.Agentresponsehtml,
                Agentresponsesubject = c.Agentresponsesubject,
                Emailreceived = c.Emailreceived,
                Aiexplanation = c.Aiexplanation,
                Attachments = c.ConversationAttachments.Select(a => new ConversationAttachmentDto
                {
                    Id = a.Id,
                    Filename = a.Filename,
                    ContentType = a.ContentType
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (conversation == null)
        {
            return NotFound(new { message = "Conversation not found" });
        }

        return Ok(conversation);
    }
}
