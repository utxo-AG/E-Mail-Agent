# Admintool - UTXO E-Mail Agent Admin Interface

A modern Blazor Server web application for managing the UTXO E-Mail Agent system, including clients, agents, administrators, and conversation tracking.

## 🎯 Purpose

The Admintool provides a comprehensive web interface to:
- Manage clients and their subscription packages
- Configure email agents with AI and email providers
- Administer system users with security features
- Monitor conversations and email processing
- Configure MCP servers for custom tools
- Track usage and conversation limits

## 🏗️ Architecture

### Technology Stack
- **Blazor Server**: Interactive server-side web framework
- **MudBlazor**: Material Design component library
- **Entity Framework Core**: Database ORM
- **Cookie Authentication**: Secure session management
- **ASP.NET Core**: Backend services and controllers

### Project Structure

```
Admintool/
├── Components/
│   ├── Layout/                 # Layout components
│   │   ├── MainLayout.razor    # Main app layout with logout
│   │   └── NavMenu.razor       # MudBlazor navigation menu
│   └── Pages/
│       ├── Administrators/      # Admin management
│       ├── Agents/             # Agent configuration
│       ├── Clients/            # Client management
│       └── Login.razor         # Authentication page
├── Controllers/
│   └── AccountController.cs    # Authentication endpoints
├── Services/
│   ├── AuthService.cs          # User authentication logic
│   ├── PasswordService.cs      # Password hashing/generation
│   ├── EmailService.cs         # Password reset emails
│   └── CustomAuthenticationStateProvider.cs
└── wwwroot/                    # Static assets (Bootstrap, CSS)
```

## 🚀 Getting Started

### Prerequisites
- .NET 9.0 SDK
- MySQL Database
- SMTP Server (for password reset emails)

### Configuration

**Database Connection**

Update the connection string in `Program.cs`:
```csharp
var connectionString = "server=your-server;Port=25060;User Id=user;password=pass;database=defaultdb";
```

**SMTP Settings**

Configure in `Services/EmailService.cs`:
```csharp
private const string smtpHost = "smtp.yourdomain.com";
private const int smtpPort = 587;
private const string smtpUsername = "noreply@yourdomain.com";
private const string smtpPassword = "your-smtp-password";
private const string fromAddress = "noreply@yourdomain.com";
private const string fromName = "UTXO E-Mail Agent";
```

**Cookie Settings**

In `Program.cs`, configure authentication:
```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // For production
    });
```

### Running

```bash
cd Admintool
dotnet run
```

Navigate to `https://localhost:5001` (or your configured port).

## 👥 User Management

### Administrators

**Features**:
- Username/password authentication
- Login attempt tracking (max 3 attempts)
- Automatic account blocking
- Password hashing with ASP.NET Identity PasswordHasher
- Password generation and reset

**Security**:
- Passwords hashed using PasswordHasher (not plain text or simple SHA256)
- Failed login tracking in `loginattempts` field
- After 3 failed attempts, `state` set to "blocked"
- Successful login resets attempt counter
- Administrators can unblock accounts via Edit dialog

**Management**:
- Create new administrators
- Edit state (active/blocked)
- Reset login attempts
- Generate new passwords (displayed once)
- **Permanent deletion** (no soft delete)

### Login Process

1. User enters username/password
2. System checks:
   - User exists
   - Account not blocked
   - Password valid (tries PasswordHasher, falls back to SHA256 for legacy)
3. On success:
   - Set authentication cookie
   - Reset login attempts to 0
4. On failure:
   - Increment login attempts
   - Block after 3 attempts

## 🏢 Client Management

### Features
- Full client profile management
- Address and company information
- Username for customer portal access
- Password management with secure hashing
- Package assignment for conversation limits
- Automatic password email delivery

### Package System
Clients can be assigned packages that define:
- Maximum conversations per month
- Pricing information
- Usage tracking

### Client Edit Dialog
- Personal information (name, company, address)
- Country selection dropdown
- Username and password management
- "Generate & Send New Password" button
- Package assignment with visual display

## 🤖 Agent Management

### Agent Configuration
- Email address
- Customer assignment
- Email provider selection (IMAP, POP3, Exchange, Inbound)
- AI provider configuration (Claude)
- Provider-specific settings (host, port, username, password)
- State management (active/inactive)

### MCP Servers
Each agent can have custom MCP servers configured:
- Server name and description
- Enabled/disabled state
- HTTP method (POST, GET, DELETE, UPDATE)
- URL endpoint
- Request body template
- Custom headers

