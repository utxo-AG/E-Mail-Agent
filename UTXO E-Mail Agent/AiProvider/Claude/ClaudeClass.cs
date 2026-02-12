using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Extensions;
using Anthropic.SDK.Messaging;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.McpServers;

namespace UTXO_E_Mail_Agent.AiProvider.Claude;

public class ClaudeClass : IAiProvider
{
    private readonly string _apikey;
    private readonly string _connectionString;

    public ClaudeClass(string apikey, string connectionString)
    {
        _apikey = apikey;
        _connectionString = connectionString;
    }

    public async Task<AiResponseClass> GenerateResponse(string systemPrompt, string prompt, MailClass mailClass, Agent agent, Conversation conversation)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        var client = new AnthropicClient(_apikey, httpClient);

        var container = new Container
        {
            Skills = new List<Skill>
            {
                new Skill { Type = "anthropic", SkillId = "pdf", Version = "latest" },
                new Skill { Type = "anthropic", SkillId = "pptx", Version = "latest" },
                new Skill { Type = "anthropic", SkillId = "xlsx", Version = "latest" },
                new Skill { Type = "anthropic", SkillId = "docx", Version = "latest" },
            }
        };

        // Built-in Tools
        var tools = new List<Anthropic.SDK.Common.Tool>
        {
            new Function("code_execution", "code_execution_20250825",
                new Dictionary<string, object> { { "name", "code_execution" } }),
            ServerTools.GetWebSearchTool(5, null, null,
                new UserLocation() { City = "Berlin", Country = "DE" }),
            new Function("bash", "bash_20250124",
                new Dictionary<string, object> { { "name", "bash" } })
        };

        // Dynamically register MCP tools from Agent.Mcpservers
        var mcpToolHandlers = new Dictionary<string, Func<JsonStringParameter, Task<string>>>();

        foreach (var mcpServer in agent.Mcpservers.OrEmptyIfNull())
        {
            var toolName = SanitizeToolName(mcpServer.Name);

            var inputSchema = new InputSchema()
            {
                Type = "object",
                Properties = new Dictionary<string, Property>()
                {
                    { "json", new Property() { Type = "string", Description = mcpServer.Description } }
                },
                Required = new List<string>() { "json" }
            };

            var jsonOpts = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            string schemaJson = System.Text.Json.JsonSerializer.Serialize(inputSchema, jsonOpts);

            var function = new Function(
                toolName,
                $"{mcpServer.Description} (HTTP {mcpServer.Call.ToUpper()} {mcpServer.Url})",
                JsonNode.Parse(schemaJson));

            tools.Add(function);

            var handler = HttpMcpServerHandler.CreateToolHandler(mcpServer, conversation.Id, _connectionString);
            mcpToolHandlers[toolName] = handler;

            Console.WriteLine($"[MCP] Registered tool: {toolName} -> {mcpServer.Call.ToUpper()} {mcpServer.Url}");
        }

        var messages = new List<Message>
        {
            new Message(RoleType.User,
                $"E-Mail Subject: {mailClass.Subject} {Environment.NewLine} E-Mail Text:{mailClass.Text} {Environment.NewLine} E-Mail Text (HTML) {mailClass.Html}")
        };

        var parameters = new MessageParameters
        {
            Model = AnthropicModels.Claude4Sonnet,
            MaxTokens = 8000,
            System = new List<SystemMessage>() { new SystemMessage(systemPrompt) },
            Messages = messages,
            Container = container,
            Tools = tools
        };

        // Tool-Use-Loop
        const int maxIterations = 20;
        MessageResponse response = null;
        var allDownloadedFiles = new List<string>(); // Collect files from all iterations
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SkillOutput");
        Directory.CreateDirectory(outputDirectory);

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            Console.WriteLine($"[Skill Download] Iteration {iteration}: Starting download");
            response = await client.Messages.GetClaudeMessageAsync(parameters);

