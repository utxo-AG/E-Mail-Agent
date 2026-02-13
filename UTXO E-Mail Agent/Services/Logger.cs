using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Services;

/// <summary>
/// Static logger that writes to both console and database
/// </summary>
public static class Logger
{
    private static string? _connectionString;
    private static bool _isInitialized;

    /// <summary>
    /// Initialize the logger with the database connection string
    /// Must be called once at application startup
    /// </summary>
    public static void Initialize(string connectionString)
    {
        _connectionString = connectionString;
        _isInitialized = true;
    }

    /// <summary>
    /// Log a message to console and database
    /// </summary>
    /// <param name="message">The log message (will be truncated to 255 chars for DB)</param>
    /// <param name="agentId">Optional agent ID that triggered the log</param>
    /// <param name="additionalData">Optional additional data (JSON, email content, etc.)</param>
    public static void Log(string message, int? agentId = null, string? additionalData = null)
    {
        // Always write to console
        Console.WriteLine(message);

        // Write to database if initialized
        if (_isInitialized && !string.IsNullOrEmpty(_connectionString))
        {
            _ = Task.Run(() => WriteToDatabase(message, agentId, additionalData));
        }
    }

    /// <summary>
    /// Log a message to console and database (async version)
    /// </summary>
    public static async Task LogAsync(string message, int? agentId = null, string? additionalData = null)
    {
        // Always write to console
        Console.WriteLine(message);

        // Write to database if initialized
        if (_isInitialized && !string.IsNullOrEmpty(_connectionString))
        {
            await WriteToDatabase(message, agentId, additionalData);
        }
    }

    /// <summary>
    /// Log an error message (prefixed with [ERROR])
    /// </summary>
    public static void LogError(string message, int? agentId = null, string? additionalData = null)
    {
        Log($"[ERROR] {message}", agentId, additionalData);
    }

    /// <summary>
    /// Log a warning message (prefixed with [WARNING])
    /// </summary>
    public static void LogWarning(string message, int? agentId = null, string? additionalData = null)
    {
        Log($"[WARNING] {message}", agentId, additionalData);
    }

    /// <summary>
    /// Log with a custom prefix (e.g., "[MCP]", "[EmailPollingService]")
    /// </summary>
    public static void LogWithPrefix(string prefix, string message, int? agentId = null, string? additionalData = null)
    {
        Log($"[{prefix}] {message}", agentId, additionalData);
    }

    private static async Task WriteToDatabase(string message, int? agentId, string? additionalData)
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<DefaultdbContext>();
            optionsBuilder.UseMySql(_connectionString!, ServerVersion.AutoDetect(_connectionString!));

            await using var db = new DefaultdbContext(optionsBuilder.Options);

            // Truncate message to 255 characters
            var truncatedMessage = message.Length > 255
                ? message.Substring(0, 252) + "..."
                : message;

            var logEntry = new Logmessage
            {
                Message = truncatedMessage,
                AgentId = agentId,
                Additionaldata = additionalData,
                Created = DateTime.Now
            };

            db.Logmessages.Add(logEntry);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Don't throw - just log to console if DB write fails
            Console.WriteLine($"[Logger] Failed to write to database: {ex.Message}");
        }
    }
}
