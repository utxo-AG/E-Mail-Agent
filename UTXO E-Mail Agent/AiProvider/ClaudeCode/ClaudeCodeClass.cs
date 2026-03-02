using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Python.Runtime;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.Services;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.AiProvider.ClaudeCode;

/// <summary>
/// AI Provider that uses Claude Code SDK via Python.NET
/// Runs Claude Code locally for agentic email processing
/// </summary>
public class ClaudeCodeClass : IAiProvider
{
    private readonly string _apiKey;
    private readonly string _pythonDll;
    private readonly string _pythonScriptPath;
    private readonly string _workingDirectory;
    private static bool _pythonInitialized = false;
    private static readonly object _initLock = new object();

    public ClaudeCodeClass(string apiKey, IConfiguration configuration)
    {
        _apiKey = apiKey;
        _pythonDll = configuration["Python:DllPath"] ?? GetDefaultPythonDll();
        _pythonScriptPath = Path.Combine(AppContext.BaseDirectory, "AiProvider", "ClaudeCode", "Python");
        _workingDirectory = configuration["ClaudeCode:WorkingDirectory"] ?? Path.Combine(Path.GetTempPath(), "claude_code_work");
        
        // Ensure working directory exists
        Directory.CreateDirectory(_workingDirectory);
    }

    private static string GetDefaultPythonDll()
    {
        // Try to find Python DLL based on OS
        if (OperatingSystem.IsWindows())
        {
            // Common Windows paths
            var paths = new[]
            {
                @"C:\Python312\python312.dll",
                @"C:\Python311\python311.dll",
                @"C:\Python310\python310.dll",
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Python\Python312\python312.dll"),
                Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Programs\Python\Python311\python311.dll"),
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var paths = new[]
            {
                "/opt/homebrew/opt/python@3.12/Frameworks/Python.framework/Versions/3.12/lib/libpython3.12.dylib",
                "/opt/homebrew/opt/python@3.11/Frameworks/Python.framework/Versions/3.11/lib/libpython3.11.dylib",
                "/usr/local/opt/python@3.12/Frameworks/Python.framework/Versions/3.12/lib/libpython3.12.dylib",
                "/usr/local/opt/python@3.11/Frameworks/Python.framework/Versions/3.11/lib/libpython3.11.dylib",
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
        }
        else // Linux
        {
            var paths = new[]
            {
                "/usr/lib/x86_64-linux-gnu/libpython3.12.so",
                "/usr/lib/x86_64-linux-gnu/libpython3.11.so",
                "/usr/lib/x86_64-linux-gnu/libpython3.10.so",
                "/usr/lib/libpython3.12.so",
                "/usr/lib/libpython3.11.so",
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path)) return path;
            }
        }

        throw new FileNotFoundException("Could not find Python DLL. Please set Python:DllPath in configuration.");
    }

    private void InitializePython()
    {
        lock (_initLock)
        {
            if (_pythonInitialized) return;

            try
            {
                Runtime.PythonDLL = _pythonDll;
                PythonEngine.Initialize();
                _pythonInitialized = true;
                Logger.Log($"[ClaudeCode] Python initialized with DLL: {_pythonDll}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ClaudeCode] Failed to initialize Python: {ex.Message}");
                throw;
            }
        }
    }

