using System.Text;
using System.Text.Json;
using UTXO_E_Mail_Agent_Admintool.Models;

namespace UTXO_E_Mail_Agent_Admintool.Services;

public class AgentApiService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AgentApiService> _logger;

    public AgentApiService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<AgentApiService> logger)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    public async Task<ChatWithAgentResponse> SendMessageAsync(int agentId, string message)
    {
        var baseUrl = _configuration["AgentApi:BaseUrl"]
            ?? throw new InvalidOperationException("AgentApi:BaseUrl not configured in appsettings.json");

        var request = new ChatWithAgentRequest
        {
            TextContent = message,
            AgentId = agentId
        };

        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(request, jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var endpoint = $"{baseUrl.TrimEnd('/')}/api/processtext";
            _logger.LogInformation("Sending chat request to {Endpoint} for agent {AgentId}", endpoint, agentId);

            var response = await _httpClient.PostAsync(endpoint, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Agent API returned {StatusCode}: {Response}", response.StatusCode, responseJson);
                return new ChatWithAgentResponse
                {
                    Success = false,
                    Error = $"API returned {response.StatusCode}: {responseJson}"
                };
            }

            var result = JsonSerializer.Deserialize<ChatWithAgentResponse>(responseJson, jsonOptions);
            return result ?? new ChatWithAgentResponse
            {
                Success = false,
                Error = "Failed to deserialize response"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to Agent API at {BaseUrl}", baseUrl);
            return new ChatWithAgentResponse
            {
                Success = false,
                Error = $"Failed to connect to Agent API: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Agent API");
            return new ChatWithAgentResponse
            {
                Success = false,
                Error = $"Unexpected error: {ex.Message}"
            };
        }
    }
}
