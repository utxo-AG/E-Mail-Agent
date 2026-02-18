using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Extensions;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.McpServers;
using UTXO_E_Mail_Agent.Services;

namespace UTXO_E_Mail_Agent.AiProvider.Claude;

public class ClaudeClass : IAiProvider
{
    private readonly string _apikey;
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;

    public ClaudeClass(string apikey, string connectionString, IConfiguration configuration)
    {
        _apikey = apikey;
        _connectionString = connectionString;
        _configuration = configuration;
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
                new UserLocation() { City = agent.Customer.City, Country = CountryToIso2(agent.Customer.Country) }),
            // new Function("bash", "bash_20250124",
            //     new Dictionary<string, object> { { "name", "bash" } })
        };

        // Send Email Tool - always available for forwarding/sending emails
        // Select the right implementation based on the agent's email provider
        ISendEmailMcpServer? sendEmailServer = null;
        try
        {
            var provider = agent.Emailprovider?.ToLower();
            if (provider == "imap" || provider == "pop3")
            {
                sendEmailServer = new SendEmailMcpServerSmtp(agent);
                Logger.Log($"[MCP] Using SMTP send email server for {provider} agent", agent.Id);
            }
            else
            {
                sendEmailServer = new SendEmailMcpServerInbound(_configuration, agent.Emailaddress, agent.Id);
                Logger.Log($"[MCP] Using Inbound API send email server for {provider} agent", agent.Id);
            }

            var sendEmailSchema = new InputSchema()
            {
                Type = "object",
                Properties = new Dictionary<string, Property>()
                {
                    { "to", new Property() { Type = "string", Description = "Recipient email address" } },
                    { "subject", new Property() { Type = "string", Description = "Email subject" } },
                    { "text", new Property() { Type = "string", Description = "Plain text content of the email" } },
                    { "html", new Property() { Type = "string", Description = "HTML content of the email (optional)" } },
                    { "reply_to", new Property() { Type = "string", Description = "Reply-to address (optional). IMPORTANT: When forwarding a customer email, set this to the customer's email so replies go directly to them." } }
                },
                Required = new List<string>() { "to", "subject", "text" }
            };

            var jsonOpts = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            string sendEmailSchemaJson = System.Text.Json.JsonSerializer.Serialize(sendEmailSchema, jsonOpts);

            tools.Add(new Function(
                "send_email",
                $"Send an email to a recipient. Use this to forward emails or send new emails. The sender address is always {agent.Emailaddress}. When forwarding, use reply_to with the original sender's email so replies go to them.",
                JsonNode.Parse(sendEmailSchemaJson)));

            Logger.Log($"[MCP] Registered built-in tool: send_email (from: {agent.Emailaddress})", agent.Id);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[MCP] Could not register send_email tool: {ex.Message}", agent.Id);
        }

        // Dynamically register MCP tools from Agent.Mcpservers
        var mcpToolHandlers = new Dictionary<string, Func<JsonStringParameter, Task<string>>>();

        foreach (var mcpServer in agent.Mcpservers.OrEmptyIfNull())
        {
            var toolName = SanitizeToolName(mcpServer.Name);

            // Extract {placeholder} names from URL to create proper schema properties
            var urlPlaceholders = Regex.Matches(mcpServer.Url, @"\{(\w+)\}")
                .Select(m => m.Groups[1].Value)
                .ToList();

            var properties = new Dictionary<string, Property>();
            var required = new List<string>();

            if (urlPlaceholders.Any())
            {
                // Create individual properties for each URL placeholder
                foreach (var placeholder in urlPlaceholders)
                {
                    properties.Add(placeholder, new Property() { Type = "string", Description = $"Value for {{{placeholder}}} in the URL" });
                    required.Add(placeholder);
                }
            }
            else
            {
                // No placeholders - use generic json property for query/body parameters
                properties.Add("json", new Property() { Type = "string", Description = mcpServer.Description });
                required.Add("json");
            }

            var inputSchema = new InputSchema()
            {
                Type = "object",
                Properties = properties,
                Required = required
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

            Logger.Log($"[MCP] Registered tool: {toolName} -> {mcpServer.Call.ToUpper()} {mcpServer.Url}", agent.Id);
        }

        var messages = new List<Message>
        {
            new Message(RoleType.User,
                $"E-Mail Subject: {mailClass.Subject} {Environment.NewLine} E-Mail Text:{mailClass.Text} {Environment.NewLine} E-Mail Text (HTML) {mailClass.Html}")
        };

        var effectiveModel = ModelFallbackCache.GetModel(agent.Aimodel);
        if (effectiveModel != agent.Aimodel)
            Logger.Log($"[Claude] Model fallback active: {agent.Aimodel} -> {effectiveModel}", agent.Id);

        var parameters = new MessageParameters
        {
            Model = effectiveModel,
            MaxTokens = 8000,
            System = [new SystemMessage(systemPrompt)],
            Messages = messages,
            Container = container,
            Tools = tools
        };

        // Tool-Use-Loop
        const int maxIterations = 20;
        MessageResponse response = null;
        var allDownloadedFiles = new List<string>(); // Collect files from all iterations
        var sentEmailRecipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // Track send_email recipients
        var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "SkillOutput");
        Directory.CreateDirectory(outputDirectory);

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            Logger.Log($"[Skill Download] Iteration {iteration}: Starting download (model: {parameters.Model})", agent.Id);
            try
            {
                response = await client.Messages.GetClaudeMessageAsync(parameters);
            }
            catch (Exception ex) when (ex.Message.Contains("overloaded", StringComparison.OrdinalIgnoreCase))
            {
                ModelFallbackCache.RecordOverload();
                throw; // Re-throw so EmailPollingService handles retry
            }

            // Download skill files after EACH iteration (before response gets overwritten)
            try
            {
                // Debug: Log container info
                if (response.Container != null)
                {
                    Logger.Log($"[Skill Download] Iteration {iteration}: Container ID = {response.Container.Id}", agent.Id);
                }

                // Try to get file IDs from this response
                var fileIds = response.GetFileIds();
                Logger.Log($"[Skill Download] Iteration {iteration}: Found {fileIds.Count()} file IDs", agent.Id);

                var iterationFiles = await response.DownloadFilesAsync(client, outputDirectory);
                if (iterationFiles.Any())
                {
                    Logger.Log($"[Skill Download] Iteration {iteration}: Downloaded {iterationFiles.Count()} files", agent.Id);
                    foreach (var f in iterationFiles)
                    {
                        Logger.Log($"  -> {f}", agent.Id);
                        if (!allDownloadedFiles.Contains(f))
                            allDownloadedFiles.Add(f);
                    }
                }
                else
                {
                    Logger.Log($"[Skill Download] Iteration {iteration}: No files downloaded", agent.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Skill Download] Iteration {iteration}: Error: {ex.Message}", agent.Id);
            }

            // Check for ALL tool calls in the response
            var allToolCalls = response.Content
                .OfType<ToolUseContent>()
                .ToList();

            // No tool calls at all → AI is done
            if (!allToolCalls.Any())
                break;

            // Add Claude response (with tool use) as assistant message
            messages.Add(response.Message);

            // Execute each tool call and send result back
            foreach (var toolUse in allToolCalls)
            {
                Logger.Log($"[MCP Tool Call] {toolUse.Name} - Input: {toolUse.Input}", agent.Id);

                try
                {
                    string result;

                    if (toolUse.Name == "send_email" && sendEmailServer != null)
                    {
                        // Handle send_email tool
                        var to = toolUse.Input?["to"]?.ToString() ?? "";
                        var subject = toolUse.Input?["subject"]?.ToString() ?? "";
                        var text = toolUse.Input?["text"]?.ToString() ?? "";
                        var html = toolUse.Input?["html"]?.ToString();
                        var replyTo = toolUse.Input?["reply_to"]?.ToString();

                        result = await sendEmailServer.SendEmailAsync(to, subject, text, html, replyTo);

                        // Track that this recipient already received an email
                        if (!string.IsNullOrEmpty(to))
                            sentEmailRecipients.Add(to);
                    }
                    else if (mcpToolHandlers.ContainsKey(toolUse.Name))
                    {
                        // Handle MCP tools
                        var jsonParam = new JsonStringParameter();
                        if (toolUse.Input != null && toolUse.Input.AsObject().ContainsKey("json"))
                        {
                            jsonParam.json = toolUse.Input["json"]?.ToString() ?? "{}";
                        }
                        else if (toolUse.Input != null)
                        {
                            jsonParam.json = toolUse.Input.ToJsonString();
                        }

                        result = await mcpToolHandlers[toolUse.Name](jsonParam);
                    }
                    else
                    {
                        // Server-side tools (bash, code_execution, web_search) that weren't processed
                        // Tell the AI to use the available custom tools instead
                        var availableTools = string.Join(", ", mcpToolHandlers.Keys.Append("send_email"));
                        result = $"ERROR: Tool '{toolUse.Name}' is not available for direct use. Please use the available tools instead: {availableTools}";
                        Logger.LogWarning($"[MCP] AI tried to use unavailable tool: {toolUse.Name}, available: {availableTools}", agent.Id);
                    }

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
                    Logger.LogError($"[MCP Tool Error] {toolUse.Name}: {ex.Message}", agent.Id);

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
        Logger.Log($"[Skill Download] Total downloaded files: {allDownloadedFiles.Count}", agent.Id);

        if (allDownloadedFiles.Any())
        {
            Logger.Log("----------------------------------------------", agent.Id);
            Logger.Log("Downloaded Files:", agent.Id);
            Logger.Log("----------------------------------------------", agent.Id);
            foreach (var filePath in allDownloadedFiles)
            {
                Logger.Log($"  {filePath}", agent.Id);
            }
        }

        // Extract text content
        var fullText = "";
        Logger.Log("----------------------------------------------", agent.Id);
        Logger.Log("All Content:", agent.Id);
        Logger.Log("----------------------------------------------", agent.Id);
        foreach (var content in response.Content)
        {
            Logger.Log($"Type: {content.GetType().Name}", agent.Id);

            if (content is TextContent textContent)
            {
                Logger.Log($"{textContent.Text}", agent.Id);
                fullText += textContent.Text + "\n";
            }
            else if (content is WebSearchToolResultContent webSearchContent)
            {
                Logger.Log($"Web Search Tool Result with {webSearchContent.Content.Count} items", agent.Id);
            }
            else if (content is ToolUseContent toolUse)
            {
                Logger.Log($"Tool Use: {toolUse.Name}", agent.Id);
            }
            else
            {
                // Log unknown content types for debugging
                Logger.Log($"[DEBUG] Content properties: {System.Text.Json.JsonSerializer.Serialize(content)}", agent.Id);
            }
        }

        // Extract JSON from response (Claude can write text before the JSON)
        var responseClass = ParseAiResponse(fullText);

        // Check if we need to generate a document with a second agent
        if (responseClass.MustCreateAttachment &&
            !string.IsNullOrEmpty(responseClass.AttachmentType) &&
            !string.IsNullOrEmpty(responseClass.AttachmentData))
        {
            Logger.Log($"[DocumentGenerator] MustCreateAttachment=true, Type={responseClass.AttachmentType}", agent.Id);
            Logger.Log($"[DocumentGenerator] Starting second agent for document generation...", agent.Id);

            try
            {
                var documentGenerator = new ClaudeGenerateDocumentsClass(_apikey, agent.Id);
                var generatedAttachment = await documentGenerator.GenerateDocumentAsync(
                    responseClass.AttachmentType,
                    responseClass.AttachmentData,
                    responseClass.AttachmentFilename
                );

                if (generatedAttachment != null)
                {
                    Logger.Log($"[DocumentGenerator] Successfully generated: {generatedAttachment.Filename}", agent.Id);

                    // Add the generated attachment to the response
                    var attachmentsList = responseClass.Attachments?.ToList() ?? new List<Attachment>();
                    attachmentsList.Add(generatedAttachment);
                    responseClass.Attachments = attachmentsList.ToArray();
                }
                else
                {
                    Logger.LogWarning("[DocumentGenerator] Document generation failed - no attachment created", agent.Id);

                    // Add note to explanation that attachment could not be generated
                    var note = "\n\n[Note: The requested document could not be created. However, the information is included in the email text.]";
                    responseClass.AiExplanation = (responseClass.AiExplanation ?? "") + note;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[DocumentGenerator] Error during document generation: {ex.Message}", agent.Id);
                Logger.LogError($"[DocumentGenerator] Stack trace: {ex.StackTrace}", agent.Id);

                // Add note to explanation that attachment could not be generated
                var note = "\n\n[Note: The requested document could not be created due to a technical error. However, the information is included in the email text.]";
                responseClass.AiExplanation = (responseClass.AiExplanation ?? "") + note;

                // Email will still be sent without the attachment
            }
        }

        // Debug: Log parsed attachments from JSON
        if (responseClass.Attachments != null && responseClass.Attachments.Length > 0)
        {
            Logger.Log($"[JSON Attachments] Found {responseClass.Attachments.Length} attachments in JSON:", agent.Id);
            foreach (var att in responseClass.Attachments)
            {
                Logger.Log($"  - Filename: {att.Filename}, Path: {att.Path}, HasContent: {!string.IsNullOrEmpty(att.Content)}", agent.Id);
            }
        }
        else
        {
            Logger.Log("[JSON Attachments] No attachments in JSON response", agent.Id);
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

                    Logger.Log("[Attachment] Added skill file: " + fileName + " (" + contentType + ")", agent.Id);
                }
                catch (Exception ex)
                {
                    Logger.LogError("[Attachment Error] Could not read file " + filePath + ": " + ex.Message, agent.Id);
                }
            }

            responseClass.Attachments = existingAttachments.ToArray();
        }

        // Transfer send_email tracking to response (for duplicate send prevention)
        responseClass.AlreadySentTo = sentEmailRecipients;

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

                        Logger.Log("[Attachment] Loaded from path: " + att.Filename, agent.Id);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[Attachment Error] Could not read " + att.Path + ": " + ex.Message, agent.Id);
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
        Logger.Log($"[ParseAiResponse] Attempting to parse response ({fullText?.Length ?? 0} characters)");

        // Check for empty or null response
        if (string.IsNullOrWhiteSpace(fullText))
        {
            Logger.LogWarning("[ParseAiResponse] Empty response from Claude!");
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
                Logger.Log("[ParseAiResponse] Found JSON block in markdown");
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
                                Logger.Log("[ParseAiResponse] Found valid JSON object");
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
            Logger.Log("[ParseAiResponse] Attempting to parse entire text as JSON");
            return JsonConvert.DeserializeObject<AiResponseClass>(fullText.Trim())
                   ?? new AiResponseClass { EmailResponseText = fullText.Trim() };
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ParseAiResponse] Failed to parse response: {ex.Message}");
            Logger.LogError($"[ParseAiResponse] Response content: {fullText.Substring(0, Math.Min(500, fullText.Length))}...");

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

    private static string CountryToIso2(string? country)
    {
        if (string.IsNullOrEmpty(country)) return "DE";

        // If already a 2-char code, return as-is
        if (country.Length == 2) return country.ToUpper();

        return country.ToLower() switch
        {
            "germany" or "deutschland" => "DE",
            "austria" or "österreich" => "AT",
            "switzerland" or "schweiz" => "CH",
            "albania" => "AL",
            "belgium" => "BE",
            "bosnia and herzegovina" => "BA",
            "bulgaria" => "BG",
            "croatia" => "HR",
            "cyprus" => "CY",
            "czech republic" => "CZ",
            "denmark" => "DK",
            "estonia" => "EE",
            "finland" => "FI",
            "france" => "FR",
            "greece" => "GR",
            "hungary" => "HU",
            "iceland" => "IS",
            "ireland" => "IE",
            "italy" => "IT",
            "latvia" => "LV",
            "liechtenstein" => "LI",
            "lithuania" => "LT",
            "luxembourg" => "LU",
            "malta" => "MT",
            "moldova" => "MD",
            "monaco" => "MC",
            "montenegro" => "ME",
            "netherlands" => "NL",
            "north macedonia" => "MK",
            "norway" => "NO",
            "poland" => "PL",
            "portugal" => "PT",
            "romania" => "RO",
            "serbia" => "RS",
            "slovakia" => "SK",
            "slovenia" => "SI",
            "spain" => "ES",
            "sweden" => "SE",
            "turkey" => "TR",
            "ukraine" => "UA",
            "united kingdom" => "GB",
            "united states" => "US",
            "canada" => "CA",
            _ => "DE"
        };
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
