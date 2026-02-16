using Microsoft.EntityFrameworkCore;
using UTXO_E_Mail_Agent_Shared.Models;

namespace UTXO_E_Mail_Agent.Services;

/// <summary>
/// Handles server registration and lifesign updates in the database.
/// Uses DigitalOcean Droplet ID when available, falls back to hostname.
/// </summary>
public static class ServerRegistrationService
{
    private static string? _serverIdentifier;

    /// <summary>
    /// Gets the unique server identifier. Tries DigitalOcean Metadata API first,
    /// falls back to Environment.MachineName.
    /// </summary>
    public static async Task<string> GetServerIdentifierAsync()
    {
        if (_serverIdentifier != null)
            return _serverIdentifier;

        // Try DigitalOcean Metadata API (only available on DO droplets)
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var dropletId = await httpClient.GetStringAsync("http://169.254.169.254/metadata/v1/id");
            var hostname = await httpClient.GetStringAsync("http://169.254.169.254/metadata/v1/hostname");

            _serverIdentifier = $"do-{dropletId}-{hostname}".Trim();
            Logger.Log($"[ServerRegistration] Identified as DigitalOcean Droplet: {_serverIdentifier}");
            return _serverIdentifier;
        }
        catch
        {
            // Not running on DigitalOcean or metadata not available
        }

        _serverIdentifier = Environment.MachineName;
        Logger.Log($"[ServerRegistration] Using hostname as identifier: {_serverIdentifier}");
        return _serverIdentifier;
    }

    /// <summary>
    /// Registers the server in the database on startup.
    /// Creates a new entry or reactivates an existing one.
    /// </summary>
    public static async Task RegisterServerAsync(DefaultdbContext db)
    {
        var identifier = await GetServerIdentifierAsync();

        var server = await db.Servers.FirstOrDefaultAsync(s => s.Servername == identifier);

        if (server != null)
        {
            server.State = "active";
            server.Lastlifesign = DateTime.UtcNow;
            Logger.Log($"[ServerRegistration] Server '{identifier}' reactivated");
        }
        else
        {
            server = new Server
            {
                Servername = identifier,
                State = "active",
                Lastlifesign = DateTime.UtcNow
            };
            db.Servers.Add(server);
            Logger.Log($"[ServerRegistration] Server '{identifier}' registered");
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Updates the lastlifesign timestamp for this server.
    /// Called during each polling cycle.
    /// </summary>
    public static async Task UpdateLifesignAsync(DefaultdbContext db)
    {
        var identifier = await GetServerIdentifierAsync();

        var server = await db.Servers.FirstOrDefaultAsync(s => s.Servername == identifier);
        if (server != null)
        {
            server.Lastlifesign = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await db.Database.ExecuteSqlAsync($"UPDATE server SET state='notactive' WHERE lastlifesign < DATE_SUB(UTC_TIMESTAMP(), INTERVAL 10 MINUTE)");
    }

    /// <summary>
    /// Sets this server to notactive on shutdown.
    /// </summary>
    public static async Task DeregisterServerAsync(DefaultdbContext db)
    {
        var identifier = await GetServerIdentifierAsync();

        var server = await db.Servers.FirstOrDefaultAsync(s => s.Servername == identifier);
        if (server != null)
        {
            server.State = "notactive";
            server.Lastlifesign = DateTime.UtcNow;
            await db.SaveChangesAsync();
            Logger.Log($"[ServerRegistration] Server '{identifier}' set to notactive (shutdown)");
        }
    }
}
