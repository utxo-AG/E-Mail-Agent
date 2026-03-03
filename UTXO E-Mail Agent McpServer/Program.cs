using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Create and run the MCP server
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<HttpTools>();

await builder.Build().RunAsync();

/// <summary>
/// HTTP Tools for making API calls
/// </summary>
[McpServerToolType]
public class HttpTools
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Makes an HTTP request to the specified URL
    /// </summary>
    /// <param name="url">The full URL to call (e.g., https://api.example.com/endpoint)</param>
    /// <param name="method">HTTP method: GET, POST, PUT, DELETE, PATCH</param>
    /// <param name="body">Optional JSON body for POST/PUT/PATCH requests</param>
    /// <param name="headers">Optional JSON object with headers (e.g., {"Authorization": "Bearer token", "X-Custom": "value"})</param>
    /// <returns>JSON object with statusCode, success, body, and error fields</returns>
    [McpServerTool(Name = "http_request"), Description("Make an HTTP request to any URL. Supports GET, POST, PUT, DELETE, PATCH methods with optional JSON body and custom headers.")]
    public static async Task<string> HttpRequest(
        [Description("The full URL to call (e.g., https://api.example.com/endpoint)")] string url,
        [Description("HTTP method: GET, POST, PUT, DELETE, PATCH")] string method = "GET",
        [Description("Optional JSON body for POST/PUT/PATCH requests")] string? body = null,
        [Description("Optional JSON object with headers (e.g., {\"Authorization\": \"Bearer token\"})")] string? headers = null)
    {
        try
        {
            var httpMethod = method.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                _ => throw new ArgumentException($"Unsupported HTTP method: {method}")
            };

            var request = new HttpRequestMessage(httpMethod, url);

            // Add custom headers if provided
            if (!string.IsNullOrEmpty(headers))
            {
                try
                {
                    var headerDict = JsonSerializer.Deserialize<Dictionary<string, string>>(headers);
                    if (headerDict != null)
                    {
                        foreach (var (key, value) in headerDict)
                        {
                            request.Headers.TryAddWithoutValidation(key, value);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    return CreateErrorResponse(0, $"Invalid headers JSON: {ex.Message}");
                }
            }

            // Add body for POST/PUT/PATCH
            if (!string.IsNullOrEmpty(body) && (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Patch))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            return CreateResponse(
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                responseBody,
                null
            );
        }
        catch (HttpRequestException ex)
        {
            return CreateErrorResponse(0, $"HTTP request failed: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return CreateErrorResponse(0, "Request timed out");
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(0, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Simple GET request helper
    /// </summary>
    [McpServerTool(Name = "http_get"), Description("Make a simple HTTP GET request to a URL. Returns the response body.")]
    public static async Task<string> HttpGet(
        [Description("The full URL to call")] string url,
        [Description("Optional Bearer token for Authorization header")] string? bearerToken = null)
    {
        var headers = bearerToken != null 
            ? JsonSerializer.Serialize(new Dictionary<string, string> { { "Authorization", $"Bearer {bearerToken}" } })
            : null;
        
        return await HttpRequest(url, "GET", null, headers);
    }

    /// <summary>
    /// Simple POST request helper
    /// </summary>
    [McpServerTool(Name = "http_post"), Description("Make an HTTP POST request with a JSON body.")]
    public static async Task<string> HttpPost(
        [Description("The full URL to call")] string url,
        [Description("JSON body to send")] string body,
        [Description("Optional Bearer token for Authorization header")] string? bearerToken = null)
    {
        var headers = bearerToken != null 
            ? JsonSerializer.Serialize(new Dictionary<string, string> { { "Authorization", $"Bearer {bearerToken}" } })
            : null;
        
        return await HttpRequest(url, "POST", body, headers);
    }

    private static string CreateResponse(int statusCode, bool success, string? body, string? error)
    {
        var response = new
        {
            statusCode,
            success,
            body = TryParseJson(body),
            error
        };
        
        return JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string CreateErrorResponse(int statusCode, string error)
    {
        return CreateResponse(statusCode, false, null, error);
    }

    /// <summary>
    /// Try to parse body as JSON, return raw string if not valid JSON
    /// </summary>
    private static object? TryParseJson(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch
        {
            return body;
        }
    }
}
