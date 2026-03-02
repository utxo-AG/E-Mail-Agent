using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Api.DTOs;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly DefaultdbContext _db;

    public AgentsController(DefaultdbContext db)
    {
        _db = db;
    }

    private int GetCustomerId()
    {
        var claim = User.FindFirst("CustomerId");
        return claim != null ? int.Parse(claim.Value) : 0;
    }

    /// <summary>
    /// Get all agents for the authenticated customer
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AgentResponseDto>>> GetAgents()
    {
        var customerId = GetCustomerId();
        
        var agents = await _db.Agents
            .Where(a => a.CustomerId == customerId)
            .Select(a => new AgentResponseDto
            {
                Id = a.Id,
                Emailaddress = a.Emailaddress,
                State = a.State,
                Defaultlanguage = a.Defaultlanguage,
                Emailprovider = a.Emailprovider,
                Emailusername = a.Emailusername,
                Emailserver = a.Emailserver,
                Emailport = a.Emailport,
                Emailusessl = a.Emailusessl,
                Tasktobecompleted = a.Tasktobecompleted,
                Aiprovider = a.Aiprovider,
                Aimodel = a.Aimodel,
                Emailprovidertype = a.Emailprovidertype,
                Smtpserver = a.Smtpserver,
                Smtpport = a.Smtpport,
                Smtpusername = a.Smtpusername,
                Smtpusessl = a.Smtpusessl,
                Lastpoll = a.Lastpoll,
                Useconversationhistory = a.Useconversationhistory
            })
            .ToListAsync();

        return Ok(agents);
    }

    /// <summary>
    /// Get a specific agent by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AgentResponseDto>> GetAgent(int id)
    {
        var customerId = GetCustomerId();
        
        var agent = await _db.Agents
            .Where(a => a.Id == id && a.CustomerId == customerId)
            .Select(a => new AgentResponseDto
            {
                Id = a.Id,
                Emailaddress = a.Emailaddress,
                State = a.State,
                Defaultlanguage = a.Defaultlanguage,
                Emailprovider = a.Emailprovider,
                Emailusername = a.Emailusername,
                Emailserver = a.Emailserver,
                Emailport = a.Emailport,
                Emailusessl = a.Emailusessl,
                Tasktobecompleted = a.Tasktobecompleted,
                Aiprovider = a.Aiprovider,
                Aimodel = a.Aimodel,
                Emailprovidertype = a.Emailprovidertype,
                Smtpserver = a.Smtpserver,
                Smtpport = a.Smtpport,
                Smtpusername = a.Smtpusername,
                Smtpusessl = a.Smtpusessl,
                Lastpoll = a.Lastpoll,
                Useconversationhistory = a.Useconversationhistory
            })
            .FirstOrDefaultAsync();

        if (agent == null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        return Ok(agent);
    }

    /// <summary>
    /// Create a new agent
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AgentResponseDto>> CreateAgent([FromBody] CreateAgentDto dto)
    {
        var customerId = GetCustomerId();

        var agent = new Agent
        {
            CustomerId = customerId,
            Emailaddress = dto.Emailaddress,
            State = dto.State,
            Defaultlanguage = dto.Defaultlanguage,
            Emailprovider = dto.Emailprovider,
            Emailusername = dto.Emailusername,
            Emailpassword = dto.Emailpassword,
            Emailserver = dto.Emailserver,
            Emailport = dto.Emailport,
            Emailusessl = dto.Emailusessl,
            Tasktobecompleted = dto.Tasktobecompleted,
            Aiprovider = dto.Aiprovider,
            Aimodel = dto.Aimodel,
            Emailprovidertype = dto.Emailprovidertype,
            Smtpserver = dto.Smtpserver,
            Smtpport = dto.Smtpport,
            Smtpusername = dto.Smtpusername,
            Smtppassword = dto.Smtppassword,
            Smtpusessl = dto.Smtpusessl,
            Useconversationhistory = dto.Useconversationhistory
        };

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync();

        var response = new AgentResponseDto
        {
            Id = agent.Id,
            Emailaddress = agent.Emailaddress,
            State = agent.State,
            Defaultlanguage = agent.Defaultlanguage,
            Emailprovider = agent.Emailprovider,
            Emailusername = agent.Emailusername,
            Emailserver = agent.Emailserver,
            Emailport = agent.Emailport,
            Emailusessl = agent.Emailusessl,
            Tasktobecompleted = agent.Tasktobecompleted,
            Aiprovider = agent.Aiprovider,
            Aimodel = agent.Aimodel,
            Emailprovidertype = agent.Emailprovidertype,
            Smtpserver = agent.Smtpserver,
            Smtpport = agent.Smtpport,
            Smtpusername = agent.Smtpusername,
            Smtpusessl = agent.Smtpusessl,
            Lastpoll = agent.Lastpoll,
            Useconversationhistory = agent.Useconversationhistory
        };

        return CreatedAtAction(nameof(GetAgent), new { id = agent.Id }, response);
    }

    /// <summary>
    /// Update an existing agent
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<AgentResponseDto>> UpdateAgent(int id, [FromBody] UpdateAgentDto dto)
    {
        var customerId = GetCustomerId();

        var agent = await _db.Agents
            .Where(a => a.Id == id && a.CustomerId == customerId)
            .FirstOrDefaultAsync();

        if (agent == null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        // Update only provided fields
        if (dto.Emailaddress != null) agent.Emailaddress = dto.Emailaddress;
        if (dto.State != null) agent.State = dto.State;
        if (dto.Defaultlanguage != null) agent.Defaultlanguage = dto.Defaultlanguage;
        if (dto.Emailprovider != null) agent.Emailprovider = dto.Emailprovider;
        if (dto.Emailusername != null) agent.Emailusername = dto.Emailusername;
        if (dto.Emailpassword != null) agent.Emailpassword = dto.Emailpassword;
        if (dto.Emailserver != null) agent.Emailserver = dto.Emailserver;
        if (dto.Emailport.HasValue) agent.Emailport = dto.Emailport;
        if (dto.Emailusessl.HasValue) agent.Emailusessl = dto.Emailusessl;
        if (dto.Tasktobecompleted != null) agent.Tasktobecompleted = dto.Tasktobecompleted;
        if (dto.Aiprovider != null) agent.Aiprovider = dto.Aiprovider;
        if (dto.Aimodel != null) agent.Aimodel = dto.Aimodel;
        if (dto.Emailprovidertype != null) agent.Emailprovidertype = dto.Emailprovidertype;
        if (dto.Smtpserver != null) agent.Smtpserver = dto.Smtpserver;
        if (dto.Smtpport.HasValue) agent.Smtpport = dto.Smtpport;
        if (dto.Smtpusername != null) agent.Smtpusername = dto.Smtpusername;
        if (dto.Smtppassword != null) agent.Smtppassword = dto.Smtppassword;
        if (dto.Smtpusessl.HasValue) agent.Smtpusessl = dto.Smtpusessl;
        if (dto.Useconversationhistory.HasValue) agent.Useconversationhistory = dto.Useconversationhistory.Value;

        await _db.SaveChangesAsync();

        var response = new AgentResponseDto
        {
            Id = agent.Id,
            Emailaddress = agent.Emailaddress,
            State = agent.State,
            Defaultlanguage = agent.Defaultlanguage,
            Emailprovider = agent.Emailprovider,
            Emailusername = agent.Emailusername,
            Emailserver = agent.Emailserver,
            Emailport = agent.Emailport,
            Emailusessl = agent.Emailusessl,
            Tasktobecompleted = agent.Tasktobecompleted,
            Aiprovider = agent.Aiprovider,
            Aimodel = agent.Aimodel,
            Emailprovidertype = agent.Emailprovidertype,
            Smtpserver = agent.Smtpserver,
            Smtpport = agent.Smtpport,
            Smtpusername = agent.Smtpusername,
            Smtpusessl = agent.Smtpusessl,
            Lastpoll = agent.Lastpoll,
            Useconversationhistory = agent.Useconversationhistory
        };

        return Ok(response);
    }

    /// <summary>
    /// Delete an agent
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteAgent(int id)
    {
        var customerId = GetCustomerId();

        var agent = await _db.Agents
            .Where(a => a.Id == id && a.CustomerId == customerId)
            .FirstOrDefaultAsync();

        if (agent == null)
        {
            return NotFound(new { message = "Agent not found" });
        }

        _db.Agents.Remove(agent);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
