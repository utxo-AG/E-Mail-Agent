using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.EmailProvider.Inbound.Classes;

namespace UTXO_E_Mail_Agent.AiProvider.ClaudeCode;

/// <summary>
/// AI Provider that uses Claude Code SDK via Python subprocess
/// Runs Claude Code locally for agentic email processing
/// </summary>
public class ClaudeCodeClass : IAiProvider
{
    private readonly string _pythonPath;
    private readonly string _pythonScriptPath;
    private readonly string _workingDirectory;
    private readonly string _mcpServerPath;
    private readonly IConfiguration _configuration;
    private static bool _mcpServerRegistered = false;
    private static readonly object _mcpLock = new object();

    public ClaudeCodeClass(IConfiguration configuration)
    {
        _configuration = configuration;
        var pythonPath = configuration["Python:Path"];
        _pythonPath = string.IsNullOrEmpty(pythonPath) ? GetDefaultPythonPath() : pythonPath;
        
        _pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "AiProvider", "ClaudeCode", "Python");
        
        var workingDir = configuration["ClaudeCode:WorkingDirectory"];
        _workingDirectory = string.IsNullOrEmpty(workingDir) 
            ? Path.Combine(Path.GetTempPath(), "claude_code_work") 
            : workingDir;
        
        var mcpPath = configuration["ClaudeCode:McpServerPath"];
        _mcpServerPath = string.IsNullOrEmpty(mcpPath) ? GetDefaultMcpServerPath() : mcpPath;
        
