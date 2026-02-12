using System.Text;
using System.Text.Json;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Shared.Models;
using System.Threading.Tasks;

namespace UTXO_E_Mail_Agent.McpServers;

/// <summary>
/// Wrapper class for JSON string parameters
/// Compatible with Claude CLI 2.1.31+ which requires a structured schema
/// </summary>
public class JsonStringParameter
{
    [Description("JSON string with API parameters. The format is defined in the tool description.")]
    public string json { get; set; } = string.Empty;
}

/// <summary>
/// Handler for HTTP-based MCP servers from the database
/// </summary>
public class HttpMcpServerHandler
{
    private readonly Mcpserver _mcpConfig;
    private readonly string _connectionString;
    private static readonly HttpClient _httpClient = new();

    public HttpMcpServerHandler(Mcpserver mcpConfig, string connectionString)
    {
        _mcpConfig = mcpConfig;
        _connectionString = connectionString;
    }

    /// <summary>
    /// Executes an HTTP call based on the MCP server configuration
    /// </summary>
    /// <param name="mcpserverid">ID of the MCP server in the database</param>
    /// <param name="conversationid">ID of the conversation</param>
    /// <param name="parameter">Wrapper object with JSON string</param>
    private async Task<string> ExecuteAsync(int mcpserverid, int conversationid, JsonStringParameter parameter)
    {
        try
        {
            // Extract JSON string from wrapper
            string? jsonString = parameter?.json;

            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(_connectionString, Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(_connectionString));
            await using var db = new DefaultdbContext(optionsBuilder.Options);

            var method = _mcpConfig.Call.ToUpper();
            var url = _mcpConfig.Url;

            Console.WriteLine($"[MCP {_mcpConfig.Name}] ========================================");
            Console.WriteLine($"[MCP {_mcpConfig.Name}] Executing {method} to {url}");
            Console.WriteLine($"[MCP {_mcpConfig.Name}] Raw Parameters: {jsonString ?? "(none)"}");

            Mcpserverrequest mrequest = new Mcpserverrequest(){McpserverId = mcpserverid, ConversationId =  conversationid, Parameter =  jsonString ?? "(none)", Created =  DateTime.Now};
            
            
            HttpResponseMessage response;

            switch (method)
            {
                case "GET":
                    response = await ExecuteGetAsync(url, jsonString);
                    break;

                case "POST":
                    response = await ExecutePostAsync(url, jsonString);
                    break;

                case "PUT":
                    response = await ExecutePutAsync(url, jsonString);
                    break;

                case "DELETE":
                    response = await ExecuteDeleteAsync(url, jsonString);
                    break;

                default:
                    return $"ERROR: Unsupported HTTP method: {method}";
            }

            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[MCP {_mcpConfig.Name}] Success: {response.StatusCode}");
                Console.WriteLine($"[MCP {_mcpConfig.Name}] Response: {content}");
                Console.WriteLine($"[MCP {_mcpConfig.Name}] ========================================");
                mrequest.Result = content;
                db.Mcpserverrequests.Add(mrequest);
                await db.SaveChangesAsync();
                
                return content;
            }
            else
            {
                Console.Error.WriteLine($"[MCP {_mcpConfig.Name}] Error: {response.StatusCode}");
                Console.Error.WriteLine($"[MCP {_mcpConfig.Name}] Error Response: {content}");
                Console.Error.WriteLine($"[MCP {_mcpConfig.Name}] ========================================");
                mrequest.Result = "ERROR ({response.StatusCode}): {content}";
                db.Mcpserverrequests.Add(mrequest);
                await db.SaveChangesAsync();
                
                return $"ERROR ({response.StatusCode}): {content}";
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MCP {_mcpConfig.Name}] Exception: {ex.Message}");
            return $"ERROR: {ex.Message}";
        }
    }

    private async Task<HttpResponseMessage> ExecuteGetAsync(string url, string? parameters)
    {
        // For GET: Parameters in URL as query string
        if (!string.IsNullOrEmpty(parameters))
        {
            var queryParams = ParseParametersToQueryString(parameters);
            url = $"{url}?{queryParams}";
            Console.WriteLine($"[MCP {_mcpConfig.Name}] Final URL: {url}");
        }

        return await _httpClient.GetAsync(url);
    }

    private async Task<HttpResponseMessage> ExecutePostAsync(string url, string? parameters)
    {
        // For POST: Parameters as JSON in body
        var bodyJson = string.IsNullOrEmpty(parameters) ? "{}" : parameters;

        Console.WriteLine($"[MCP {_mcpConfig.Name}] POST Body:");
        Console.WriteLine($"[MCP {_mcpConfig.Name}] {bodyJson}");

        var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        return await _httpClient.PostAsync(url, content);
    }

    private async Task<HttpResponseMessage> ExecutePutAsync(string url, string? parameters)
    {
        // For PUT: Parameters as JSON in body
        var bodyJson = string.IsNullOrEmpty(parameters) ? "{}" : parameters;

        Console.WriteLine($"[MCP {_mcpConfig.Name}] PUT Body:");
        Console.WriteLine($"[MCP {_mcpConfig.Name}] {bodyJson}");

        var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

        return await _httpClient.PutAsync(url, content);
    }

    private async Task<HttpResponseMessage> ExecuteDeleteAsync(string url, string? parameters)
    {
        // For DELETE: Parameters in URL as query string
        if (!string.IsNullOrEmpty(parameters))
        {
            var queryParams = ParseParametersToQueryString(parameters);
            url = $"{url}?{queryParams}";
            Console.WriteLine($"[MCP {_mcpConfig.Name}] Final URL: {url}");
        }

        return await _httpClient.DeleteAsync(url);
    }

    /// <summary>
    /// Converts JSON parameters to query string for GET/DELETE
    /// </summary>
    private string ParseParametersToQueryString(string jsonParameters)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonParameters);
            if (dict == null || dict.Count == 0)
                return string.Empty;

            var queryParams = string.Join("&",
                dict.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value?.ToString() ?? "")}"));

            return queryParams;
        }
        catch
        {
            // If not valid JSON, interpret as query string
            return jsonParameters;
        }
    }

    /// <summary>
    /// Creates an MCP tool for this server
    /// Format: Accepts JsonStringParameter wrapper for compatibility with Claude CLI 2.1.31+
    /// SDK generates schema with "json" property for the JSON string
    /// The JSON format is defined in the tool description (Description)
    /// </summary>
    public static Func<JsonStringParameter, Task<string>> CreateToolHandler(Mcpserver mcpConfig, int conversationid, string connectionString)
    {
        var handler = new HttpMcpServerHandler(mcpConfig, connectionString);
        return async (parameters) => await handler.ExecuteAsync(mcpConfig.Id, conversationid, parameters);
    }
}
