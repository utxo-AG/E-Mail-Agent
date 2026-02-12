# UTXO E-Mail Agent

The core email processing engine that handles incoming emails, processes them with AI, and sends intelligent responses.

## 🎯 Purpose

This application continuously monitors email accounts, processes incoming messages using AI (Claude), and automatically sends contextual responses. It supports multiple email providers and can be extended with custom MCP (Model Context Protocol) servers for dynamic tool integration.

## 🏗️ Architecture

### Components

#### Email Providers (`EmailProvider/`)
Implementations for different email protocols:
- **IMAP** (`ImapClass.cs`): Standard IMAP protocol support
- **POP3** (`PopClass.cs`): POP3 protocol support
- **Exchange** (`ExchangeClass.cs`): Microsoft Exchange support
- **Inbound API** (`InboundClass.cs`): Custom API-based email provider

All providers implement `IEmailProvider` interface with methods:
- `ListNewEmailsAsync()`: Fetch unread emails
- `GetEmailByIdAsync()`: Retrieve specific email
- `ReplyToEmailAsync()`: Send response

#### AI Providers (`AiProvider/`)
AI integration for response generation:

- **Claude** (`ClaudeClass.cs`): Main Anthropic Claude AI integration
  - Multi-turn conversation support
  - MCP server tool usage for API calls
  - Handles email response generation
  - Maximum 20 iterations for tool calls

- **ClaudeGenerateDocumentsClass** (`ClaudeGenerateDocumentsClass.cs`): Document generation agent
  - Isolated agent for creating PDF, DOCX, XLSX, PPTX documents
  - Uses Anthropic Skills API (no MCP tools)
  - Called automatically when `MustCreateAttachment=true`
  - Downloads generated files via Skills API

**Two-Agent Architecture:**
Due to conflicts between MCP tools and Anthropic Skills, document generation is handled by a separate agent:
1. **Agent 1 (ClaudeClass)**: Processes email, calls APIs via MCP tools, prepares response
2. **Agent 2 (ClaudeGenerateDocumentsClass)**: Creates requested documents using Skills

Implements `IAiProvider` interface:
- `GenerateResponse()`: Generate AI response for email

#### Factories (`Factory/`)
- `EmailProviderFactory.cs`: Creates email provider instances
- `AiProviderFactory.cs`: Creates AI provider instances

#### MCP Servers (`McpServers/`)
Dynamic tool registration system:
- `McpServerLoader.cs`: Loads and manages MCP servers
- `EmailMcpServer.cs`: Built-in email tools
- `HttpMcpServerHandler.cs`: HTTP-based MCP tool executor
- See [MCP Servers Documentation](McpServers/README.md)

#### Core Classes (`Classes/`)
- `ProcessMailsClass.cs`: Main email processing logic and AI prompt building
- `ListNewEmailsClass.cs`: Email fetching logic
- `MailClass.cs`: Email data structure
- `AiResponseClass.cs`: AI response structure
- `StringExtensions.cs`: Utility extensions

## 🔧 Configuration

### Required Settings

