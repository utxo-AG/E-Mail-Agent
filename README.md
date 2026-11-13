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
- **MariaDB / MySQL**: Database (via Pomelo.EntityFrameworkCore.MySql)
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

The database connection is resolved centrally by `DatabaseConfig`
(`UTXO E-Mail Agent Shared/Models/DatabaseConfig.cs`) in the following order:

1. Individual environment variables `DB_HOST` / `DB_PORT` / `DB_USER` / `DB_PASSWORD` / `DB_NAME`
2. A full connection string via `UTXO_DB_CONNECTION`
3. `ConnectionStrings:DefaultConnection` from `appsettings.json`

This lets you run locally via `appsettings.json` and in containers (e.g. rstcloud)
purely via environment variables.

The target database is **MariaDB** (MySQL also works). No EF Core migrations are used
for the `DefaultdbContext` — the schema is created directly from the model.

#### Creating the schema (tables)

To build the complete database and all tables from the model, start the **Admintool**
with the environment variable **`DB_INIT=true`** set (alternatively pass the
`--init-db` argument). On startup it runs `EnsureCreated()`, which:

- creates the database and all tables if they do **not** yet exist, and
- leaves an existing database untouched.

The Admintool then continues to run normally. The log shows either
`[DB-Init] Datenbank und Tabellen wurden neu erstellt.` or
`[DB-Init] Datenbank existiert bereits – kein Schema-Aufbau nötig.`

#### Default administrator

Because `EnsureCreated()` only creates the schema (no data), the init step also
creates a **default administrator** — but only if no administrator exists yet:

- Username: `admin` (override with `ADMIN_USERNAME`)
- Password: taken from `ADMIN_PASSWORD` if set, otherwise a secure random password
  is generated and **printed to the console/log**:

  ```
  ═══════════════════════════════════════════════════
  [DB-Init] Standard-Administrator wurde angelegt:
  [DB-Init]   Benutzername: admin
  [DB-Init]   Passwort:     <generated password>
  [DB-Init]   >> Bitte sicher notieren und nach dem ersten Login ändern!
  ═══════════════════════════════════════════════════
  ```

> **Notes**
> - `DB_INIT` is only honoured by the Admintool; the other services ignore it.
> - The `DB_USER` must be allowed to run `CREATE DATABASE` if `DB_NAME` does not exist yet.
> - The default admin is only created when the `administrators` table is empty — it is safe to leave `DB_INIT=true` on, but removing it after the first run is cleaner.
> - A generated password ends up in the container logs. Use `ADMIN_PASSWORD` if you want to avoid that, and change the password after the first login.

#### Automatic schema migrations

Because the model is database-first (no EF Core migrations), incremental schema changes
are applied by a lightweight migration runner. On **every** Admintool start, pending SQL
steps are applied automatically and tracked in a `schema_migrations` table, so each step
runs only once — no external database access required. If the database is not reachable or
not yet initialized, the step is skipped (start once with `DB_INIT=true` first).

New changes are added as entries in `DatabaseMigrator`
(`UTXO E-Mail Agent Shared/Models/DatabaseMigrator.cs`).

### Environment Variables

Hierarchical .NET settings use a **double underscore** `__` as the separator
(e.g. `Claude:ApiKey` → `Claude__ApiKey`). Environment variables override `appsettings.json`.

**Database — required for all four services:**

| Variable            | Required | Default            | Description                          |
|---------------------|----------|--------------------|--------------------------------------|
| `DB_HOST`           | yes      | –                  | MariaDB host                         |
| `DB_PORT`           | no       | `3306`             | Database port                        |
| `DB_USER`           | yes      | `root`             | Database user                        |
| `DB_PASSWORD`       | yes      | *(empty)*          | Database password                    |
| `DB_NAME`           | yes      | `utxo_email_agent` | Database name                        |
| `DB_SERVER_VERSION` | no       | `11.4.0-mariadb`   | EF Core server version               |
| `UTXO_DB_CONNECTION`| no       | –                  | Full connection string (alternative to the `DB_*` vars) |

