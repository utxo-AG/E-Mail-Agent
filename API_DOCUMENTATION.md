# UTXO E-Mail Agent API Documentation

## Overview
The UTXO E-Mail Agent now provides a REST API for direct email processing, perfect for integration with the Admintool as a chat interface.

## Base URL
```
http://localhost:5051
```

## Endpoints

### 1. Health Check
**GET** `/api/health`

Check if the API is running and healthy.

#### Response
```json
{
  "status": "healthy",
  "version": "1.4.0"
}
```

### 2. Process Text
**POST** `/api/processtext`

Process text with AI and get a response. This endpoint uses Claude AI with configured MCP servers to generate intelligent email-formatted responses.

#### Request Headers
```
Content-Type: application/json
```

#### Request Body
```json
{
  "textContent": "string",   // Text content to process
  "agentId": 1              // Specific agent ID to use (optional, uses first active agent if not specified)
}
```

#### Response
```json
{
  "success": true,
  "error": null,
  "emailResponseText": "Plain text email response",
  "emailResponseSubject": "RE: original subject",
  "emailResponseHtml": "<html>HTML formatted response</html>",
  "aiExplanation": "Explanation of AI's reasoning",
  "attachments": [
    {
      "filename": "document.pdf",
      "contentType": "application/pdf",
      "content": "base64_encoded_content"
    }
  ]
}
```

## Usage Examples

### Using curl
```bash
# Simple text email
curl -X POST http://localhost:5050/api/processtext \
  -H "Content-Type: application/json" \
  -d '{
    "textContent": "Ihre Nachricht hier",
    "agentId": null
  }'

# With specific agent
curl -X POST http://localhost:5050/api/processtext \
  -H "Content-Type: application/json" \
  -d '{
    "textContent": "Ihre Nachricht hier",
    "agentId": 1
  }'
```

### Using JavaScript/Fetch (for Admintool integration)
```javascript
async function processText(textContent, agentId = null) {
  const response = await fetch('http://localhost:5050/api/processtext', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      textContent: textContent,
      agentId: agentId
    })
  });

  const result = await response.json();

  if (result.success) {
    console.log('Response:', result.emailResponseText);
    console.log('HTML:', result.emailResponseHtml);
    console.log('AI Explanation:', result.aiExplanation);
  } else {
    console.error('Error:', result.error);
  }

  return result;
}
```

### Using C#/.NET (for Blazor Admintool)
```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

public class EmailService
{
    private readonly HttpClient _httpClient;

    public EmailService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5050");
    }

    public async Task<ProcessEmailResponse> ProcessTextAsync(string textContent, int? agentId = null)
    {
        var request = new ProcessTextRequestClass
        {
            TextContent = textContent,
            AgentId = agentId
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/api/processtext", content);
        var responseJson = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<ProcessEmailResponse>(responseJson);
    }
}
```

## Features

1. **AI-Powered Responses**: Uses Claude AI to generate intelligent email responses
2. **MCP Server Integration**: Automatically uses configured MCP servers for specialized tasks (e.g., checking availability)
3. **Multi-format Support**: Returns both plain text and HTML formatted responses
4. **Attachment Support**: Can generate and return file attachments (PDFs, documents, etc.)
5. **Background Email Polling**: Continues to poll for emails in the background while serving API requests
6. **Database Integration**: Stores conversations and responses in the database for tracking

## Integration with Admintool

To integrate this API into the Admintool as a chat interface:

1. **Create a Chat Component**: Build a chat interface in the Admintool
2. **Send Messages**: Use the `/api/process-email` endpoint to send user messages
3. **Display Responses**: Show the `emailResponseText` or `emailResponseHtml` in the chat
4. **Show AI Reasoning**: Optionally display the `aiExplanation` to show how the AI processed the request
5. **Handle Attachments**: If attachments are returned, provide download links

## Running the API

1. Start the application:
```bash
cd "UTXO E-Mail Agent"
dotnet run
```

2. The API will be available at `http://localhost:5001`
3. The background email polling service will also start automatically

## Configuration

The API uses the configuration from `appsettings.json`:
- Database connection
- Claude API key
- Email polling interval
- Agent configurations

## Error Handling

If an error occurs, the API will return:
```json
{
  "success": false,
  "error": "Error message description",
  "emailResponseText": null,
  "emailResponseSubject": null,
  "emailResponseHtml": null,
  "aiExplanation": null,
  "attachments": []
}
```

Common errors:
- No active agent found
- Invalid request format
- AI processing failed
- Database connection issues