Create `appsettings.json` based on `appsettings.json.example`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ClaudeApiKey": "your-anthropic-api-key",
  "DatabaseConnectionString": "server=localhost;database=emailagent;user=root;password=your-password"
}
```

### Database Connection

The application uses MySQL via Entity Framework Core. The connection string is currently in:
- `Program.cs` (should be moved to `appsettings.json`)

### Agent Configuration

Agents are configured via the Admintool interface:
1. **Email Provider**: Choose IMAP, POP3, Exchange, or Inbound
2. **AI Provider**: Currently Claude (configurable for future providers)
3. **MCP Servers**: Add custom tools per agent
4. **System Prompts**: Configured in `ProcessMailsClass.cs`

## 🚀 Running the Agent

### Development

```bash
dotnet run
```

### Production

```bash
dotnet publish -c Release
dotnet "bin/Release/net9.0/UTXO E-Mail Agent.dll"
```

### As a Service

Consider running as a systemd service (Linux) or Windows Service for continuous operation.

## 📊 Workflow

1. **Email Polling**: Agent checks for new emails based on configured interval
2. **Fetch Emails**: Retrieve unread messages via email provider
3. **Load Agent Config**: Get agent settings, MCP servers, and context
4. **Build AI Prompt**: Create system prompt with:
   - Agent instructions
   - Available MCP tools
   - Customer information
   - Previous conversation history (if exists)
   - Email content and attachments
5. **AI Processing**: Send to Claude with multi-turn support for tool usage
6. **Extract Response**: Parse AI response JSON
7. **Handle Attachments**: Process files created by AI
8. **Send Response**: Reply via email provider
9. **Log Conversation**: Store in database for tracking

## 🛠️ Key Features

### Multi-Turn Conversations
The Claude integration supports up to 40 turns, allowing complex tool usage:
```csharp
var options = new QueryOptions
{
    MaxTurns = 40,
    McpServers = mcpServers
};
```

### Attachment Optimization
AI can save files locally and return paths instead of Base64:
```csharp
// AI saves file to /tmp/document.pdf
// Returns: {"attachment": {"path": "/tmp/document.pdf"}}
// System auto-converts to Base64 for email
```

### MCP Server Integration
Dynamic tool registration per agent:
- Email tools (send, forward, search)
- HTTP API tools (custom integrations)
- Database tools (if configured)
- Custom tools per customer

### Conversation History
Previous conversation context is included:
- Last 5 messages from conversation thread
- Attachments from previous replies
- Customer information

## 📝 AI Response Format

The AI must respond in strict JSON format:

```json
{
  "EmailResponseText": "Plain text email response",
  "EmailResponseSubject": "RE: Original Subject",
  "EmailResponseHtml": "<html>HTML formatted response</html>",
  "AiExplanation": "Brief explanation of what was done",
  "attachments": [],
  "MustCreateAttachment": false,
  "AttachmentType": null,
  "AttachmentData": null,
  "AttachmentFilename": null
}
```

### Document Generation Fields

When the customer requests a document (PDF, Word, etc.), the AI should NOT create it directly. Instead:

| Field | Description |
|-------|-------------|
| `MustCreateAttachment` | Set to `true` if a document should be generated |
| `AttachmentType` | Document type: `pdf`, `docx`, `xlsx`, `pptx` |
| `AttachmentData` | Structured text data to include in the document |
| `AttachmentFilename` | Suggested filename (e.g., `Angebot_Murg.pdf`) |

The system will automatically call a second agent to generate the document using Anthropic Skills.

**Example for PDF generation:**
```json
{
  "EmailResponseText": "...",
  "MustCreateAttachment": true,
  "AttachmentType": "pdf",
  "AttachmentData": "INTERNET-ANGEBOT\n\nKunde: Max Mustermann\nAdresse: Musterstraße 1\n\nVerfügbare Tarife:\n1. Basic - 25 Mbit/s - 24,90€",
  "AttachmentFilename": "Angebot_Mustermann.pdf"
}
```

## 🔍 Logging

Extensive console logging for debugging:
- `[EmailPollingService]`: Email polling status
- `[MCP]`: MCP tool registration
- `[MCP Tool Call]`: MCP tool execution
- `[MCP toolname]`: Specific MCP tool details
- `[Skill Download]`: Anthropic Skills file downloads
- `[DocumentGenerator]`: Second agent for document generation
- `[ParseAiResponse]`: JSON parsing from AI response
- `[JSON Attachments]`: Parsed attachment information
- `[API]`: API endpoint processing

## ⚠️ Error Handling

- **Invalid email formats**: Logged and skipped
- **AI errors**: Logged, no response sent
- **Email send failures**: Logged with details
- **MCP tool failures**: Handled gracefully, error message returned to AI
- **Document generation failures**:
  - Email is still sent without attachment
  - Note added to `AiExplanation` informing about the issue
  - All information is included in email text as fallback
- **Attachment file not found**: Logged with warning, attachment skipped
- **API timeouts**: Document generation has 10-minute timeout

## 🧪 Testing

### Manual Testing
1. Configure a test agent in Admintool
2. Send email to agent's address
3. Monitor console logs
4. Check response email

### Tool Testing
Test MCP servers individually via Admintool's MCP Request feature.

## 📦 Dependencies

### Anthropic.SDK

The application uses the **Anthropic.SDK** (unofficial) for .NET to interact with Claude AI. This SDK provides:

- Direct Claude API access
- Skills support (PDF, DOCX, XLSX, PPTX generation)
- Code execution capabilities
- Web search integration
- File download handling

#### Configuration

Add your Anthropic API key to `appsettings.json`:

```json
{
  "Claude": {
    "ApiKey": "sk-ant-api03-..."
  }
}
```

#### How It's Used in This Project

**Main Agent (ClaudeClass.cs):**
```csharp
var client = new AnthropicClient(apiKey, httpClient);

var container = new Container
{
    Skills = new List<Skill>
    {
        new Skill { Type = "anthropic", SkillId = "pdf", Version = "latest" }
    }
};

var parameters = new MessageParameters
{
    Model = AnthropicModels.Claude4Sonnet,
    MaxTokens = 8000,
    Container = container,
    Tools = tools  // MCP tools + built-in tools
};

var response = await client.Messages.GetClaudeMessageAsync(parameters);
```

**Document Generator (ClaudeGenerateDocumentsClass.cs):**
```csharp
// Isolated agent with Skills only (no MCP tools)
var downloadedFiles = await response.DownloadFilesAsync(client, outputDirectory);
```

#### SDK Documentation

- [Anthropic.SDK on NuGet](https://www.nuget.org/packages/Anthropic.SDK)
- [Anthropic.SDK GitHub](https://github.com/tghamm/Anthropic.SDK)
- [Anthropic API Documentation](https://docs.anthropic.com)

### Other Dependencies

Key packages:
- `Anthropic.SDK`: Claude AI integration (v5.9.0+)
- `Microsoft.EntityFrameworkCore`: Database access
- `Pomelo.EntityFrameworkCore.MySql`: MySQL provider
- `Newtonsoft.Json`: JSON processing
- `MailKit`: Email operations (for IMAP/POP3 providers)

## 🔒 Security

- API keys in configuration files (never commit!)
- Email credentials stored encrypted in database
- Input validation for all email content
- Tool permission checks via MCP servers

## 🚧 Future Improvements

- [ ] Support for more AI providers (OpenAI, Azure)
- [ ] Configurable AI model selection
- [ ] Email classification and routing
- [ ] Spam detection integration
- [ ] Webhook support for real-time processing
- [ ] Rate limiting per agent
- [ ] Enhanced attachment handling

## 📚 Related Documentation

- [Main README](../README.md)
- [Admintool README](../UTXO%20E-Mail%20Agent%20Admintool/README.md)
- [MCP Servers Documentation](McpServers/README.md)
- [Database Setup](McpServers/DATABASE_SETUP.md)

---

For support, contact your system administrator.