            // Download skill files after EACH iteration (before response gets overwritten)
            try
            {
                // Debug: Log container info
                if (response.Container != null)
                {
                    Console.WriteLine($"[Skill Download] Iteration {iteration}: Container ID = {response.Container.Id}");
                }

                // Try to get file IDs from this response
                var fileIds = response.GetFileIds();
                Console.WriteLine($"[Skill Download] Iteration {iteration}: Found {fileIds.Count()} file IDs");

                var iterationFiles = await response.DownloadFilesAsync(client, outputDirectory);
                if (iterationFiles.Any())
                {
                    Console.WriteLine($"[Skill Download] Iteration {iteration}: Downloaded {iterationFiles.Count()} files");
                    foreach (var f in iterationFiles)
                    {
                        Console.WriteLine($"  -> {f}");
                        if (!allDownloadedFiles.Contains(f))
                            allDownloadedFiles.Add(f);
                    }
                }
                else
                {
                    Console.WriteLine($"[Skill Download] Iteration {iteration}: No files downloaded");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Skill Download] Iteration {iteration}: Error: {ex.Message}");
            }

            // Only handle our MCP tools (built-in tools like code_execution/bash/web_search are processed server-side)
            var mcpToolCalls = response.Content
                .OfType<ToolUseContent>()
                .Where(tc => mcpToolHandlers.ContainsKey(tc.Name))
                .ToList();

            if (!mcpToolCalls.Any())
                break;

            // Add Claude response (with tool use) as assistant message
            messages.Add(response.Message);

