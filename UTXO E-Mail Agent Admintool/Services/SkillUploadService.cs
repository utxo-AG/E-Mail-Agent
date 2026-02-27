using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
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
    
    private const int MinFileSize = 10;           // Minimum 10 bytes
    private const int MaxFileSize = 10 * 1024 * 1024;  // Maximum 10 MB

    public SkillUploadService(IConfiguration configuration, ILogger<SkillUploadService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Validates the skill file before uploading.
    /// </summary>
    public SkillValidationResult ValidateSkill(Skill skill)
    {
        if (skill.Skillfiles == null || skill.Skillfiles.Length == 0)
        {
            return new SkillValidationResult { IsValid = false, ErrorMessage = "Die Datei ist leer." };
        }

        if (skill.Skillfiles.Length < MinFileSize)
        {
            return new SkillValidationResult { IsValid = false, ErrorMessage = "Die Datei ist zu klein (mindestens 10 Bytes erforderlich)." };
        }

        if (skill.Skillfiles.Length > MaxFileSize)
        {
            return new SkillValidationResult { IsValid = false, ErrorMessage = "Die Datei ist zu groß (maximal 10 MB erlaubt)." };
        }

        if (skill.Filetype == "zip")
        {
            return ValidateZipSkill(skill.Skillfiles);
        }
        else
        {
            return ValidateMarkdownSkill(skill.Skillfiles);
        }
    }

    // Allowed frontmatter keys according to Anthropic API
    private static readonly HashSet<string> AllowedFrontmatterKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "description", "license", "allowed-tools", "compatibility",
        "metadata", "argument-hint", "user-invocable", "disable-model-invocation",
        "when_to_use", "version", "model", "context", "agent"
    };

    private SkillValidationResult ValidateMarkdownSkill(byte[] fileContent)
    {
        // Check if it's valid UTF-8 text
        string content;
        try
        {
            content = Encoding.UTF8.GetString(fileContent);
        }
        catch
        {
            return new SkillValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Die Datei ist kein gültiger UTF-8 Text. Bitte laden Sie eine Markdown-Datei hoch." 
            };
        }

        // Check for binary content (null bytes indicate binary file)
        if (content.Contains('\0'))
        {
            return new SkillValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Die Datei scheint eine Binärdatei zu sein, keine Markdown-Datei." 
            };
        }

        // Validate frontmatter if present
        var frontmatterValidation = ValidateFrontmatter(content);
        if (!frontmatterValidation.IsValid)
        {
            return frontmatterValidation;
        }

        // Check if it contains at least one markdown header
        if (!Regex.IsMatch(content, @"^#{1,6}\s+.+", RegexOptions.Multiline))
        {
            return new SkillValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Die Datei enthält keine Markdown-Überschriften (# Header). Skills benötigen mindestens eine Überschrift.",
                Warning = true
            };
        }

        // Check minimum content length (at least some meaningful content)
        var textOnly = Regex.Replace(content, @"\s+", " ").Trim();
        if (textOnly.Length < 50)
        {
            return new SkillValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Die Datei enthält zu wenig Inhalt. Ein Skill sollte mindestens 50 Zeichen Text enthalten." 
            };
        }

        return new SkillValidationResult 
        { 
            IsValid = true,
            Info = frontmatterValidation.Info
        };
    }

    private SkillValidationResult ValidateFrontmatter(string content)
    {
        // Check if file starts with frontmatter (---)
        var frontmatterMatch = Regex.Match(content, @"^---\s*\n([\s\S]*?)\n---", RegexOptions.Multiline);
        
        if (!frontmatterMatch.Success)
        {
            // No frontmatter is OK, but we'll note it
            return new SkillValidationResult 
            { 
                IsValid = true,
                Info = "Kein Frontmatter gefunden (optional)"
            };
        }

        var frontmatterContent = frontmatterMatch.Groups[1].Value;
        var invalidKeys = new List<string>();
        var foundKeys = new List<string>();

        // Parse YAML-like frontmatter (simple key: value pairs)
        var lines = frontmatterContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                continue;

            // Match top-level keys (not indented)
            var keyMatch = Regex.Match(line, @"^([a-zA-Z_-]+)\s*:");
            if (keyMatch.Success)
            {
                var key = keyMatch.Groups[1].Value;
                foundKeys.Add(key);
                
                if (!AllowedFrontmatterKeys.Contains(key))
                {
                    invalidKeys.Add(key);
                }
            }
        }

        if (invalidKeys.Any())
        {
            return new SkillValidationResult 
            { 
                IsValid = false,
                ErrorMessage = $"Ungültige Frontmatter-Keys: '{string.Join("', '", invalidKeys)}'. " +
                              $"Erlaubte Keys sind: {string.Join(", ", AllowedFrontmatterKeys.OrderBy(k => k))}"
            };
        }

        return new SkillValidationResult 
        { 
            IsValid = true,
            Info = $"Frontmatter OK ({string.Join(", ", foundKeys)})"
        };
    }

    private SkillValidationResult ValidateZipSkill(byte[] fileContent)
    {
        try
        {
            using var zipStream = new MemoryStream(fileContent);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            if (archive.Entries.Count == 0)
            {
                return new SkillValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Das ZIP-Archiv ist leer." 
                };
            }

            // Look for SKILL.md (case-insensitive, can be in any folder)
            var skillMdEntry = archive.Entries
                .FirstOrDefault(e => e.Name.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase));

            if (skillMdEntry == null)
            {
                var mdFiles = archive.Entries.Where(e => e.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)).ToList();
                if (mdFiles.Any())
                {
                    return new SkillValidationResult 
                    { 
                        IsValid = false, 
                        ErrorMessage = $"Das ZIP enthält keine SKILL.md Datei. Gefundene MD-Dateien: {string.Join(", ", mdFiles.Select(f => f.Name))}. Bitte benennen Sie die Hauptdatei in SKILL.md um." 
                    };
                }
                return new SkillValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Das ZIP enthält keine SKILL.md Datei. Skills müssen eine SKILL.md als Hauptdatei enthalten." 
                };
            }

            // Validate the SKILL.md content
            using var entryStream = skillMdEntry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);
            var content = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(content))
            {
                return new SkillValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Die SKILL.md Datei im ZIP ist leer." 
                };
            }

            // Validate frontmatter
            var frontmatterValidation = ValidateFrontmatter(content);
            if (!frontmatterValidation.IsValid)
            {
                return new SkillValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = $"SKILL.md Frontmatter-Fehler: {frontmatterValidation.ErrorMessage}"
                };
            }

            if (!Regex.IsMatch(content, @"^#{1,6}\s+.+", RegexOptions.Multiline))
            {
                return new SkillValidationResult 
                { 
                    IsValid = false, 
                    ErrorMessage = "Die SKILL.md enthält keine Markdown-Überschriften (# Header).",
                    Warning = true
                };
            }

            return new SkillValidationResult 
            { 
                IsValid = true,
                Info = $"ZIP enthält {archive.Entries.Count} Datei(en), SKILL.md gefunden. {frontmatterValidation.Info}"
            };
        }
        catch (InvalidDataException)
        {
            return new SkillValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = "Die Datei ist kein gültiges ZIP-Archiv." 
            };
        }
        catch (Exception ex)
        {
            return new SkillValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = $"Fehler beim Lesen des ZIP-Archivs: {ex.Message}" 
            };
        }
    }

    /// <summary>
    /// Uploads a skill to Anthropic and returns the assigned Skill ID.
    /// </summary>
    public async Task<SkillUploadResult> UploadSkillAsync(Skill skill)
    {
        // Pre-validation
        var validation = ValidateSkill(skill);
        if (!validation.IsValid)
        {
            return new SkillUploadResult
            {
                Success = false,
                ErrorMessage = validation.ErrorMessage
            };
        }

        var apiKey = _configuration["Claude:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return new SkillUploadResult
            {
                Success = false,
                ErrorMessage = "Claude API Key is not configured. Please set Claude:ApiKey in appsettings.json."
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
                    await File.WriteAllBytesAsync(tempPath, skill.Skillfiles!);
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
                var stream = new MemoryStream(skill.Skillfiles!);
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

public class SkillValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Info { get; set; }
    public bool Warning { get; set; }  // True if validation passed but with warnings
}