**Admintool only — first deploy:**

| Variable         | Required | Default | Description                                                        |
|------------------|----------|---------|--------------------------------------------------------------------|
| `DB_INIT`        | no       | –       | `true` creates the schema + default admin on startup; remove afterwards |
| `ADMIN_USERNAME` | no       | `admin` | Username of the default administrator                              |
| `ADMIN_PASSWORD` | no       | *(random)* | Password of the default administrator; if unset, a random one is generated and printed to the log |

**Functional settings per service** (`__` separator):

| Service   | Variables |
|-----------|-----------|
| Admintool | `Claude__ApiKey`, `Email__ApiUrl`, `Email__BearerToken`, `Email__FromEmail`, `Email__FromName`, `AzureAd__TenantId`, `AzureAd__ClientId`, `AzureAd__ClientSecret`, `AzureAd__RedirectUri`, `AgentApi__BaseUrl` |
| Agent     | `Claude__ApiKey`, `Email__ApiUrl`, `Email__BearerToken`, `AppSettings__PollingIntervalSeconds` (default `60`), `ClaudeCode__McpServerPath`, `ClaudeCode__WorkingDirectory`, `Python__Path` (+ `AzureAd__*` if Office365 is used; `RstCloud__ApiKey` and optional `RstCloud__Endpoint` if the rstcloud/Qwen provider is used) |
| Api       | `Email__BearerToken`, `WebhookSecret`, `WebhookVerificationToken`, `AgentApiUrl` |
| McpServer | *(only the `DB_*` variables above)* |

### Application Settings (local development)

For local development, create `appsettings.json` files based on the provided
`.example` files (the real `appsettings.json` are git-ignored). At minimum set
`ConnectionStrings:DefaultConnection`; the functional keys mirror the table above
(e.g. `Claude:ApiKey`, `Email:BearerToken`).

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

## 🐳 Docker

Each application has its own Dockerfile:

| Application | Dockerfile                               | Base image     | Port            |
|-------------|------------------------------------------|----------------|-----------------|
| Agent       | `UTXO E-Mail Agent/Dockerfile`           | `aspnet:9.0`   | 8080            |
| Api         | `UTXO E-Mail Agent Api/Dockerfile`       | `aspnet:9.0`   | 8080            |
| Admintool   | `UTXO E-Mail Agent Admintool/Dockerfile` | `aspnet:9.0`   | 8080 / 8081     |
| McpServer   | `UTXO E-Mail Agent McpServer/Dockerfile` | `runtime:9.0`  | – (stdio only)  |

All images share the **repository root** as build context (because of the shared
project reference), so always build with `-f` from the repo root:

```bash
docker build -f "UTXO E-Mail Agent/Dockerfile"           -t utxo-agent .
docker build -f "UTXO E-Mail Agent Api/Dockerfile"       -t utxo-api .
docker build -f "UTXO E-Mail Agent Admintool/Dockerfile" -t utxo-admintool .
docker build -f "UTXO E-Mail Agent McpServer/Dockerfile" -t utxo-mcpserver .
```

Configure the containers via the environment variables listed above. To create the
schema on first deploy, start the Admintool once with `DB_INIT=true`:

```bash
docker run -e DB_INIT=true \
  -e DB_HOST=... -e DB_USER=... -e DB_PASSWORD=... -e DB_NAME=... \
  -p 8080:8080 utxo-admintool
```

> The McpServer communicates over STDIO (no network port). It only belongs in a
> container if an MCP client launches it as a subprocess (`docker run -i`); it is not
> a standalone network service.

## 📝 Initial Setup

1. **Database Initialization**: Create the schema by starting the Admintool with `DB_INIT=true` (see [Database Setup](#database-setup))
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