            // Execute each MCP tool call and send result back
            foreach (var toolUse in mcpToolCalls)
            {
                Console.WriteLine($"[MCP Tool Call] {toolUse.Name} - Input: {toolUse.Input}");

                try
                {
                    var jsonParam = new JsonStringParameter();
                    if (toolUse.Input != null && toolUse.Input.AsObject().ContainsKey("json"))
                    {
                        jsonParam.json = toolUse.Input["json"]?.ToString() ?? "{}";
                    }
                    else if (toolUse.Input != null)
                    {
                        jsonParam.json = toolUse.Input.ToJsonString();
                    }

                    var result = await mcpToolHandlers[toolUse.Name](jsonParam);

                    messages.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase>()
                        {
                            new ToolResultContent()
                            {
                                ToolUseId = toolUse.Id,
                                Content = new List<ContentBase>()
                                {
                                    new TextContent() { Text = result }
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[MCP Tool Error] {toolUse.Name}: {ex.Message}");

                    messages.Add(new Message()
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase>()
                        {
                            new ToolResultContent()
                            {
                                ToolUseId = toolUse.Id,
                                IsError = true,
                                Content = new List<ContentBase>()
                                {
                                    new TextContent() { Text = $"ERROR: {ex.Message}" }
                                }
                            }
                        }
                    });
                }
            }
        }

        // Use files collected from all iterations
        Console.WriteLine($"[Skill Download] Total downloaded files: {allDownloadedFiles.Count}");

        if (allDownloadedFiles.Any())
        {
            Console.WriteLine("----------------------------------------------");
            Console.WriteLine("Downloaded Files:");
            Console.WriteLine("----------------------------------------------");
            foreach (var filePath in allDownloadedFiles)
            {
                Console.WriteLine($"  {filePath}");
            }
            Console.WriteLine();
        }

        // Extract text content
        var fullText = "";
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine("All Content:");
        Console.WriteLine("----------------------------------------------");
        foreach (var content in response.Content)
        {
            Console.WriteLine($"Type: {content.GetType().Name}");

            if (content is TextContent textContent)
            {
                Console.WriteLine($"{textContent.Text}");
                fullText += textContent.Text + "\n";
            }
            else if (content is WebSearchToolResultContent webSearchContent)
            {
                Console.WriteLine($"Web Search Tool Result with {webSearchContent.Content.Count} items");
            }
            else if (content is ToolUseContent toolUse)
            {
                Console.WriteLine($"Tool Use: {toolUse.Name}");
            }
            else
            {
                // Log unknown content types for debugging
                Console.WriteLine($"[DEBUG] Content properties: {System.Text.Json.JsonSerializer.Serialize(content)}");
            }
        }

        // Extract JSON from response (Claude can write text before the JSON)
        var responseClass = ParseAiResponse(fullText);

        // Check if we need to generate a document with a second agent
        if (responseClass.MustCreateAttachment &&
            !string.IsNullOrEmpty(responseClass.AttachmentType) &&
            !string.IsNullOrEmpty(responseClass.AttachmentData))
        {
            Console.WriteLine($"[DocumentGenerator] MustCreateAttachment=true, Type={responseClass.AttachmentType}");
            Console.WriteLine($"[DocumentGenerator] Starting second agent for document generation...");

            try
            {
                var documentGenerator = new ClaudeGenerateDocumentsClass(_apikey);
                var generatedAttachment = await documentGenerator.GenerateDocumentAsync(
                    responseClass.AttachmentType,
                    responseClass.AttachmentData,
                    responseClass.AttachmentFilename
                );

                if (generatedAttachment != null)
                {
                    Console.WriteLine($"[DocumentGenerator] Successfully generated: {generatedAttachment.Filename}");

                    // Add the generated attachment to the response
                    var attachmentsList = responseClass.Attachments?.ToList() ?? new List<Attachment>();
                    attachmentsList.Add(generatedAttachment);
                    responseClass.Attachments = attachmentsList.ToArray();
                }
                else
                {
                    Console.WriteLine("[DocumentGenerator] Document generation failed - no attachment created");

                    // Add note to explanation that attachment could not be generated
                    var note = "\n\n[Note: The requested document could not be created. However, the information is included in the email text.]";
                    responseClass.AiExplanation = (responseClass.AiExplanation ?? "") + note;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentGenerator] Error during document generation: {ex.Message}");
                Console.WriteLine($"[DocumentGenerator] Stack trace: {ex.StackTrace}");

                // Add note to explanation that attachment could not be generated
                var note = "\n\n[Note: The requested document could not be created due to a technical error. However, the information is included in the email text.]";
                responseClass.AiExplanation = (responseClass.AiExplanation ?? "") + note;

                // Email will still be sent without the attachment
            }
        }

        // Debug: Log parsed attachments from JSON
        if (responseClass.Attachments != null && responseClass.Attachments.Length > 0)
        {
            Console.WriteLine($"[JSON Attachments] Found {responseClass.Attachments.Length} attachments in JSON:");
            foreach (var att in responseClass.Attachments)
            {
                Console.WriteLine($"  - Filename: {att.Filename}, Path: {att.Path}, HasContent: {!string.IsNullOrEmpty(att.Content)}");
            }
        }
        else
        {
            Console.WriteLine("[JSON Attachments] No attachments in JSON response");
        }

        // Add downloaded skill files as attachments
        if (allDownloadedFiles.Any())
        {
            var existingAttachments = responseClass.Attachments?.ToList() ?? new List<Attachment>();

            foreach (var filePath in allDownloadedFiles)
            {
                try
                {
                    var fileBytes = await File.ReadAllBytesAsync(filePath);
                    var base64Content = Convert.ToBase64String(fileBytes);
                    string fileName = System.IO.Path.GetFileName(filePath) ?? filePath;
                    string contentType = GetContentTypeFromExtension(filePath);

                    existingAttachments.Add(new Attachment
                    {
                        Filename = fileName,
                        Content = base64Content,
                        ContentType = contentType,
                        Path = filePath
                    });

                    Console.WriteLine("[Attachment] Added skill file: " + fileName + " (" + contentType + ")");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("[Attachment Error] Could not read file " + filePath + ": " + ex.Message);
                }
            }

            responseClass.Attachments = existingAttachments.ToArray();
        }

        // Attachments that only have a path: Read file and base64 encode
        if (responseClass.Attachments != null)
        {
            foreach (var att in responseClass.Attachments)
            {
                if (!string.IsNullOrEmpty(att.Path) && string.IsNullOrEmpty(att.Content) && File.Exists(att.Path))
                {
                    try
                    {
                        var fileBytes = await File.ReadAllBytesAsync(att.Path);
                        att.Content = Convert.ToBase64String(fileBytes);
                        if (string.IsNullOrEmpty(att.ContentType))
                            att.ContentType = GetContentTypeFromExtension(att.Path);
                        if (string.IsNullOrEmpty(att.Filename))
                            att.Filename = System.IO.Path.GetFileName(att.Path) ?? att.Path;

                        Console.WriteLine("[Attachment] Loaded from path: " + att.Filename);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("[Attachment Error] Could not read " + att.Path + ": " + ex.Message);
                    }
                }
            }
        }

        return responseClass;
    }

