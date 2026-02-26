using Anthropic.SDK;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent_Admintool.Services;

/// <summary>
/// Service to upload skills to Anthropic immediately after creation.
/// </summary>
public class SkillUploadService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SkillUploadService> _logger;

    public SkillUploadService(IConfiguration configuration, ILogger<SkillUploadService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a skill to Anthropic and returns the assigned Skill ID.
    /// </summary>
    public async Task<SkillUploadResult> UploadSkillAsync(Skill skill)
    {
        var apiKey = _configuration["Claude:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return new SkillUploadResult
            {
                Success = false,
                ErrorMessage = "Claude API Key is not configured. Please set Claude:ApiKey in appsettings.json."
            };
        }

        if (skill.Skillfiles == null || skill.Skillfiles.Length == 0)
        {
            return new SkillUploadResult
            {
                Success = false,
                ErrorMessage = "Skill has no file content."
            };
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var client = new AnthropicClient(apiKey, httpClient);

            string skillId;

            if (skill.Filetype == "zip")
            {
                // Write zip to temp file and upload
                var tempPath = Path.GetTempFileName() + ".zip";
                try
                {
                    await File.WriteAllBytesAsync(tempPath, skill.Skillfiles);
                    var response = await client.Skills.CreateSkillFromZipAsync(skill.Skillname, tempPath);
                    skillId = response.Id;
                }
                finally
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
            }
            else
            {
                // Single .md file upload
                var stream = new MemoryStream(skill.Skillfiles);
                var files = new List<(string filename, Stream stream, string mimeType)>
                {
                    ($"{skill.Skillname}/SKILL.md", stream, "text/markdown")
                };
                var response = await client.Skills.CreateSkillFromStreamsAsync(skill.Skillname, files);
                skillId = response.Id;
            }

            _logger.LogInformation("Successfully uploaded skill '{SkillName}' to Anthropic with ID: {SkillId}", 
                skill.Skillname, skillId);

            return new SkillUploadResult
            {
                Success = true,
                SkillId = skillId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload skill '{SkillName}' to Anthropic", skill.Skillname);
            return new SkillUploadResult
            {
                Success = false,
                ErrorMessage = $"Upload failed: {ex.Message}"
            };
        }
    }
}

public class SkillUploadResult
{
    public bool Success { get; set; }
    public string? SkillId { get; set; }
    public string? ErrorMessage { get; set; }
}
