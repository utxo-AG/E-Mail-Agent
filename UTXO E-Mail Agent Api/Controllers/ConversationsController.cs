using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Api.DTOs;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    /// Get all conversations for the authenticated customer's agents
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ConversationListResponseDto>> GetConversations(
        [FromQuery] int? agentId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var customerId = GetCustomerId();
        
        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var query = _db.Conversations
            .Include(c => c.Agent)
            .Where(c => c.Agent.CustomerId == customerId);

        if (agentId.HasValue)
        {
            query = query.Where(c => c.AgentId == agentId.Value);
        }

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
    [HttpGet("{id}")]
    public async Task<ActionResult<ConversationResponseDto>> GetConversation(int id)
    {
        var customerId = GetCustomerId();

        var conversation = await _db.Conversations
            .Include(c => c.Agent)
            .Include(c => c.ConversationAttachments)
            .Where(c => c.Id == id && c.Agent.CustomerId == customerId)
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

    /// <summary>
    /// Get conversations for a specific agent
    /// </summary>
    [HttpGet("agent/{agentId}")]
    public async Task<ActionResult<ConversationListResponseDto>> GetConversationsByAgent(
        int agentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var customerId = GetCustomerId();

        // Verify agent belongs to customer
        var agentExists = await _db.Agents
            .AnyAsync(a => a.Id == agentId && a.CustomerId == customerId);

        if (!agentExists)
        {
            return NotFound(new { message = "Agent not found" });
        }

        if (pageSize > 100) pageSize = 100;
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
}
