using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Extensions;
using Anthropic.SDK.Messaging;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;
using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.AiProvider.Claude;

/// <summary>
/// Separate class for document generation using Claude Skills (PDF, DOCX, XLSX, PPTX).
/// This class is called when the main agent sets MustCreateAttachment = true.
/// It runs without MCP tools to avoid conflicts with the Skills API.
/// </summary>
public class ClaudeGenerateDocumentsClass
{
    private readonly string _apiKey;
    private readonly int _agentId;

    public ClaudeGenerateDocumentsClass(string apiKey, int agentId)
    {
        _apiKey = apiKey;
        _agentId = agentId;
    }

    /// <summary>
    /// Generates a document using Claude Skills.
    /// </summary>
    /// <param name="attachmentType">Type of document: pdf, docx, xlsx, pptx</param>
    /// <param name="attachmentData">Structured data to include in the document</param>
    /// <param name="filename">Suggested filename for the document</param>
    /// <returns>Attachment with the generated document, or null if generation failed</returns>
    public async Task<Attachment?> GenerateDocumentAsync(string attachmentType, string attachmentData, string? filename)
    {
        Logger.Log($"[DocumentGenerator] Starting document generation: {attachmentType}", _agentId);
        Logger.Log($"[DocumentGenerator] Data length: {attachmentData?.Length ?? 0} characters", _agentId);

        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10) // Skills can take time
        };

        var client = new AnthropicClient(_apiKey, httpClient);

        // Determine which skill to use
        var skillId = attachmentType.ToLowerInvariant() switch
        {
            "pdf" => "pdf",
            "docx" or "word" => "docx",
            "xlsx" or "excel" => "xlsx",
            "pptx" or "powerpoint" => "pptx",
            _ => "pdf" // Default to PDF
        };

        Logger.Log($"[DocumentGenerator] Using skill: {skillId}", _agentId);

        // Create container with only the required skill
        var container = new Container
        {
            Skills = new List<Skill>
            {
                new Skill
                {
                    Type = "anthropic",
                    SkillId = skillId,
                    Version = "latest"
                }
            }
        };

        // System prompt for document generation
        var systemPrompt = $@"Du bist ein Dokument-Generator. Deine einzige Aufgabe ist es, ein professionelles {attachmentType.ToUpper()}-Dokument zu erstellen.

WICHTIG:
- Erstelle das Dokument mit den bereitgestellten Daten
- Das Dokument soll professionell formatiert sein
- Verwende eine klare Struktur mit Überschriften und Abschnitten
- Füge ein Firmenlogo oder Header hinzu wenn möglich
- Das Dokument soll druckfertig sein

Erstelle JETZT das {attachmentType.ToUpper()}-Dokument mit den folgenden Daten.
Antworte NUR mit dem erstellten Dokument, keine zusätzlichen Erklärungen.";

        var messages = new List<Message>
        {
            new Message(RoleType.User, $"Erstelle ein {attachmentType.ToUpper()}-Dokument mit folgenden Daten:\n\n{attachmentData}")
        };

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude4Sonnet,
            MaxTokens = 8000,
            System = new List<SystemMessage> { new SystemMessage(systemPrompt) },
            Messages = messages,
            Container = container,
            // Only code execution tool for skills - NO other tools
            Tools = new List<Anthropic.SDK.Common.Tool>
            {
                new Function("code_execution", "code_execution_20250825",
                    new Dictionary<string, object> { { "name", "code_execution" } })
            }
        };

        try
        {
            Logger.Log("[DocumentGenerator] Calling Claude API...", _agentId);
            var response = await client.Messages.GetClaudeMessageAsync(parameters);

            // Log container info
            if (response.Container != null)
            {
                Logger.Log($"[DocumentGenerator] Container ID: {response.Container.Id}", _agentId);
            }

            // Check for file IDs
            var fileIds = response.GetFileIds();
            Logger.Log($"[DocumentGenerator] Found {fileIds.Count()} file IDs", _agentId);

            // Download files
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SkillOutput");
            Directory.CreateDirectory(outputDirectory);

            var downloadedFiles = await response.DownloadFilesAsync(client, outputDirectory);
            Logger.Log($"[DocumentGenerator] Downloaded {downloadedFiles.Count()} files", _agentId);

            if (downloadedFiles.Any())
            {
                var filePath = downloadedFiles.First();
                Logger.Log($"[DocumentGenerator] Using file: {filePath}", _agentId);

                // Read the file and create attachment
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                var base64Content = Convert.ToBase64String(fileBytes);
                var actualFilename = filename ?? Path.GetFileName(filePath);
                var contentType = GetContentType(attachmentType);

                Logger.Log($"[DocumentGenerator] Created attachment: {actualFilename} ({fileBytes.Length} bytes)", _agentId);

                return new Attachment
                {
                    Filename = actualFilename,
                    Content = base64Content,
                    ContentType = contentType,
                    Path = filePath
                };
            }
            else
            {
                Logger.LogWarning("[DocumentGenerator] No files downloaded from skill", _agentId);
                Logger.LogWarning("[DocumentGenerator] This may indicate a problem with the Skills API or the generated content", _agentId);

                // Log response content for debugging
                Logger.Log("[DocumentGenerator] Response content types:", _agentId);
                foreach (var content in response.Content)
                {
                    Logger.Log($"  - {content.GetType().Name}", _agentId);
                    if (content is TextContent textContent)
                    {
                        // Log first 500 chars of text content for debugging
                        var preview = textContent.Text?.Length > 500
                            ? textContent.Text.Substring(0, 500) + "..."
                            : textContent.Text;
                        Logger.Log($"    Text preview: {preview}", _agentId);
                    }
                }

                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError($"[DocumentGenerator] HTTP Error: {ex.Message}", _agentId);
            Logger.LogError($"[DocumentGenerator] This may indicate network issues or API unavailability", _agentId);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogError($"[DocumentGenerator] Timeout Error: {ex.Message}", _agentId);
            Logger.LogError($"[DocumentGenerator] Document generation took too long (>10 minutes)", _agentId);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[DocumentGenerator] Unexpected Error: {ex.Message}", _agentId);
            Logger.LogError($"[DocumentGenerator] Stack trace: {ex.StackTrace}", _agentId);
            return null;
        }
    }

    private static string GetContentType(string attachmentType)
    {
        return attachmentType.ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            "docx" or "word" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xlsx" or "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "pptx" or "powerpoint" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            _ => "application/octet-stream"
        };
    }
}
