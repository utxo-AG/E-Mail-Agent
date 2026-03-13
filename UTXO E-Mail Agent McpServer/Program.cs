using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using UTXO_E_Mail_Agent_Shared.Models;

// Create and run the MCP server
var builder = Host.CreateApplicationBuilder(args);

// Add configuration from appsettings.json
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

// Get database connection - read from config or environment variable
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("UTXO_DB_CONNECTION") 
    ?? "server=localhost;database=utxo_email_agent;user=root;password=root";

// Set the connection string for the static ApiTools class
ApiTools.ConnectionString = connectionString;

builder.Services.AddDbContext<DefaultdbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ApiTools>();

await builder.Build().RunAsync();

/// <summary>
/// API Tools - calls configured APIs from database
/// </summary>
[McpServerToolType]
public class ApiTools
{
    /// <summary>
    /// Database connection string - set at startup
    /// </summary>
    public static string ConnectionString { get; set; } = "";
    
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>
    /// Calls a configured API endpoint for the specified agent.
    /// The API configuration (URL, method, auth) is looked up from the database.
    /// </summary>
    [McpServerTool(Name = "call_api"), Description("Call a configured API endpoint. The API configuration (URL, method, authentication) is retrieved from the database based on agent_id and api_name.")]
    public static async Task<string> CallApi(
        [Description("The agent ID that owns this API configuration")] int agentId,
        [Description("The name of the API to call (as configured in the system)")] string apiName,
        [Description("Optional JSON data to send for POST/PUT/PATCH requests")] string? data = null)
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
            
            await using var db = new DefaultdbContext(optionsBuilder.Options);
            
            // Look up API configuration
            var apiConfig = await db.Mcpservers
                .Where(m => m.AgentId == agentId && m.Name == apiName)
                .FirstOrDefaultAsync();

            if (apiConfig == null)
            {
                return CreateErrorResponse(404, $"API '{apiName}' not found for agent {agentId}");
            }

            // Determine HTTP method
            var httpMethod = apiConfig.Call.ToUpperInvariant() switch
            {
                "GET" => HttpMethod.Get,
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                "PATCH" => HttpMethod.Patch,
                _ => throw new ArgumentException($"Unsupported HTTP method: {apiConfig.Call}")
            };

            // Create request
            var request = new HttpRequestMessage(httpMethod, apiConfig.Url);

            // Add Bearer token if configured
            if (!string.IsNullOrEmpty(apiConfig.Bearer))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiConfig.Bearer);
            }

            // Add body for POST/PUT/PATCH
            if (!string.IsNullOrEmpty(data) && (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Patch))
            {
                request.Content = new StringContent(data, Encoding.UTF8, "application/json");
            }

            // Execute request
            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            return CreateResponse(
                (int)response.StatusCode,
                response.IsSuccessStatusCode,
                responseBody,
                null,
                apiConfig.Name,
                apiConfig.Description
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
    /// Lists all available API endpoints for a specific agent.
    /// </summary>
    [McpServerTool(Name = "list_apis"), Description("List all available API endpoints configured for a specific agent.")]
    public static async Task<string> ListApis(
        [Description("The agent ID to list APIs for")] int agentId)
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
            
            await using var db = new DefaultdbContext(optionsBuilder.Options);
            
            var apis = await db.Mcpservers
                .Where(m => m.AgentId == agentId)
                .Select(m => new 
                {
                    name = m.Name,
                    description = m.Description,
                    method = m.Call.ToUpper(),
                    hasAuth = !string.IsNullOrEmpty(m.Bearer)
                })
                .ToListAsync();

            return JsonSerializer.Serialize(new
            {
                agentId,
                apiCount = apis.Count,
                apis
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(0, $"Error listing APIs: {ex.Message}");
        }
    }

    /// <summary>
    /// Search previous conversations for this agent. Filter by sender email, keywords, and time range.
    /// </summary>
    [McpServerTool(Name = "search_conversations"), Description("Search previous email conversations. Filter by sender email address, subject/text keywords, and time range. Returns conversation details with attachment metadata.")]
    public static async Task<string> SearchConversations(
        [Description("The agent ID")] int agentId,
        [Description("Filter by sender email address (partial match)")] string? emailAddress = null,
        [Description("Search term for subject or text content")] string? searchTerm = null,
        [Description("Number of days to look back (default 7)")] int daysBack = 7,
        [Description("Maximum number of results (default 10)")] int limit = 10)
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString),
                mysqlOptions => mysqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            await using var db = new DefaultdbContext(optionsBuilder.Options);

            var cutoff = DateTime.UtcNow.AddDays(-daysBack);

            var query = db.Conversations
                .Where(c => c.AgentId == agentId && c.Emailreceived >= cutoff);

            if (!string.IsNullOrEmpty(emailAddress))
                query = query.Where(c => c.Emailfrom.Contains(emailAddress));

            if (!string.IsNullOrEmpty(searchTerm))
                query = query.Where(c =>
                    c.Subject.Contains(searchTerm) ||
                    (c.Text != null && c.Text.Contains(searchTerm)) ||
                    (c.Agentresponsetext != null && c.Agentresponsetext.Contains(searchTerm)));

            var conversations = await query
                .OrderByDescending(c => c.Emailreceived)
                .Take(limit)
                .Select(c => new
                {
                    c.Id,
                    c.Emailfrom,
                    c.Subject,
                    text = c.Text != null ? c.Text.Substring(0, Math.Min(500, c.Text.Length)) : null,
                    agentResponse = c.Agentresponsetext != null
                        ? c.Agentresponsetext.Substring(0, Math.Min(500, c.Agentresponsetext.Length))
                        : null,
                    c.Emailreceived,
                    attachments = c.ConversationAttachments.Select(a => new
                    {
                        a.Id,
                        a.Filename,
                        a.ContentType
                    }).ToList()
                })
                .AsNoTracking()
                .ToListAsync();

            return JsonSerializer.Serialize(new
            {
                agentId,
                count = conversations.Count,
                conversations
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(0, $"Error searching conversations: {ex.Message}");
        }
    }

    /// <summary>
    /// Download a conversation attachment by its ID. Returns the file content as Base64.
    /// </summary>
    [McpServerTool(Name = "get_attachment"), Description("Download a conversation attachment by ID. Returns the Base64-encoded file content with metadata.")]
    public static async Task<string> GetAttachment(
        [Description("The agent ID (for authorization)")] int agentId,
        [Description("The attachment ID to download")] int attachmentId)
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(ConnectionString, ServerVersion.AutoDetect(ConnectionString));
            await using var db = new DefaultdbContext(optionsBuilder.Options);

            var attachment = await db.ConversationAttachments
                .Include(a => a.Conversation)
                .Where(a => a.Id == attachmentId && a.Conversation.AgentId == agentId)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (attachment == null)
            {
                return CreateErrorResponse(404, $"Attachment {attachmentId} not found for agent {agentId}");
            }

            return JsonSerializer.Serialize(new
            {
                id = attachment.Id,
                filename = attachment.Filename,
                contentType = attachment.ContentType,
                content = attachment.Content
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(0, $"Error getting attachment: {ex.Message}");
        }
    }

    private static string CreateResponse(int statusCode, bool success, string? body, string? error, string? apiName = null, string? apiDescription = null)
    {
        var response = new
        {
            statusCode,
            success,
            apiName,
            apiDescription,
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
