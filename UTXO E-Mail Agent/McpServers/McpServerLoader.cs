using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Shared.Models;
using Claude.AgentSdk.Mcp;

namespace UTXO_E_Mail_Agent.McpServers;

/// <summary>
/// Lädt MCP Server-Konfigurationen aus der Datenbank und erstellt Tools
/// </summary>
public static class McpServerLoader
{
    /// <summary>
    /// Lädt alle MCP Server für einen Agent aus der DB und gibt sie als Builder zurück
    /// </summary>
    public static async Task<Action<McpServerRegistry>?> LoadMcpServersForAgentAsync(int agentId, int conversationid, string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        await using var db = new DefaultdbContext(optionsBuilder.Options);

        var mcpServers = await db.Mcpservers
            .Where(m => m.AgentId == agentId)
            .ToListAsync();

        if (!mcpServers.Any())
        {
            Console.WriteLine($"[MCP] No MCP servers found for agent {agentId}");
            return null;
        }

        Console.WriteLine($"[MCP] Loading {mcpServers.Count} MCP server(s) for agent {agentId}");

        return builder =>
        {
            // Erstelle einen SDK MCP Server mit allen Tools
            builder.AddSdk("database_mcp_servers", serverBuilder =>
            {
                foreach (var mcpConfig in mcpServers)
                {
                    Console.WriteLine($"[MCP] Registering tool: {mcpConfig.Name}");

                    // Erstelle den Handler für diesen MCP Server
                    var toolHandler = HttpMcpServerHandler.CreateToolHandler(mcpConfig, conversationid, connectionString);

                    // Registriere das Tool
                    serverBuilder.Tool(
                        mcpConfig.Name,
                        toolHandler,
                        mcpConfig.Description
                    );
                }
            });
        };
    }

    /// <summary>
    /// Lädt alle MCP Server für einen Agent und gibt sie direkt als Mcpserver-Liste zurück
    /// (für manuelle Verarbeitung)
    /// </summary>
    public static async Task<List<Mcpserver>> GetMcpServersForAgentAsync(int agentId, string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
        optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        await using var db = new DefaultdbContext(optionsBuilder.Options);

        return await db.Mcpservers
            .Where(m => m.AgentId == agentId)
            .ToListAsync();
    }
}
