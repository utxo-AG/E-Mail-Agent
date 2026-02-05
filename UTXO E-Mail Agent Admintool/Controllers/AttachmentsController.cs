using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Admintool.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AttachmentsController : ControllerBase
{
    private readonly IDbContextFactory<DefaultdbContext> _dbFactory;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(IDbContextFactory<DefaultdbContext> dbFactory, ILogger<AttachmentsController> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    [HttpGet("download/{id}")]
    public async Task<IActionResult> Download(int id)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var attachment = await db.ConversationAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attachment == null)
            {
                _logger.LogWarning($"Attachment with ID {id} not found");
                return NotFound("Attachment not found");
            }

            // Convert base64 content to bytes
            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(attachment.Content);
            }
            catch (FormatException)
            {
                _logger.LogError($"Failed to decode base64 content for attachment {id}");
                return BadRequest("Invalid attachment content format");
            }

            _logger.LogInformation($"Downloading attachment: {attachment.Filename} (ID: {id})");

            // Return file with proper content type and filename
            return File(fileBytes, attachment.ContentType, attachment.Filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error downloading attachment {id}");
            return StatusCode(500, "Error downloading attachment");
        }
    }
}