### Conversation Tracking
- View all conversations per agent
- Total conversation count (all-time)
- Current month conversation count
- Color-coded usage indicators:
  - 🟢 Green: < 50% of limit used
  - 🟡 Yellow: 50-90% of limit used
  - 🔴 Red: > 90% of limit used
- Conversation details with attachments
- Email content and AI responses

### MCP Request Testing
Test MCP server endpoints directly from the interface:
- Select server and agent
- Execute request
- View response
- Debug tool integration

## 🎨 UI Components (MudBlazor)

### Navigation
- **MudNavMenu**: Collapsible navigation groups
- **MudNavLink**: Navigation items with icons
- **MudNavGroup**: Grouped menu items (Clients, Agents, Administrators)
- White text for dark sidebar theme

### Data Tables
- **MudTable**: Server-side pagination
- Search functionality
- Sortable columns
- Action buttons (Edit, Delete, View)
- Chip indicators for status

### Forms
- **MudTextField**: Text inputs with validation
- **MudSelect**: Dropdown selections
- **MudButton**: Action buttons
- **MudAlert**: Warning and error messages
- **MudChip**: Status indicators

### Dialogs
- **MudDialog**: Modal dialogs for edit operations
- Form validation
- Async save operations
- Success/error feedback

## 🔐 Security Features

### Password Management
- **Generation**: 12-character random passwords with mixed case, numbers, and symbols
- **Hashing**: ASP.NET Identity PasswordHasher
- **Verification**: Supports both new (PasswordHasher) and legacy (SHA256) formats
- **Email Delivery**: Secure SMTP delivery of new passwords

### Authentication
- Cookie-based sessions
- 30-minute timeout with sliding expiration
- Automatic logout on inactivity
- Logout button in header
- Protected routes with `[Authorize]` attribute

### Input Validation
- Required field validation
- Email format validation
- Password confirmation matching
- Duplicate username prevention
- Form validation before submission

## 📊 Dashboard & Monitoring

### Home Page
- System overview
- Quick stats
- Recent activity (future enhancement)

### Conversation View
- List all conversations for an agent
- Email date and subject
- Response status
- View full conversation thread
- Attachment display

### Usage Tracking
- Per-agent conversation counts
- Package limit monitoring
- Monthly reset tracking
- Visual indicators

## 🛠️ Development

### Adding New Pages

1. Create Razor component in `Components/Pages/`
2. Add `@page` directive with route
3. Add `@rendermode InteractiveServer`
4. Inject required services
5. Add to NavMenu if needed

Example:
```razor
@page "/mypage"
@rendermode InteractiveServer
@attribute [Authorize]
@inject IDbContextFactory<DefaultdbContext> DbFactory

<PageTitle>My Page</PageTitle>

<MudText Typo="Typo.h3">My Page</MudText>

@code {
    // Component logic
}
```

### Adding Services

1. Create service class in `Services/`
2. Register in `Program.cs`:
```csharp
builder.Services.AddScoped<MyService>();
```
3. Inject in components:
```csharp
@inject MyService MyService
```

## 📦 Key Dependencies

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" />
<PackageReference Include="Pomelo.EntityFrameworkCore.MySql" />
<PackageReference Include="MudBlazor" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
<PackageReference Include="MailKit" />
```

## ⚠️ Important Notes

### Before Deployment

1. **Move connection strings to `appsettings.json`**
2. **Never commit `appsettings.json`** (already in `.gitignore`)
3. **Set `CookieSecurePolicy.Always` for production**
4. **Configure HTTPS**
5. **Update SMTP credentials**
6. **Set strong admin passwords**

### Database Scaffold

If you need to regenerate models:
```bash
dotnet ef dbcontext scaffold "connection-string" Pomelo.EntityFrameworkCore.MySql \
  -o Models \
  --project 'UTXO E-Mail Agent Shared' \
  --force
```

## 🐛 Troubleshooting

### Login Issues
- Check console logs for `[AuthService]` messages
- Verify password hashing method
- Check `loginattempts` and `state` fields
- Ensure database connection

### MudBlazor Styling Issues
- Verify `MudProviders.razor` is included
- Check browser console for CSS errors
- Clear browser cache

### Password Reset Not Sending
- Verify SMTP settings in `EmailService.cs`
- Check firewall rules for SMTP port
- Test SMTP credentials independently

## 📚 Related Documentation

- [Main README](../README.md)
- [UTXO E-Mail Agent README](../UTXO%20E-Mail%20Agent/README.md)
- [MudBlazor Documentation](https://mudblazor.com/)
- [Blazor Documentation](https://docs.microsoft.com/aspnet/core/blazor)

---

For support, contact your system administrator.
