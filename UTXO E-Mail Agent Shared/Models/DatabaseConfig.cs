using Microsoft.EntityFrameworkCore;

namespace UTXO_E_Mail_Agent_Shared.Models;

/// <summary>
/// Zentrale Auflösung der Datenbank-Verbindung und der Server-Version.
///
/// Priorität der Verbindung:
///   1. Einzelne Umgebungsvariablen DB_HOST / DB_PORT / DB_USER / DB_PASSWORD / DB_NAME
///   2. Vollständiger Connection-String aus Umgebungsvariable UTXO_DB_CONNECTION
///   3. Übergebener Fallback (z. B. ConnectionStrings:DefaultConnection aus appsettings.json)
///
/// So kann jedes Projekt lokal über appsettings.json und im Container (rstcloud)
/// ausschließlich über Umgebungsvariablen konfiguriert werden.
/// </summary>
public static class DatabaseConfig
{
    /// <summary>
    /// Ermittelt den effektiven Connection-String. <paramref name="fallbackConnectionString"/>
    /// ist üblicherweise <c>configuration.GetConnectionString("DefaultConnection")</c>.
    /// </summary>
    public static string ResolveConnectionString(string? fallbackConnectionString = null)
    {
        // 1. Einzelne DB_* Umgebungsvariablen (höchste Priorität)
        var host = Environment.GetEnvironmentVariable("DB_HOST");
        if (!string.IsNullOrWhiteSpace(host))
        {
            var port = Environment.GetEnvironmentVariable("DB_PORT");
            if (string.IsNullOrWhiteSpace(port))
            {
                port = "3306";
            }

            var user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
            var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? string.Empty;
            var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "utxo_email_agent";

            // Passwort in Anführungszeichen, damit Sonderzeichen (z. B. Semikolon) nicht
            // als Trennzeichen im Connection-String interpretiert werden.
            var escapedPassword = password.Replace("'", "''");

            return $"server={host};port={port};user={user};password='{escapedPassword}';database={name};";
        }

        // 2. Vollständiger Connection-String als Umgebungsvariable
        var full = Environment.GetEnvironmentVariable("UTXO_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(full))
        {
            return full;
        }

        // 3. Fallback aus appsettings.json
        if (!string.IsNullOrWhiteSpace(fallbackConnectionString))
        {
            return fallbackConnectionString;
        }

        throw new InvalidOperationException(
            "Keine Datenbankverbindung konfiguriert. Setze entweder die Umgebungsvariablen " +
            "DB_HOST/DB_USER/DB_PASSWORD/DB_NAME (und optional DB_PORT), oder UTXO_DB_CONNECTION, " +
            "oder ConnectionStrings:DefaultConnection in der appsettings.json.");
    }

    /// <summary>
    /// Liefert die EF-Core-Server-Version. Standard ist MariaDB, da kein Verbindungs-Roundtrip
    /// nötig ist (wichtig beim Erst-Setup, wenn die Datenbank noch nicht existiert).
    /// Über die Umgebungsvariable DB_SERVER_VERSION überschreibbar, z. B.
    /// "11.4.0-mariadb" oder "8.0.36-mysql".
    /// </summary>
    public static ServerVersion GetServerVersion(string connectionString)
    {
        var configured = Environment.GetEnvironmentVariable("DB_SERVER_VERSION");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ServerVersion.Parse(configured);
        }

        // Sinnvoller, stabiler Standard für MariaDB (LTS). Kein Server-Roundtrip.
        return new MariaDbServerVersion(new Version(11, 4, 0));
    }
}
