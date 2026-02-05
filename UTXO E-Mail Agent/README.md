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
- **Claude** (`ClaudeClass.cs`): Anthropic Claude AI integration
  - Multi-turn conversation support
  - Tool usage with MCP servers
  - File creation and Base64 conversion
  - Configurable max turns (currently 40)

Implements `IAiProvider` interface:
- `GetReplyAsync()`: Generate AI response for email

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
  "response": "The email text response",
  "attachments": [
    {
      "filename": "document.pdf",
      "contentType": "application/pdf",
      "content": "base64-encoded-content-or-null",
      "path": "/absolute/path/to/file"
    }
  ]
}
```

## 🔍 Logging

Extensive console logging for debugging:
- `[Claude Turn X]`: AI turn tracking
- `[EmailProvider]`: Email operations
- `[ProcessMails]`: Processing flow
- `[McpServer]`: Tool usage

## ⚠️ Error Handling

- Invalid email formats: Logged and skipped
- AI errors: Logged, no response sent
- Email send failures: Logged with details
- MCP tool failures: Handled gracefully

## 🧪 Testing

### Manual Testing
1. Configure a test agent in Admintool
2. Send email to agent's address
3. Monitor console logs
4. Check response email

### Tool Testing
Test MCP servers individually via Admintool's MCP Request feature.

## 📦 Dependencies

Key packages:
- `Claude.AgentSdk`: Claude AI integration
- `Microsoft.EntityFrameworkCore`: Database access
- `Pomelo.EntityFrameworkCore.MySql`: MySQL provider
- `Newtonsoft.Json`: JSON processing
- `MailKit`: Email operations (for some providers)

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
- [Admintool README](../Admintool/README.md)
- [MCP Servers Documentation](McpServers/README.md)
- [Database Setup](McpServers/DATABASE_SETUP.md)

---

For support, contact your system administrator.