    public async Task<AiResponseClass> GenerateResponse(
        string systemPrompt, 
        string prompt, 
        MailClass mailClass, 
        Agent agent, 
        Conversation conversation, 
        List<Conversation>? conversationHistory = null)
    {
        Logger.Log($"[ClaudeCode] Processing email: {mailClass.Subject}", agent.Id);

        try
        {
            InitializePython();

            // Build conversation history into system prompt if available
            var fullSystemPrompt = systemPrompt;
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

            // Create a unique working directory for this conversation
            var convWorkDir = Path.Combine(_workingDirectory, $"conv_{conversation.Id}");
            Directory.CreateDirectory(convWorkDir);

            string resultJson;
            
            using (Py.GIL())
            {
                // Add script path to Python path
                dynamic sys = Py.Import("sys");
                sys.path.append(_pythonScriptPath);

                // Import our runner module
                dynamic runner = Py.Import("claude_agent_runner");

                // Call the email agent function
                resultJson = runner.run_email_agent(
                    fullSystemPrompt,
                    prompt,
                    mailClass.Subject ?? "(No Subject)",
                    mailClass.Text ?? "",
                    mailClass.Html,
                    mailClass.From ?? "(Unknown)",
                    convWorkDir,
                    20,  // max_turns
                    _apiKey
                );
            }

            Logger.Log($"[ClaudeCode] Received response from Python", agent.Id);

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
                            Logger.LogWarning($"[ClaudeCode] Could not read file {filePath}: {ex.Message}", agent.Id);
                        }
                    }
                }

                responseClass.Attachments = attachments.ToArray();
            }

            // Cleanup working directory (optional - keep for debugging)
            // Directory.Delete(convWorkDir, true);

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

    /// <summary>
    /// Extracts JSON response from Claude Code output
    /// </summary>
    private static AiResponseClass ParseAiResponse(string fullText, string agentDefaultLanguage, string? emailText)
    {
        Logger.Log($"[ClaudeCode.ParseAiResponse] Parsing response ({fullText?.Length ?? 0} characters)");

        if (string.IsNullOrWhiteSpace(fullText))
        {
            var lang = GlobalFunctions.DetectLanguage(emailText) ?? agentDefaultLanguage ?? "de";
            var (fallbackText, fallbackSubject) = GlobalFunctions.GetFallbackMessages(lang);
            return new AiResponseClass
            {
                EmailResponseText = fallbackText,
                EmailResponseSubject = fallbackSubject,
                EmailResponseHtml = $"<p>{System.Web.HttpUtility.HtmlEncode(fallbackText)}</p>",
                Attachments = Array.Empty<Attachment>()
            };
        }

        try
        {
            // Try to find JSON block in response
            var jsonPatterns = new[]
            {
                @"```json\s*([\s\S]*?)\s*```",
                @"```JSON\s*([\s\S]*?)\s*```",
                @"```\s*\n?\s*(\{[\s\S]*?\})\s*```",
            };

            foreach (var pattern in jsonPatterns)
            {
                var match = Regex.Match(fullText, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var jsonContent = match.Groups[1].Value.Trim();
                    try
                    {
                        var parsed = JsonConvert.DeserializeObject<AiResponseClass>(jsonContent);
                        if (parsed != null && !string.IsNullOrEmpty(parsed.EmailResponseText))
                        {
                            var matchIndex = fullText.IndexOf(match.Value, StringComparison.Ordinal);
                            if (matchIndex > 0)
                            {
                                parsed.AiExplanation = fullText[..matchIndex].Trim();
                            }
                            return parsed;
                        }
                    }
                    catch { /* Continue to next pattern */ }
                }
            }

            // Fallback: try to find raw JSON object
            var lastBrace = fullText.LastIndexOf('}');
            if (lastBrace >= 0)
            {
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
                                parsed.AiExplanation = fullText[..i].Trim();
                                return parsed;
                            }
                        }
                        catch { /* Not valid JSON */ }
                    }
                }
            }

            // Ultimate fallback
            var fallbackLang = GlobalFunctions.DetectLanguage(emailText) ?? agentDefaultLanguage ?? "de";
            var (fallbackText, fallbackSubject) = GlobalFunctions.GetFallbackMessages(fallbackLang);
            return new AiResponseClass
            {
                EmailResponseText = fallbackText,
                EmailResponseSubject = fallbackSubject,
                EmailResponseHtml = $"<p>{System.Web.HttpUtility.HtmlEncode(fallbackText)}</p>",
                Attachments = Array.Empty<Attachment>(),
                AiExplanation = $"[Parse Error] Could not extract JSON. Raw: {fullText[..Math.Min(500, fullText.Length)]}..."
            };
        }
        catch (Exception ex)
        {
            var lang = GlobalFunctions.DetectLanguage(emailText) ?? agentDefaultLanguage ?? "de";
            var (fallbackText, fallbackSubject) = GlobalFunctions.GetFallbackMessages(lang);
            return new AiResponseClass
            {
                EmailResponseText = fallbackText,
                EmailResponseSubject = fallbackSubject,
                EmailResponseHtml = $"<p>{System.Web.HttpUtility.HtmlEncode(fallbackText)}</p>",
                Attachments = Array.Empty<Attachment>(),
                AiExplanation = $"[Parse Exception] {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Shutdown Python engine (call on application exit)
    /// </summary>
    public static void Shutdown()
    {
        lock (_initLock)
        {
            if (_pythonInitialized)
            {
                PythonEngine.Shutdown();
                _pythonInitialized = false;
                Logger.Log("[ClaudeCode] Python engine shutdown");
            }
        }
    }
}

/// <summary>
/// Response structure from Python claude_agent_runner
/// </summary>
internal class ClaudeCodePythonResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("response_text")]
    public string? ResponseText { get; set; }

    [JsonProperty("full_response")]
    public string? FullResponse { get; set; }

    [JsonProperty("files_created")]
    public List<string>? FilesCreated { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}