        // Ensure working directory exists
        Directory.CreateDirectory(_workingDirectory);
    }
    
    private static string GetDefaultPythonPath()
    {
        // Try to find Python based on OS
        if (OperatingSystem.IsWindows())
        {
            var paths = new[]
            {
                @"C:\Python314\python.exe",
                @"C:\Python313\python.exe",
                @"C:\Python312\python.exe",
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Python\Python314\python.exe"),
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Python\Python313\python.exe"),
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Python\Python312\python.exe"),
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            
            return "python"; // Fallback to PATH
        }
        else if (OperatingSystem.IsMacOS())
        {
            var paths = new[]
            {
                // Homebrew Intel
                "/usr/local/bin/python3",
                "/usr/local/opt/python@3.14/bin/python3.14",
                "/usr/local/opt/python@3.13/bin/python3.13",
                "/usr/local/opt/python@3.12/bin/python3.12",
                // Homebrew ARM (Apple Silicon)
                "/opt/homebrew/bin/python3",
                "/opt/homebrew/opt/python@3.14/bin/python3.14",
                "/opt/homebrew/opt/python@3.13/bin/python3.13",
                "/opt/homebrew/opt/python@3.12/bin/python3.12",
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            
            return "python3"; // Fallback to PATH
        }
        else // Linux
        {
            var paths = new[]
            {
                "/usr/bin/python3",
                "/usr/bin/python3.14",
                "/usr/bin/python3.13",
                "/usr/bin/python3.12",
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            
            return "python3"; // Fallback to PATH
        }
    }
    
    private static string GetDefaultMcpServerPath()
    {
        // Try to find the MCP server DLL relative to the application
        var possiblePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "UTXO E-Mail Agent McpServer.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "UTXO E-Mail Agent McpServer", "bin", "Debug", "net9.0", "UTXO E-Mail Agent McpServer.dll"),
            Path.Combine(AppContext.BaseDirectory, "..", "UTXO E-Mail Agent McpServer", "bin", "Release", "net9.0", "UTXO E-Mail Agent McpServer.dll"),
        };
        
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
                return fullPath;
        }
        
        // Fallback to relative path
        return Path.Combine(AppContext.BaseDirectory, "UTXO E-Mail Agent McpServer.dll");
    }
    
    private static string GetDotnetPath()
    {
        // Try to find dotnet based on OS
        if (OperatingSystem.IsWindows())
        {
            var paths = new[]
            {
                @"C:\Program Files\dotnet\dotnet.exe",
                Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\dotnet\dotnet.exe"),
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            
            return "dotnet"; // Fallback to PATH
        }
        else if (OperatingSystem.IsMacOS())
        {
            var paths = new[]
            {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            
            return "dotnet"; // Fallback to PATH
        }
        else // Linux
        {
            var paths = new[]
            {
                "/usr/share/dotnet/dotnet",
                "/usr/bin/dotnet",
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
            
            return "dotnet"; // Fallback to PATH
        }
    }

    /// <summary>
    /// Installs skills into the conversation working directory.
    /// The SDK looks for skills in .claude/skills/ relative to the working directory.
    /// ZIP files are extracted, MD files are copied directly.
    /// </summary>
    private void InstallSkillsToWorkingDirectory(Agent agent, string convWorkDir)
    {
        if (agent.Skills == null || agent.Skills.Count == 0)
        {
            Logger.Log($"[ClaudeCode] No skills to install for agent {agent.Id}", agent.Id);
            return;
        }

        // Create .claude/skills/ directory in the conversation working directory
        var localSkillsDir = Path.Combine(convWorkDir, ".claude", "skills");
        Directory.CreateDirectory(localSkillsDir);
        
        Logger.Log($"[ClaudeCode] Installing {agent.Skills.Count} skill(s) to {localSkillsDir}", agent.Id);

        foreach (var skill in agent.Skills)
        {
            if (skill.Skillfiles == null || skill.Skillfiles.Length == 0)
            {
                Logger.Log($"[ClaudeCode] Skill '{skill.Skillname}' has no files, skipping", agent.Id);
                continue;
            }

            var skillDir = Path.Combine(localSkillsDir, skill.Skillname);

            try
            {
                // Create skill directory
                Directory.CreateDirectory(skillDir);

                if (skill.Filetype?.ToLower() == "zip")
                {
                    // Extract ZIP file
                    using var zipStream = new MemoryStream(skill.Skillfiles);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                    
                    // Detect if ZIP has a root folder that matches skill name (common pattern)
                    // If so, we need to strip it to avoid: skills/review-summarizer/review-summarizer/SKILL.md
                    var rootFolder = "";
                    var firstEntry = archive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name));
                    if (firstEntry != null && firstEntry.FullName.Contains('/'))
                    {
                        var potentialRoot = firstEntry.FullName.Split('/')[0];
                        // Check if all entries start with this root folder
                        if (archive.Entries.All(e => string.IsNullOrEmpty(e.FullName) || e.FullName.StartsWith(potentialRoot + "/")))
                        {
                            rootFolder = potentialRoot + "/";
                            Logger.Log($"[ClaudeCode] Detected root folder in ZIP: '{potentialRoot}', stripping it", agent.Id);
                        }
                    }
                    
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories
                        
                        // Strip root folder from path if present
                        var relativePath = entry.FullName;
                        if (!string.IsNullOrEmpty(rootFolder) && relativePath.StartsWith(rootFolder))
                        {
                            relativePath = relativePath.Substring(rootFolder.Length);
                        }
                        
                        if (string.IsNullOrEmpty(relativePath)) continue;
                        
                        var entryPath = Path.Combine(skillDir, relativePath);
                        var entryDir = Path.GetDirectoryName(entryPath);
                        if (!string.IsNullOrEmpty(entryDir))
                        {
                            Directory.CreateDirectory(entryDir);
                        }
                        
                        entry.ExtractToFile(entryPath, overwrite: true);
                    }
                    
                    Logger.Log($"[ClaudeCode] Extracted ZIP skill '{skill.Skillname}' to {skillDir}", agent.Id);
                }
                else
                {
                    // Single MD file - write directly as SKILL.md
                    var skillMdPath = Path.Combine(skillDir, "SKILL.md");
                    File.WriteAllBytes(skillMdPath, skill.Skillfiles);
                    Logger.Log($"[ClaudeCode] Installed MD skill '{skill.Skillname}' to {skillMdPath}", agent.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ClaudeCode] Failed to install skill '{skill.Skillname}': {ex.Message}", agent.Id);
            }
        }
    }

    /// <summary>
    /// Registers the UTXO MCP Server in Claude Code's settings if the agent has MCP servers configured.
    /// </summary>
    private void RegisterMcpServerIfNeeded(Agent agent)
    {
        if (agent.Mcpservers == null || agent.Mcpservers.Count == 0)
        {
            return;
        }

        lock (_mcpLock)
        {
            if (_mcpServerRegistered)
            {
                return;
            }

            try
            {
                var claudeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
                var settingsPath = Path.Combine(claudeDir, "settings.json");
                
                Directory.CreateDirectory(claudeDir);

                // Read existing settings or create new
                Dictionary<string, object>? settings = null;
                if (File.Exists(settingsPath))
                {
                    var existingJson = File.ReadAllText(settingsPath);
                    settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(existingJson);
                }
                settings ??= new Dictionary<string, object>();

                // Get or create mcpServers section
                Dictionary<string, object> mcpServers;
                if (settings.TryGetValue("mcpServers", out var existing) && existing is System.Text.Json.JsonElement jsonElement)
                {
                    mcpServers = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText()) 
                                 ?? new Dictionary<string, object>();
                }
                else
                {
                    mcpServers = new Dictionary<string, object>();
                }

                // Add our MCP server if not already present
                const string serverName = "utxo-http";
                if (!mcpServers.ContainsKey(serverName))
                {
                    mcpServers[serverName] = new Dictionary<string, object>
                    {
                        { "command", GetDotnetPath() },
                        { "args", new[] { _mcpServerPath } }
                    };

                    settings["mcpServers"] = mcpServers;

                    // Write back settings
                    var options = new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    };
                    var json = System.Text.Json.JsonSerializer.Serialize(settings, options);
                    File.WriteAllText(settingsPath, json);

                    Logger.Log($"[ClaudeCode] Registered MCP server 'utxo-http' in {settingsPath}", agent.Id);
                }
                else
                {
                    Logger.Log($"[ClaudeCode] MCP server 'utxo-http' already registered", agent.Id);
                }

                _mcpServerRegistered = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ClaudeCode] Failed to register MCP server: {ex.Message}", agent.Id);
            }
        }
    }

    /// <summary>
    /// Builds documentation for available API endpoints from the agent's MCP servers.
    /// This is added to the system prompt so Claude knows which APIs to call.
    /// </summary>
    private static string BuildApiDocumentation(Agent agent)
    {
        if (agent.Mcpservers == null || agent.Mcpservers.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("\n\n--- VERFÜGBARE APIs ---");
        sb.AppendLine("Du hast Zugriff auf folgende APIs. Verwende curl via Bash um sie aufzurufen.");
        sb.AppendLine();
        
        // Critical forwarding instruction
        sb.AppendLine("🚨 WICHTIG FÜR ALLE API-AUFRUFE MIT E-MAIL-INHALTEN:");
        sb.AppendLine("Bei curl-Aufrufen an send_email API MUSS der VOLLSTÄNDIGE E-Mail-Inhalt übergeben werden!");
        sb.AppendLine("- NIEMALS den text/html Parameter kürzen oder zusammenfassen!");
        sb.AppendLine("- Der GESAMTE Original-Inhalt muss 1:1 im JSON-Body stehen!");
        sb.AppendLine();

        foreach (var api in agent.Mcpservers)
        {
            sb.AppendLine($"### {api.Name}");
            sb.AppendLine($"- **Beschreibung:** {api.Description}");
            sb.AppendLine($"- **URL:** {api.Url}");
            sb.AppendLine($"- **Methode:** {api.Call.ToUpper()}");
            
            // Build curl example
            var curlCmd = new StringBuilder("curl -s");
            
            // Add method if not GET
            if (!api.Call.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                curlCmd.Append($" -X {api.Call.ToUpper()}");
            }
            
            // Add Bearer token if present
            if (!string.IsNullOrEmpty(api.Bearer))
            {
                sb.AppendLine($"- **Authentifizierung:** Bearer Token (bereits konfiguriert)");
                curlCmd.Append($" -H \"Authorization: Bearer {api.Bearer}\"");
            }
            
            // Add Content-Type for POST/PUT/PATCH
            if (api.Call.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                api.Call.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                api.Call.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
            {
                curlCmd.Append(" -H \"Content-Type: application/json\"");
                curlCmd.Append(" -d '<JSON_DATA>'");
            }
            
            curlCmd.Append($" \"{api.Url}\"");
            
            sb.AppendLine($"- **curl-Befehl:**");
            sb.AppendLine($"  ```");
            sb.AppendLine($"  {curlCmd}");
            sb.AppendLine($"  ```");
            sb.AppendLine();
        }

        sb.AppendLine("--- END VERFÜGBARE APIs ---");

        return sb.ToString();
    }

    public async Task<AiResponseClass> GenerateResponse(
        string systemPrompt, 
        string prompt, 
        MailClass mailClass, 
        Agent agent, 
        Conversation conversation, 
        List<Conversation>? conversationHistory = null)
    {
        Logger.Log($"[ClaudeCode] Processing email: {mailClass.Subject} (MessageId: {mailClass.OriginalMessageId ?? mailClass.Id})", agent.Id);
        
        // Register MCP server if agent has API endpoints configured
        RegisterMcpServerIfNeeded(agent);

        try
        {
            // Create a unique working directory for this conversation
            var convWorkDir = Path.Combine(_workingDirectory, $"conv_{conversation.Id}");
            Directory.CreateDirectory(convWorkDir);
            
            // Create output directory for generated files (PDF, HTML, etc.)
            var outputDir = Path.Combine(convWorkDir, "output");
            Directory.CreateDirectory(outputDir);
            
            // Install skills into the conversation working directory
            // The SDK looks for skills in .claude/skills/ relative to the working directory
            InstallSkillsToWorkingDirectory(agent, convWorkDir);
            
            // Build conversation history into system prompt if available
            var fullSystemPrompt = systemPrompt;
            
            // Add file output instruction with ABSOLUTE path - Claude ignores relative paths
            fullSystemPrompt += "\n\n--- WICHTIG: DATEI-AUSGABE ---\n";
            fullSystemPrompt += $"Wenn du Dateien erstellst (HTML, PDF, Bilder, etc.), speichere sie IMMER im Verzeichnis: {outputDir}\n";
            fullSystemPrompt += $"Beispiel: '{Path.Combine(outputDir, "report.html")}', '{Path.Combine(outputDir, "report.pdf")}'\n";
            fullSystemPrompt += "Nur Dateien in diesem Verzeichnis werden als E-Mail-Anhänge erkannt und versendet.\n";
            fullSystemPrompt += "--- ENDE DATEI-AUSGABE ---\n";
            
            // Add API documentation if agent has MCP servers
            fullSystemPrompt += BuildApiDocumentation(agent);
            
            if (conversationHistory != null && conversationHistory.Count > 0)
            {
                fullSystemPrompt += "\n\n--- CONVERSATION HISTORY ---\n";
                foreach (var prevConv in conversationHistory)
                {
                    fullSystemPrompt += $"\nPrevious Email from {prevConv.Emailfrom}:\n";
                    fullSystemPrompt += $"Subject: {prevConv.Subject}\n";
                    fullSystemPrompt += $"Text: {prevConv.Text}\n";
                    if (!string.IsNullOrEmpty(prevConv.Aifullresponse))
                    {
                        fullSystemPrompt += $"Your Response: {prevConv.Aifullresponse}\n";
                    }
                    fullSystemPrompt += "---\n";
                }
                Logger.Log($"[ClaudeCode] Added {conversationHistory.Count} previous conversations to context", agent.Id);
            }

            // Prepare input data as JSON file (easier than command line args for large data)
            // Calculate attachments directory path
            var attachmentsDir = Path.Combine(Path.GetTempPath(), "attachments", agent.Id.ToString(), mailClass.Id ?? "unknown");
            
            var inputData = new
            {
                system_prompt = fullSystemPrompt,
                user_prompt = prompt,
                email_subject = mailClass.Subject ?? "(No Subject)",
                email_text = mailClass.Text ?? "",
                email_html = mailClass.Html,
                email_from = mailClass.From ?? "(Unknown)",
                message_id = mailClass.Id ?? "",
                attachments = mailClass.Attachments ?? Array.Empty<string>(),
                has_attachments = mailClass.HasAttachments ?? false,
                attachments_directory = attachmentsDir,
                agent_id = agent.Id,
                working_directory = convWorkDir,
                max_turns = 20,
                model = agent.Aimodel  // Pass model to Claude Code SDK
            };

            var inputJsonPath = Path.Combine(convWorkDir, "input.json");
            var inputJson = JsonConvert.SerializeObject(inputData, Formatting.Indented);
            await File.WriteAllTextAsync(inputJsonPath, inputJson);

            var scriptPath = Path.Combine(_pythonScriptPath, "claude_agent_runner.py");
            
            Logger.Log($"[ClaudeCode] Running Python script: {_pythonPath} {scriptPath}", agent.Id);
            Logger.Log($"[ClaudeCode] Input file: {inputJsonPath}", agent.Id);

            // Run Python script as subprocess
            var psi = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{scriptPath}\" \"{inputJsonPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = convWorkDir,
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using var process = Process.Start(psi);
            if (process == null)
            {
                throw new Exception("Failed to start Python process");
            }

            // Read output and error streams
            var outputTask = Task.Run(async () =>
            {
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = await process.StandardOutput.ReadLineAsync();
                    if (line != null)
                    {
                        outputBuilder.AppendLine(line);
                    }
                }
            });

            var errorTask = Task.Run(async () =>
            {
                while (!process.StandardError.EndOfStream)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (line != null)
                    {
                        errorBuilder.AppendLine(line);
                        Logger.Log($"[ClaudeCode][stderr] {line}", agent.Id);
                    }
                }
            });

            await process.WaitForExitAsync();
            await Task.WhenAll(outputTask, errorTask);

            var resultJson = outputBuilder.ToString().Trim();
            
            Logger.Log($"[ClaudeCode] Process exited with code: {process.ExitCode}", agent.Id);

            if (process.ExitCode != 0)
            {
                var errorMsg = errorBuilder.ToString();
                Logger.LogError($"[ClaudeCode] Python script failed: {errorMsg}", agent.Id);
                throw new Exception($"Python script failed with exit code {process.ExitCode}: {errorMsg}");
            }

            // Parse the Python response
            var pythonResult = JsonConvert.DeserializeObject<ClaudeCodePythonResponse>(resultJson);

            if (pythonResult == null || !pythonResult.Success)
            {
                var errorMsg = pythonResult?.Error ?? "Unknown error from Claude Code";
                Logger.LogError($"[ClaudeCode] Error: {errorMsg}", agent.Id);
                
                var lang = GlobalFunctions.DetectLanguage(mailClass.Text) ?? agent.Defaultlanguage ?? "de";
                var (fallbackText, fallbackSubject) = GlobalFunctions.GetFallbackMessages(lang);
                
                return new AiResponseClass
                {
                    EmailResponseText = fallbackText,
                    EmailResponseSubject = fallbackSubject,
                    EmailResponseHtml = $"<p>{System.Web.HttpUtility.HtmlEncode(fallbackText)}</p>",
                    Attachments = Array.Empty<Attachment>(),
                    AiExplanation = $"[ClaudeCode Error] {errorMsg}"
                };
            }

            // Parse the AI response from the full response text
            var responseClass = ParseAiResponse(
                pythonResult.FullResponse ?? pythonResult.ResponseText ?? "", 
                agent.Defaultlanguage, 
                mailClass.Text
            );

            // Store full response for conversation history
            responseClass.FullResponse = pythonResult.FullResponse ?? pythonResult.ResponseText;
            
            // Store cost, duration and token usage
            responseClass.AiCostUsd = pythonResult.TotalCostUsd;
            responseClass.AiDurationMs = pythonResult.TotalDurationMs;
            responseClass.AiInputTokens = pythonResult.TotalInputTokens;
            responseClass.AiOutputTokens = pythonResult.TotalOutputTokens;
            
            Logger.Log($"[ClaudeCode] Cost: ${pythonResult.TotalCostUsd:F4}, Duration: {pythonResult.TotalDurationMs}ms, Tokens: {pythonResult.TotalInputTokens} in / {pythonResult.TotalOutputTokens} out", agent.Id);

            // Process any files created by Claude Code
            if (pythonResult.FilesCreated != null && pythonResult.FilesCreated.Count > 0)
            {
                var attachments = responseClass.Attachments?.ToList() ?? new List<Attachment>();
                
                foreach (var filePath in pythonResult.FilesCreated)
                {
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var fileBytes = await File.ReadAllBytesAsync(filePath);
                            var fileName = Path.GetFileName(filePath);
                            var contentType = GlobalFunctions.GetContentTypeFromExtension(filePath);

                            attachments.Add(new Attachment
                            {
                                Filename = fileName,
                                Content = Convert.ToBase64String(fileBytes),
                                ContentType = contentType,
                                Path = filePath
                            });

                            Logger.Log($"[ClaudeCode] Added attachment: {fileName}", agent.Id);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[ClaudeCode] Failed to read file {filePath}: {ex.Message}", agent.Id);
                        }
                    }
                }
                
                responseClass.Attachments = attachments.ToArray();
            }

            // Store working directory for cleanup after email is sent
            responseClass.WorkingDirectory = convWorkDir;

            // Cleanup input file
            try { File.Delete(inputJsonPath); } catch { /* ignore */ }

            Logger.Log($"[ClaudeCode] Response generated successfully", agent.Id);
            return responseClass;
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ClaudeCode] Exception: {ex.Message}", agent.Id);
            Logger.LogError($"[ClaudeCode] Stack: {ex.StackTrace}", agent.Id);
            
            var lang = GlobalFunctions.DetectLanguage(mailClass.Text) ?? agent.Defaultlanguage ?? "de";
            var (fallbackText, fallbackSubject) = GlobalFunctions.GetFallbackMessages(lang);
            
            return new AiResponseClass
            {
                EmailResponseText = fallbackText,
                EmailResponseSubject = fallbackSubject,
                EmailResponseHtml = $"<p>{System.Web.HttpUtility.HtmlEncode(fallbackText)}</p>",
                Attachments = Array.Empty<Attachment>(),
                AiExplanation = $"[ClaudeCode Exception] {ex.Message}"
            };
        }
    }

    private AiResponseClass ParseAiResponse(string fullResponse, string? defaultLanguage, string? emailText)
    {
        // Try to extract structured response from the full response
        var responseClass = new AiResponseClass
        {
            Attachments = Array.Empty<Attachment>()
        };

        // First, try to find JSON block (same as ClaudeClass)
        var jsonPatterns = new[]
        {
            @"```json\s*([\s\S]*?)\s*```",           // Standard ```json ... ```
            @"```JSON\s*([\s\S]*?)\s*```",           // Uppercase JSON
            @"```\s*\n?\s*(\{[\s\S]*?\})\s*```",     // Just ``` with JSON inside
        };

        foreach (var pattern in jsonPatterns)
        {
            var jsonBlockMatch = Regex.Match(fullResponse, pattern, RegexOptions.IgnoreCase);
            if (jsonBlockMatch.Success)
            {
                var jsonContent = jsonBlockMatch.Groups[1].Value.Trim();
                try
                {
                    var parsed = JsonConvert.DeserializeObject<AiResponseClass>(jsonContent);
                    if (parsed != null)
                    {
                        // Save text before JSON as AiExplanation if not already set
                        var matchIndex = fullResponse.IndexOf(jsonBlockMatch.Value, StringComparison.Ordinal);
                        if (matchIndex > 0 && string.IsNullOrEmpty(parsed.AiExplanation))
                        {
                            var beforeJson = fullResponse[..matchIndex].Trim();
                            if (!string.IsNullOrEmpty(beforeJson))
                            {
                                parsed.AiExplanation = beforeJson;
                            }
                        }
                        parsed.Attachments ??= Array.Empty<Attachment>();
                        return parsed;
                    }
                }
                catch
                {
                    // JSON parse failed, continue with other patterns
                }
            }
        }

        // Look for EMAIL_RESPONSE markers (legacy format)
        var responseMatch = Regex.Match(fullResponse, @"<EMAIL_RESPONSE>([\s\S]*?)</EMAIL_RESPONSE>", RegexOptions.IgnoreCase);
        if (responseMatch.Success)
        {
            var emailContent = responseMatch.Groups[1].Value.Trim();
            
            // Extract subject
            var subjectMatch = Regex.Match(emailContent, @"<SUBJECT>([\s\S]*?)</SUBJECT>", RegexOptions.IgnoreCase);
            if (subjectMatch.Success)
            {
                responseClass.EmailResponseSubject = subjectMatch.Groups[1].Value.Trim();
            }
            
            // Extract body
            var bodyMatch = Regex.Match(emailContent, @"<BODY>([\s\S]*?)</BODY>", RegexOptions.IgnoreCase);
            if (bodyMatch.Success)
            {
                responseClass.EmailResponseText = bodyMatch.Groups[1].Value.Trim();
            }
            else
            {
                // If no BODY tag, use the content without SUBJECT
                responseClass.EmailResponseText = Regex.Replace(emailContent, @"<SUBJECT>[\s\S]*?</SUBJECT>", "", RegexOptions.IgnoreCase).Trim();
            }
        }
        else
        {
            // No structured response, use the full response
            responseClass.EmailResponseText = fullResponse;
            responseClass.EmailResponseSubject = "Re: Your inquiry";
        }

        // Generate HTML from text
        if (!string.IsNullOrEmpty(responseClass.EmailResponseText))
        {
            responseClass.EmailResponseHtml = $"<p>{System.Web.HttpUtility.HtmlEncode(responseClass.EmailResponseText).Replace("\n", "<br/>")}</p>";
        }

        // Extract explanation if present
        var explanationMatch = Regex.Match(fullResponse, @"<EXPLANATION>([\s\S]*?)</EXPLANATION>", RegexOptions.IgnoreCase);
        if (explanationMatch.Success)
        {
            responseClass.AiExplanation = explanationMatch.Groups[1].Value.Trim();
        }

        return responseClass;
    }
}

/// <summary>
/// Response structure from the Python Claude Code runner
/// </summary>
public class ClaudeCodePythonResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }
    
    [JsonProperty("response_text")]
    public string? ResponseText { get; set; }
    
    [JsonProperty("full_response")]
    public string? FullResponse { get; set; }
    
    [JsonProperty("files_created")]
    public List<string>? FilesCreated { get; set; }
    
    [JsonProperty("total_cost_usd")]
    public decimal TotalCostUsd { get; set; }
    
    [JsonProperty("total_duration_ms")]
    public long TotalDurationMs { get; set; }
    
    [JsonProperty("total_input_tokens")]
    public long TotalInputTokens { get; set; }
    
    [JsonProperty("total_output_tokens")]
    public long TotalOutputTokens { get; set; }
    
    [JsonProperty("error")]
    public string? Error { get; set; }
}
