# UTXO E-Mail Agent System

A comprehensive AI-powered email processing system with multi-provider support, intelligent response generation, and a full-featured admin interface.

## 🚀 Overview

The UTXO E-Mail Agent System consists of three main components that work together to provide automated, intelligent email processing:

1. **UTXO E-Mail Agent**: Core email processing engine with AI integration
2. **Admintool**: Blazor-based web admin interface
3. **UTXO E-Mail Agent Shared**: Shared models and database context

## 📋 Features

### Email Processing
- **Multiple Email Providers**: Support for IMAP, POP3, Exchange, and Inbound API
- **AI-Powered Responses**: Integration with Claude AI for intelligent email handling
- **MCP Server Support**: Dynamic tool registration via Model Context Protocol
- **Attachment Processing**: Automatic file handling and Base64 conversion
- **Multi-Turn Conversations**: Advanced conversation management with Claude SDK

### Admin Interface
- **Client Management**: Create and manage clients with package-based limits
- **Agent Configuration**: Set up email agents with custom AI and email providers
- **Administrator Management**: User authentication with security features
- **Conversation Tracking**: View conversation history and statistics
- **MCP Server Management**: Configure custom tools per agent
- **Package Management**: Set conversation limits per customer

### Security Features
- **Password Hashing**: ASP.NET Identity PasswordHasher implementation
- **Login Attempt Tracking**: Automatic account blocking after 3 failed attempts
- **Cookie-Based Authentication**: Secure session management
- **Password Reset**: Generate and send new passwords securely

## 🏗️ Architecture

```
UTXO E-Mail Agent System/
├── UTXO E-Mail Agent/          # Core email processing engine
│   ├── AiProvider/             # AI provider implementations
│   ├── EmailProvider/          # Email provider implementations
│   ├── Factory/                # Factory patterns for providers
│   ├── McpServers/             # MCP server management
│   └── Classes/                # Core business logic
├── Admintool/                  # Blazor Server admin interface
│   ├── Components/             # Razor components
│   ├── Services/               # Business services
│   └── Controllers/            # API controllers
└── UTXO E-Mail Agent Shared/   # Shared models and database context
    └── Models/                 # EF Core models
```

## 🛠️ Technology Stack

- **.NET 9.0**: Latest .NET framework
- **Blazor Server**: Interactive web UI
- **MudBlazor**: Material Design component library
- **Entity Framework Core**: Database ORM
- **MySQL**: Database (via Pomelo.EntityFrameworkCore.MySql)
- **Claude AI SDK**: AI integration
- **MailKit**: Email handling
- **ASP.NET Core Identity**: Authentication and password management

## 🎬 Getting Started

### Clone the Repository

This project uses git submodules for the Claude Agent SDK. Clone with submodules:

```bash
# Clone with submodules
git clone --recursive https://github.com/utxo-AG/E-Mail-Agent.git

# Or if you already cloned without --recursive:
git submodule update --init --recursive
```

### Build the Solution

```bash
cd E-Mail-Agent
dotnet restore "UTXO E-Mail Agent.sln"
dotnet build "UTXO E-Mail Agent.sln"
```

## 📦 Prerequisites

- .NET 9.0 SDK
- MySQL Database
- Claude Code CLI (required for Claude Agent SDK)
  - macOS/Linux: `brew install anthropics/claude/claude-code`
  - Windows: `winget install Anthropic.ClaudeCode`
  - npm: `npm install -g @anthropics/claude-code`
  - Then authenticate: `claude-code auth login`
- Claude API Key (for AI features)
- Inbound API Key (for email sending)

## ⚙️ Configuration

### Database Setup

1. Update connection strings in:
   - `Admintool/Program.cs`
   - `UTXO E-Mail Agent Shared/Models/DefaultdbContext.cs`

2. Run EF Core migrations (if needed):
   ```bash
   dotnet ef database update --project "UTXO E-Mail Agent Shared"
   ```

### Application Settings

Create `appsettings.json` files based on the provided `.example` files:

**UTXO E-Mail Agent/appsettings.json**:
```json
{
  "ClaudeApiKey": "your-claude-api-key",
  "DatabaseConnectionString": "your-mysql-connection-string"
}
```

**Admintool** configuration is in `Program.cs` (consider moving to `appsettings.json`).

### SMTP Configuration

Update `EmailService.cs` in Admintool with your SMTP settings:
- SMTP Host
- SMTP Port
- SMTP Credentials
- From Address

## 🚀 Running the Applications

### Email Processing Agent

```bash
cd "UTXO E-Mail Agent"
dotnet run
```

### Admin Interface

```bash
cd Admintool
dotnet run
```

Navigate to `https://localhost:5001` (or the configured port).

## 📝 Initial Setup

1. **Database Initialization**: Ensure the database schema exists
2. **Create First Administrator**: Use the "Add Administrator" feature
3. **Add Packages**: Define conversation packages with limits
4. **Create Clients**: Add clients and assign packages
5. **Configure Agents**: Set up agents with email and AI providers
6. **MCP Servers**: Optionally add custom MCP tools

## 🔐 Security Considerations

### ⚠️ IMPORTANT: Before Deploying

1. **Remove Hardcoded Credentials**:
   - Move database connection strings to `appsettings.json`
   - Never commit `appsettings.json` to Git (already in `.gitignore`)

2. **Update SMTP Settings**:
   - Configure secure SMTP credentials
   - Use environment variables for production

3. **Cookie Security**:
   - Set `CookieSecurePolicy.Always` in production
   - Configure HTTPS

4. **Password Policy**:
   - Minimum 8 characters enforced
   - Consider adding complexity requirements

## 📖 Documentation

See project-specific README files:
- [UTXO E-Mail Agent README](UTXO%20E-Mail%20Agent/README.md)
- [Admintool README](Admintool/README.md)
- [MCP Servers Documentation](UTXO%20E-Mail%20Agent/McpServers/README.md)

## 🤝 Contributing

This is a private project. For questions or issues, contact the development team.

## 📄 License

Proprietary - All Rights Reserved

## 🛟 Support

For technical support, please contact your system administrator.

---

**Note**: This system was developed with assistance from Claude Code.
