using Microsoft.EntityFrameworkCore;

namespace UTXO_E_Mail_Agent_Shared.Models;

/// <summary>
/// Leichtgewichtiger Schema-Migrations-Runner für das database-first-Modell
/// (das keine EF-Core-Migrations hat). Wendet beim Start inkrementelle SQL-Schritte an,
/// aber nur die, die noch nicht angewendet wurden – getrackt in der Tabelle
/// <c>schema_migrations</c>. Dadurch können DB-Änderungen automatisch verteilt werden,
/// ohne direkten Datenbankzugriff von außen.
///
/// Neue Änderung hinzufügen: unten einen Eintrag mit stabiler, eindeutiger Id und dem
/// SQL anhängen. Die Reihenfolge bleibt erhalten; bereits angewendete Ids werden übersprungen.
/// Die Statements sollten idempotent/wiederholbar formuliert sein (z. B. MODIFY auf die
/// aktuelle Spaltendefinition), damit sie auch gegen eine frisch per EnsureCreated()
/// erzeugte DB gefahrlos laufen.
/// </summary>
public static class DatabaseMigrator
{
    private static readonly (string Id, string Sql)[] Migrations =
    {
        ("2026-11-13_aiprovider_add_rstcloud",
            "ALTER TABLE agents MODIFY aiprovider " +
            "ENUM('claude','openai','grok','gemini','claudecode','rstcloud') NOT NULL DEFAULT 'claude'"),
    };

    /// <summary>
    /// Wendet alle noch nicht angewendeten Migrationen an und gibt deren Anzahl zurück.
    /// </summary>
    public static async Task<int> ApplyAsync(DefaultdbContext db, CancellationToken ct = default)
    {
        // Tracking-Tabelle sicherstellen.
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS schema_migrations (" +
            "id VARCHAR(255) NOT NULL PRIMARY KEY, " +
            "applied_at DATETIME NOT NULL)", ct);

        // Bereits angewendete Ids laden (Spalte muss 'Value' heißen für SqlQueryRaw<string>).
        var applied = await db.Database
            .SqlQueryRaw<string>("SELECT id AS Value FROM schema_migrations")
            .ToListAsync(ct);
        var appliedSet = new HashSet<string>(applied, StringComparer.Ordinal);

        var count = 0;
        foreach (var (id, sql) in Migrations)
        {
            if (appliedSet.Contains(id))
            {
                continue;
            }

            await db.Database.ExecuteSqlRawAsync(sql, ct);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO schema_migrations (id, applied_at) VALUES ({0}, {1})",
                new object[] { id, DateTime.UtcNow }, ct);
            count++;
        }

        return count;
    }
}
