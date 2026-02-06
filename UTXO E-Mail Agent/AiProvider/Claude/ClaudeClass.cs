using System.Text;
using Claude.AgentSdk;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent_Shared.Models;
using UTXO_E_Mail_Agent.McpServers;
using ClaudeSDK = Claude.AgentSdk.Claude;

namespace UTXO_E_Mail_Agent.AiProvider.Claude;

public class ClaudeClass : IAiProvider
{
    private readonly string _connectionString;

    public ClaudeClass(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AiResponseClass> GenerateResponse(string systemPrompt, string prompt, MailClass mailClass, Agent agent, Conversation conversation)
    {
        // MCP Server aus Datenbank laden
        var dbMcpServers = await McpServerLoader.LoadMcpServersForAgentAsync(agent.Id, conversation.Id, _connectionString);

        // Claude Agent SDK Optionen konfigurieren mit MCP Server
        var optionsBuilder = ClaudeSDK.Options()
            .SystemPrompt(systemPrompt)
            .Model(agent.Aimodel ?? "claude-sonnet-4-5-20250929")
            .AllowAllTools() // Alle Tools erlauben (MCP Servers, etc.)
            .AcceptEdits() // Automatisch Dateien schreiben (für Anhänge)
            .BypassPermissions()
            .MaxTurns(40); // Erhöht auf 40 für komplexe Tool-Chains (z.B. PDF-Erstellung)

        // MCP Server hinzufügen
        optionsBuilder.McpServers(m =>
        {
           // Dynamische MCP Server aus Datenbank
            if (dbMcpServers != null)
            {
                dbMcpServers(m);
            }
        });
       
        var options = optionsBuilder
            .OnStderr(stderr => Console.Error.WriteLine($"[Claude CLI stderr]: {stderr}"))
            .Build();

        // Response von Claude holen
        // Sammle alle Messages, um die letzte Text-Antwort nach Tool-Uses zu erhalten
        var messages = new List<global::Claude.AgentSdk.Message>();
        int turnCount = 0;

        await foreach (var message in ClaudeSDK.QueryAsync(prompt, options))
        {
            messages.Add(message);

            // Debug-Ausgabe für Entwicklung
            if (message is global::Claude.AgentSdk.AssistantMessage am)
            {
                turnCount++;
                Console.WriteLine($"[Claude Turn {turnCount}] AssistantMessage mit {am.Content.Count} Content-Blocks");
                foreach (var block in am.Content)
                {
                    if (block is global::Claude.AgentSdk.TextBlock tb)
                        Console.WriteLine($"  - TextBlock: {tb.Text.Substring(0, Math.Min(100, tb.Text.Length))}...");
                    else if (block is global::Claude.AgentSdk.ToolUseBlock tub)
                        Console.WriteLine($"  - ToolUseBlock: {tub.Name}");
                    else if (block is global::Claude.AgentSdk.ToolResultBlock trb)
                        Console.WriteLine($"  - ToolResultBlock");
                }
            }
            else if (message is global::Claude.AgentSdk.ResultMessage rm)
            {
                Console.WriteLine($"[Claude Finished] Conversation beendet nach {turnCount} Turns");
                // ResultMessage Properties können je nach SDK-Version variieren
                Console.WriteLine($"  - ResultMessage empfangen");
            }
        }

        Console.WriteLine($"[Claude] Alle Turns abgeschlossen. Insgesamt {messages.Count} Messages empfangen.");

        // Suche die letzte AssistantMessage mit TextBlock (nach allen Tool-Uses)
        string fullResponse = string.Empty;

        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i] is global::Claude.AgentSdk.AssistantMessage am)
            {
                var textBuilder = new StringBuilder();
                foreach (var block in am.Content)
                {
                    if (block is global::Claude.AgentSdk.TextBlock tb)
                    {
                        textBuilder.Append(tb.Text);
                    }
                }

                if (textBuilder.Length > 0)
                {
                    fullResponse = textBuilder.ToString();
                    Console.WriteLine($"[Claude] Verwende letzte AssistantMessage (Index {i}) mit {textBuilder.Length} Zeichen");
                    break;
                }
            }
        }

        // Fallback: Falls keine AssistantMessage mit Text gefunden wurde, sammle alle Texte
        if (string.IsNullOrEmpty(fullResponse))
        {
            var responseBuilder = new StringBuilder();
            foreach (var message in messages)
            {
                if (message is global::Claude.AgentSdk.AssistantMessage am)
                {
                    foreach (var block in am.Content)
                    {
                        if (block is global::Claude.AgentSdk.TextBlock tb)
                        {
                            responseBuilder.Append(tb.Text);
                        }
                    }
                }
            }
            fullResponse = responseBuilder.ToString();
            Console.WriteLine($"[Claude] Fallback: Verwende alle TextBlocks zusammen ({fullResponse.Length} Zeichen)");
        }

        // JSON aus der Response extrahieren (könnte in ```json ... ``` eingebettet sein)
        var result = ExtractJsonFromResponse(fullResponse);

        return result;
    }

    private AiResponseClass ExtractJsonFromResponse(string response)
    {
        string? explanation = null;
        string jsonContent;

        Console.WriteLine($"[Claude] Extrahiere JSON aus Response ({response.Length} Zeichen)");

        // Suche nach JSON in Markdown Code-Blöcken
        var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(
            response,
            @"```json\s*\n(.*?)\n```",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );

        if (jsonBlockMatch.Success)
        {
            // JSON gefunden in Code-Block
            jsonContent = jsonBlockMatch.Groups[1].Value.Trim();
            Console.WriteLine($"[Claude] JSON in Code-Block gefunden ({jsonContent.Length} Zeichen)");

            // Text vor dem JSON als Explanation speichern
            var textBeforeJson = response.Substring(0, jsonBlockMatch.Index).Trim();
            if (!string.IsNullOrWhiteSpace(textBeforeJson))
            {
                explanation = textBeforeJson;
                Console.WriteLine($"[Claude] Explanation gefunden: {textBeforeJson.Substring(0, Math.Min(100, textBeforeJson.Length))}...");
            }
        }
        else
        {
            // Kein Code-Block gefunden, versuche JSON-ähnlichen Content zu finden
            Console.WriteLine("[Claude] WARNUNG: Kein JSON Code-Block gefunden");

            // Suche nach JSON-Objekt im Text (beginnt mit { und endet mit })
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                response,
                @"\{[\s\S]*?\}",
                System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (jsonMatch.Success)
            {
                jsonContent = jsonMatch.Value.Trim();
                Console.WriteLine($"[Claude] JSON-Objekt im Text gefunden ({jsonContent.Length} Zeichen)");

                // Text vor dem JSON als Explanation
                var textBeforeJson = response.Substring(0, jsonMatch.Index).Trim();
                if (!string.IsNullOrWhiteSpace(textBeforeJson))
                {
                    explanation = textBeforeJson;
                }
            }
            else
            {
                // Letzter Versuch: gesamte Response als JSON
                jsonContent = response.Trim();
                Console.WriteLine("[Claude] Kein JSON gefunden, versuche gesamte Response zu parsen");
            }
        }

        try
        {
            var result = JsonConvert.DeserializeObject<AiResponseClass>(jsonContent);

            if (result == null)
            {
                throw new InvalidOperationException(
                    $"AI Response konnte nicht geparst werden (Deserialization returned null).\n" +
                    $"Vollständige Response:\n{response}"
                );
            }

            // Explanation hinzufügen falls vorhanden
            if (explanation != null)
            {
                result.AiExplanation = explanation;
            }

            Console.WriteLine("[Claude] JSON erfolgreich extrahiert und geparst");

            // Attachments verarbeiten: Dateien einlesen und in Base64 konvertieren
            if (result.Attachments != null && result.Attachments.Length > 0)
            {
                Console.WriteLine($"[Claude] Verarbeite {result.Attachments.Length} Attachment(s)");

                for (int i = 0; i < result.Attachments.Length; i++)
                {
                    var attachment = result.Attachments[i];

                    // Wenn path gesetzt ist, aber content leer, dann Datei einlesen
                    if (!string.IsNullOrEmpty(attachment.Path) && string.IsNullOrEmpty(attachment.Content))
                    {
                        try
                        {
                            Console.WriteLine($"[Claude] Lese Attachment {i + 1}: {attachment.Path}");

                            // Prüfe ob Datei existiert
                            if (!System.IO.File.Exists(attachment.Path))
                            {
                                Console.WriteLine($"[Claude] WARNUNG: Datei nicht gefunden: {attachment.Path}");
                                continue;
                            }

                            // Datei einlesen und in Base64 konvertieren
                            byte[] fileBytes = System.IO.File.ReadAllBytes(attachment.Path);
                            attachment.Content = Convert.ToBase64String(fileBytes);

                            Console.WriteLine($"[Claude] Attachment {i + 1} konvertiert: {fileBytes.Length} Bytes -> {attachment.Content.Length} Base64 Zeichen");

                            // Filename setzen falls nicht vorhanden
                            if (string.IsNullOrEmpty(attachment.Filename))
                            {
                                attachment.Filename = System.IO.Path.GetFileName(attachment.Path);
                            }

                            // Content-Type auto-erkennen falls nicht gesetzt
                            if (string.IsNullOrEmpty(attachment.ContentType))
                            {
                                attachment.ContentType = GetContentTypeFromExtension(attachment.Path);
                                Console.WriteLine($"[Claude] Content-Type auto-erkannt: {attachment.ContentType}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Claude] FEHLER beim Lesen von Attachment {i + 1}: {ex.Message}");
                            // Attachment bleibt mit leerem Content (wird später ignoriert oder führt zu Fehler)
                        }
                    }
                }
            }

            return result;
        }
        catch (JsonException ex)
        {
            var errorMessage = new StringBuilder();
            errorMessage.AppendLine("Fehler beim Parsen der AI Response als JSON.");
            errorMessage.AppendLine();
            errorMessage.AppendLine("JSON Content (erste 1000 Zeichen):");
            errorMessage.AppendLine(jsonContent.Substring(0, Math.Min(1000, jsonContent.Length)));
            errorMessage.AppendLine();
            errorMessage.AppendLine("Vollständige Response:");
            errorMessage.AppendLine(response);
            errorMessage.AppendLine();
            errorMessage.AppendLine($"JSON Fehler: {ex.Message}");

            Console.Error.WriteLine(errorMessage.ToString());

            throw new InvalidOperationException(errorMessage.ToString(), ex);
        }
    }

    private string GetContentTypeFromExtension(string filePath)
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
            ".html" => "text/html",
            ".htm" => "text/html",
            ".csv" => "text/csv",
            ".xml" => "text/xml",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream" // Fallback
        };
    }
}