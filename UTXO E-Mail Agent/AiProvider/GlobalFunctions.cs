using System.Text.RegularExpressions;

namespace UTXO_E_Mail_Agent.AiProvider;

/// <summary>
/// Shared utility functions used across AI providers
/// </summary>
public static class GlobalFunctions
{
    /// <summary>
    /// Sanitizes tool names for Anthropic API compatibility
    /// Pattern: ^[a-zA-Z0-9_-]{1,64}$
    /// </summary>
    public static string SanitizeToolName(string name)
    {
        var sanitized = Regex.Replace(name.Replace(" ", "_"), "[^a-zA-Z0-9_-]", "");
        return sanitized.Length > 64 ? sanitized[..64] : sanitized;
    }

    /// <summary>
    /// Detects the language of an email text based on common words.
    /// Returns a 2-char ISO code (e.g. "de", "en", "fr") or null if uncertain.
    /// </summary>
    public static string? DetectLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var lower = text.ToLowerInvariant();

        var languageIndicators = new Dictionary<string, string[]>
        {
            ["en"] = ["dear", "hello", "please", "thank you", "thanks", "regards", "sincerely", "would", "could", "should", "the", "this", "that", "with", "have", "from", "your", "you", "we are", "i am", "looking forward"],
            ["de"] = ["sehr geehrte", "hallo", "bitte", "danke", "vielen dank", "freundliche grüße", "mit freundlichen", "können", "möchten", "würden", "liebe grüße", "hiermit", "bezüglich", "anbei", "wir haben", "ich bin"],
            ["fr"] = ["bonjour", "merci", "s'il vous plaît", "cordialement", "madame", "monsieur", "nous avons", "je suis", "veuillez", "cher", "chère", "avec"],
            ["es"] = ["hola", "gracias", "por favor", "saludos", "estimado", "estimada", "atentamente", "nosotros", "tenemos", "somos", "querido", "querida"],
            ["it"] = ["buongiorno", "grazie", "per favore", "cordiali saluti", "gentile", "distinti saluti", "abbiamo", "siamo", "vorrei"],
            ["nl"] = ["geachte", "bedankt", "alstublieft", "met vriendelijke groet", "hartelijk", "wij hebben", "graag"],
            ["pt"] = ["olá", "obrigado", "obrigada", "por favor", "atenciosamente", "prezado", "prezada", "cordialmente"],
        };

        var scores = new Dictionary<string, int>();
        foreach (var (lang, words) in languageIndicators)
        {
            scores[lang] = words.Count(word => lower.Contains(word));
        }

        var best = scores.MaxBy(kv => kv.Value);
        return best.Value >= 2 ? best.Key : null;
    }

    /// <summary>
    /// Returns localized fallback messages for error cases
    /// </summary>
    public static (string Text, string Subject) GetFallbackMessages(string languageCode)
    {
        return languageCode.ToLowerInvariant() switch
        {
            "en" => ("I was unable to generate a suitable response. Please try again.", "RE: Your inquiry"),
            "de" => ("Ich konnte keine passende Antwort generieren. Bitte versuchen Sie es erneut.", "RE: Ihre Anfrage"),
            "fr" => ("Je n'ai pas pu générer une réponse appropriée. Veuillez réessayer.", "RE: Votre demande"),
            "es" => ("No pude generar una respuesta adecuada. Por favor, inténtelo de nuevo.", "RE: Su consulta"),
            "it" => ("Non sono riuscito a generare una risposta adeguata. Per favore, riprovi.", "RE: La sua richiesta"),
            "nl" => ("Ik kon geen passend antwoord genereren. Probeer het opnieuw.", "RE: Uw aanvraag"),
            "pt" => ("Não foi possível gerar uma resposta adequada. Por favor, tente novamente.", "RE: Sua consulta"),
            _ => ("I was unable to generate a suitable response. Please try again.", "RE: Your inquiry"),
        };
    }

    /// <summary>
    /// Converts country name to ISO 2-char code
    /// </summary>
    public static string CountryToIso2(string? country)
    {
        if (string.IsNullOrEmpty(country)) return "DE";

        // If already a 2-char code, return as-is
        if (country.Length == 2) return country.ToUpper();

        return country.ToLower() switch
        {
            "germany" or "deutschland" => "DE",
            "austria" or "österreich" => "AT",
            "switzerland" or "schweiz" => "CH",
            "albania" => "AL",
            "belgium" => "BE",
            "bosnia and herzegovina" => "BA",
            "bulgaria" => "BG",
            "croatia" => "HR",
            "cyprus" => "CY",
            "czech republic" => "CZ",
            "denmark" => "DK",
            "estonia" => "EE",
            "finland" => "FI",
            "france" => "FR",
            "greece" => "GR",
            "hungary" => "HU",
            "iceland" => "IS",
            "ireland" => "IE",
            "italy" => "IT",
            "latvia" => "LV",
            "liechtenstein" => "LI",
            "lithuania" => "LT",
            "luxembourg" => "LU",
            "malta" => "MT",
            "moldova" => "MD",
            "monaco" => "MC",
            "montenegro" => "ME",
            "netherlands" => "NL",
            "north macedonia" => "MK",
            "norway" => "NO",
            "poland" => "PL",
            "portugal" => "PT",
            "romania" => "RO",
            "serbia" => "RS",
            "slovakia" => "SK",
            "slovenia" => "SI",
            "spain" => "ES",
            "sweden" => "SE",
            "turkey" => "TR",
            "ukraine" => "UA",
            "united kingdom" => "GB",
            "united states" => "US",
            "canada" => "CA",
            _ => "DE"
        };
    }

    /// <summary>
    /// Gets MIME content type from file extension
    /// </summary>
    public static string GetContentTypeFromExtension(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".csv" => "text/csv",
            ".xml" => "text/xml",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream"
        };
    }
}
