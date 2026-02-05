# In-Process MCP Server Beispiele

## Übersicht
In-Process MCP Server ermöglichen es Claude, auf Funktionen in deinem Code zuzugreifen.

## Tool-Signaturen

Das SDK unterstützt verschiedene Methoden-Signaturen:

### 1. Einfache synchrone Methode
```csharp
public static string SimpleMethod(string input)
{
    return $"Processed: {input}";
}

// Verwendung:
.Tool("simple_method", SimpleMethod, "Description")
```

### 2. Async Methode
```csharp
public static async Task<string> AsyncMethod(string input)
{
    await Task.Delay(100);
    return $"Processed: {input}";
}

// Verwendung:
.Tool("async_method", AsyncMethod, "Description")
```

### 3. Mehrere Parameter
```csharp
public static string MultiParam(string name, int age, bool active)
{
    return $"{name} is {age} years old and {(active ? "active" : "inactive")}";
}

// Verwendung:
.Tool("multi_param", MultiParam, "Takes name, age and active status")
```

### 4. Komplexes Objekt als Parameter
```csharp
public class SearchRequest
{
    public string Query { get; set; }
    public int Limit { get; set; }
    public DateTime? From { get; set; }
}

public static async Task<string> ComplexSearch(SearchRequest request)
{
    // request wird automatisch aus dem JSON deserialisiert
    return $"Searching for {request.Query} with limit {request.Limit}";
}

// Verwendung:
.Tool("complex_search", ComplexSearch, "Search with complex parameters")
```

### 5. Mit CancellationToken
```csharp
public static async Task<string> WithCancellation(
    string query,
    CancellationToken ct)
{
    // Kann auf Cancellation reagieren
    await Task.Delay(1000, ct);
    return "Done";
}

// Verwendung:
.Tool("with_cancellation", WithCancellation, "Supports cancellation")
```

### 6. Lambda-Funktionen (für einfache Logik)
```csharp
.Tool("add", (double a, double b) => a + b, "Add two numbers")
.Tool("get_time", () => DateTime.Now.ToString(), "Get current time")
.Tool("format_text", (string text) => text.ToUpper(), "Convert to uppercase")
```

## Beispiel: E-Mail MCP Server

```csharp
// In ClaudeClass.cs
var options = ClaudeSDK.Options()
    .SystemPrompt(systemPrompt)
    .McpServers(m => m.AddSdk("email_tools", s => s
        // Einfaches Tool
        .Tool("search_customer_emails",
            EmailMcpServer.SearchCustomerEmails,
            "Searches for previous emails from a customer")

        // Tool mit mehreren Parametern
        .Tool("search_by_subject",
            EmailMcpServer.SearchBySubject,
            "Search conversations by subject/topic")

        // Lambda für einfache Operationen
        .Tool("get_current_time",
            () => DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            "Returns current date and time")
    ))
    .AllowAllTools()  // WICHTIG: Tools erlauben!
    .Build();
```

## Rückgabewerte

Tools können folgendes zurückgeben:

- **`string`** - Wird als Text zurückgegeben
- **`int`, `double`, `bool`** - Werden als Text konvertiert
- **`object`** - Wird als JSON serialisiert
- **`McpToolResult`** - Für volle Kontrolle über das Ergebnis
- **`McpContent`** - Für strukturierte Inhalte

## Best Practices

1. **Kurze, klare Tool-Namen** - z.B. `search_emails`, nicht `SearchEmailsInDatabase`
2. **Gute Beschreibungen** - Claude nutzt diese, um zu entscheiden, wann das Tool verwendet wird
3. **Fehlerbehandlung** - Try-catch in Tools, um hilfreiche Fehlermeldungen zurückzugeben
4. **Performance** - Async/await für DB-Zugriffe
5. **Logging** - Console.WriteLine() in Tools für Debugging

## Aktivierung

Vergiss nicht `.AllowAllTools()` oder `.AllowTools("tool1", "tool2")` zu verwenden,
sonst kann Claude die Tools nicht nutzen!

## Beispiel mit komplexeren Rückgabewerten

```csharp
using Claude.AgentSdk.Mcp;

public static McpToolResult GetEmailWithDetails(string emailId)
{
    var email = // ... lade E-Mail aus DB

    return new McpToolResult
    {
        Content = [
            new McpContent
            {
                Type = "text",
                Text = $"Subject: {email.Subject}\nFrom: {email.From}"
            },
            new McpContent
            {
                Type = "resource",
                Uri = $"email://{emailId}",
                MimeType = "text/plain",
                Text = email.Body
            }
        ]
    };
}
```
