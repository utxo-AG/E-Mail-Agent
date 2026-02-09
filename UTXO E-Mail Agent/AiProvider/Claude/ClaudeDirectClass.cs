using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UTXO_E_Mail_Agent.Classes;
using UTXO_E_Mail_Agent.Interfaces;
using UTXO_E_Mail_Agent.McpServers;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.AiProvider.Claude;

/// <summary>
/// Alternative Claude implementation that calls Claude CLI directly
/// without using the SDK - more compatible with different Claude CLI versions
/// </summary>
public class ClaudeDirectClass : IAiProvider
{
    private readonly string _connectionString;

    public ClaudeDirectClass(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<AiResponseClass> GenerateResponse(string systemPrompt, string prompt, MailClass mailClass, Agent agent, Conversation conversation)
    {
        try
        {
            // Prepare MCP server configuration as JSON
            var mcpServers = await McpServerLoader.GetMcpServersForAgentAsync(agent.Id, _connectionString);

            // Build command line arguments
            var args = new List<string>();

            // Add system prompt
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                args.Add("--system-prompt");
                args.Add($"\"{systemPrompt}\"");
            }

            // Add model
            args.Add("--model");
            args.Add(agent.Aimodel ?? "claude-sonnet-4-5-20250929");

            // Add other options
            args.Add("--allow-all-tools");
            args.Add("--accept-edits");
            args.Add("--max-turns");
            args.Add("40");

            // Create MCP server config file if needed
            if (mcpServers != null && mcpServers.Any())
            {
                var mcpConfigPath = Path.GetTempFileName();
                await CreateMcpConfigFile(mcpConfigPath, mcpServers, conversation.Id);
                args.Add("--mcp-server-config");
                args.Add($"\"{mcpConfigPath}\"");
            }

            // Prepare the process
            var processInfo = new ProcessStartInfo
            {
                FileName = "claude",
                Arguments = string.Join(" ", args),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            using var process = new Process { StartInfo = processInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            // Set up event handlers for async output
            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    Console.WriteLine($"[Claude Output]: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    Console.Error.WriteLine($"[Claude Error]: {e.Data}");
                }
            };

            // Start the process
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Send the prompt
            await process.StandardInput.WriteLineAsync(prompt);
            process.StandardInput.Close();

            // Wait for completion
            await process.WaitForExitAsync();

            var output = outputBuilder.ToString();

            // Extract JSON from response
            return ExtractJsonFromResponse(output);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ClaudeDirect] Error: {ex.Message}");
            throw;
        }
    }

    private async Task CreateMcpConfigFile(string path, List<Mcpserver> mcpServers, int conversationId)
    {
        // Create MCP configuration JSON for Claude CLI
        var config = new
        {
            servers = mcpServers.Select(m => new
            {
                name = m.Name,
                description = m.Description,
                url = m.Url,
                method = m.Call,
                handler = "http"  // Indicate it's an HTTP MCP server
            }).ToList()
        };

        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        await File.WriteAllTextAsync(path, json);
    }

    private AiResponseClass ExtractJsonFromResponse(string response)
    {
        // Same extraction logic as ClaudeClass
        string? explanation = null;
        string jsonContent;

        Console.WriteLine($"[ClaudeDirect] Extracting JSON from response ({response.Length} chars)");

        // Find JSON in markdown code blocks
        var jsonBlockMatch = System.Text.RegularExpressions.Regex.Match(
            response,
            @"```json\s*\n(.*?)\n```",
            System.Text.RegularExpressions.RegexOptions.Singleline
        );

        if (jsonBlockMatch.Success)
        {
            jsonContent = jsonBlockMatch.Groups[1].Value.Trim();
            Console.WriteLine($"[ClaudeDirect] JSON found in code block ({jsonContent.Length} chars)");

            var textBeforeJson = response.Substring(0, jsonBlockMatch.Index).Trim();
            if (!string.IsNullOrWhiteSpace(textBeforeJson))
            {
                explanation = textBeforeJson;
            }
        }
        else
        {
            // Try to find JSON object in text
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                response,
                @"\{[\s\S]*?\}",
                System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (jsonMatch.Success)
            {
                jsonContent = jsonMatch.Value.Trim();
            }
            else
            {
                jsonContent = response.Trim();
            }
        }

        try
        {
            var result = JsonConvert.DeserializeObject<AiResponseClass>(jsonContent);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to parse AI response");
            }

            if (explanation != null)
            {
                result.AiExplanation = explanation;
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ClaudeDirect] JSON parse error: {ex.Message}");
            throw;
        }
    }
}