    private static string SanitizeToolName(string name)
    {
        // Anthropic Tool-Namen: ^[a-zA-Z0-9_-]{1,64}$
        var sanitized = Regex.Replace(name.Replace(" ", "_"), "[^a-zA-Z0-9_-]", "");
        return sanitized.Length > 64 ? sanitized[..64] : sanitized;
    }

    /// <summary>
    /// Extracts JSON from Claude's response. Claude can write text before the JSON.
    /// </summary>
    private static AiResponseClass ParseAiResponse(string fullText)
    {
        Console.WriteLine($"[ParseAiResponse] Attempting to parse response ({fullText?.Length ?? 0} characters)");

        // Check for empty or null response
        if (string.IsNullOrWhiteSpace(fullText))
        {
            Console.WriteLine("[ParseAiResponse] WARNING: Empty response from Claude!");
            return new AiResponseClass
            {
                EmailResponseText = "Ich konnte keine passende Antwort generieren. Bitte versuchen Sie es erneut.",
                EmailResponseSubject = "RE: Ihre Anfrage",
                EmailResponseHtml = "<p>Ich konnte keine passende Antwort generieren. Bitte versuchen Sie es erneut.</p>",
                Attachments = Array.Empty<Attachment>()
            };
        }

        try
        {
            // First try to find a ```json block
            var jsonBlockMatch = Regex.Match(fullText, @"```json\s*([\s\S]*?)\s*```");
            if (jsonBlockMatch.Success)
            {
                Console.WriteLine("[ParseAiResponse] Found JSON block in markdown");
                var parsed = JsonConvert.DeserializeObject<AiResponseClass>(jsonBlockMatch.Groups[1].Value);
                if (parsed != null)
                {
                    // Save text before JSON as AiExplanation
                    var beforeJson = fullText[..fullText.IndexOf("```json", StringComparison.Ordinal)].Trim();
                    if (!string.IsNullOrEmpty(beforeJson))
                        parsed.AiExplanation = beforeJson;
                    return parsed;
                }
            }

            // Fallback: Find last complete JSON object (search from end)
            var lastBrace = fullText.LastIndexOf('}');
            if (lastBrace >= 0)
            {
                // Find matching opening brace
                var depth = 0;
                for (int i = lastBrace; i >= 0; i--)
                {
                    if (fullText[i] == '}') depth++;
                    else if (fullText[i] == '{') depth--;

                    if (depth == 0)
                    {
                        var jsonCandidate = fullText[i..(lastBrace + 1)];
                        try
                        {
                            var parsed = JsonConvert.DeserializeObject<AiResponseClass>(jsonCandidate);
                            if (parsed != null && !string.IsNullOrEmpty(parsed.EmailResponseText))
                            {
                                Console.WriteLine("[ParseAiResponse] Found valid JSON object");
                                var beforeJson = fullText[..i].Trim();
                                if (!string.IsNullOrEmpty(beforeJson))
                                    parsed.AiExplanation = beforeJson;
                                return parsed;
                            }
                        }
                        catch { /* Not valid JSON, continue searching */ }
                    }
                }
            }

            // Last fallback: Parse entire text as JSON
            Console.WriteLine("[ParseAiResponse] Attempting to parse entire text as JSON");
            return JsonConvert.DeserializeObject<AiResponseClass>(fullText.Trim())
                   ?? new AiResponseClass { EmailResponseText = fullText.Trim() };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ParseAiResponse] ERROR: Failed to parse response: {ex.Message}");
            Console.WriteLine($"[ParseAiResponse] Response content: {fullText.Substring(0, Math.Min(500, fullText.Length))}...");

            // Return a default response on error
            return new AiResponseClass
            {
                EmailResponseText = fullText.Trim(),
                EmailResponseSubject = "RE: Ihre Anfrage",
                EmailResponseHtml = $"<p>{System.Web.HttpUtility.HtmlEncode(fullText.Trim())}</p>",
                Attachments = Array.Empty<Attachment>(),
                AiExplanation = $"Parse error: {ex.Message}"
            };
        }
    }

    private static string GetContentTypeFromExtension(string filePath)
    {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".csv" => "text/csv",
            ".xml" => "text/xml",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream"
        };
    }
}
