# Configuration Guide

## Database Connection String

### ✅ Valid MySQL Connection String Format

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;user=root;password=your-password;database=emailagent"
  }
}
```

### MySQL Connection String Parameters

| Parameter | Required | Example | Description |
|-----------|----------|---------|-------------|
| `server` | ✅ Yes | `localhost` or `192.168.1.100` | MySQL server hostname or IP |
| `port` | ⚠️ Optional | `3306` (default) | MySQL server port |
| `user` | ✅ Yes | `root` or `your-username` | MySQL username |
| `password` | ✅ Yes | `your-secure-password` | MySQL password |
| `database` | ✅ Yes | `emailagent` | Database name |

### Additional Optional Parameters

```
server=localhost;port=3306;user=root;password=pass;database=emailagent;pooling=false;old guids=true
```

- `pooling=false`: Disable connection pooling
- `old guids=true`: Use old GUID format for compatibility

### ❌ SQL Server Options NOT Supported

These options work in SQL Server but **NOT in MySQL**:

- ❌ `Trusted_Connection=True` → **Use `user` and `password` instead**
- ❌ `User Id` → **Use `user` instead**
- ❌ `Integrated Security` → **Not supported in MySQL**
- ❌ `Persist Security Info` → **Not needed**

## SMTP Configuration (Admintool only)

```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "NMKR E-Mail Agent"
  }
}
```

### Gmail Configuration

If using Gmail, you need to:
1. Enable 2-Factor Authentication
2. Generate an **App Password** (not your regular password)
3. Use the App Password in `SmtpPassword`

## Setup Steps

### 1. Admintool Configuration

```bash
cd Admintool
cp appsettings.json.example appsettings.json
```

Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=your-server;port=3306;user=your-user;password=your-pass;database=emailagent"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUsername": "your-actual-email@gmail.com",
    "SmtpPassword": "your-actual-app-password",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "NMKR E-Mail Agent"
  }
}
```

### 2. Email Agent Configuration

```bash
cd "NMKR E-Mail Agent"
cp appsettings.json.example appsettings.json
```

Edit `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=your-server;port=3306;user=your-user;password=your-pass;database=emailagent"
  },
  "AppSettings": {
    "PollingIntervalSeconds": "60"
  },
  "Claude": {
    "ApiKey": "sk-ant-api03-..."
  }
}
```

### 3. Claude API Key (Environment Variable)

Set the Claude API key as an environment variable:

**macOS/Linux:**
```bash
export ANTHROPIC_API_KEY="sk-ant-api03-your-key"
```

**Windows (PowerShell):**
```powershell
$env:ANTHROPIC_API_KEY="sk-ant-api03-your-key"
```

**Permanently (Linux/macOS):**
```bash
echo 'export ANTHROPIC_API_KEY="sk-ant-api03-your-key"' >> ~/.bashrc
# or for zsh:
echo 'export ANTHROPIC_API_KEY="sk-ant-api03-your-key"' >> ~/.zshrc
```

## Testing Configuration

### Test Database Connection

**Admintool:**
```bash
cd Admintool
dotnet run
```

Should output: `✓ Connection to database successful`

**Email Agent:**
```bash
cd "NMKR E-Mail Agent"
dotnet run
```

Should output: `✓ Found X active agent(s)`

### Test SMTP (Admintool)

1. Start Admintool
2. Navigate to Administrators
3. Edit an administrator
4. Click "Generate & Send New Password"
5. Check if email arrives

## Common Errors

### Error: `Option 'trusted_connection' not supported`

**Cause:** Using SQL Server connection string format with MySQL

**Fix:** Remove `Trusted_Connection=True` and use:
```
server=localhost;port=3306;user=root;password=your-pass;database=emailagent
```

### Error: `Connection string 'DefaultConnection' not found`

**Cause:** `appsettings.json` missing or incorrectly formatted

**Fix:**
1. Copy `appsettings.json.example` to `appsettings.json`
2. Verify JSON syntax (use JSON validator)
3. Ensure `ConnectionStrings` section exists

### Error: `Access denied for user`

**Cause:** Wrong MySQL username/password

**Fix:**
1. Verify MySQL credentials
2. Test connection with MySQL client:
   ```bash
   mysql -h localhost -P 3306 -u root -p
   ```
3. Update `appsettings.json` with correct credentials

### Error: `Unknown database`

**Cause:** Database doesn't exist

**Fix:**
1. Create database:
   ```sql
   CREATE DATABASE emailagent;
   ```
2. Or change database name in connection string to existing database

## Security Best Practices

1. ✅ **Never commit `appsettings.json`** - it's in `.gitignore`
2. ✅ **Use strong passwords** - minimum 12 characters
3. ✅ **Use App Passwords** for Gmail, not account password
4. ✅ **Restrict database user permissions** - only grant necessary privileges
5. ✅ **Use environment variables** for production secrets
6. ✅ **Enable SSL** for production SMTP (`port=465` with SSL)

## Production Recommendations

### Database Connection

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=prod-server.example.com;port=3306;user=emailagent_user;password=${DB_PASSWORD};database=emailagent;sslmode=Required"
  }
}
```

Use environment variable `DB_PASSWORD` instead of hardcoded password.

### SMTP Configuration

```json
{
  "Email": {
    "SmtpHost": "smtp.sendgrid.net",
    "SmtpPort": "587",
    "SmtpUsername": "apikey",
    "SmtpPassword": "${SENDGRID_API_KEY}",
    "FromEmail": "noreply@yourdomain.com",
    "FromName": "NMKR E-Mail Agent"
  }
}
```

Consider using dedicated SMTP services like:
- SendGrid
- Mailgun
- AWS SES
- Postmark

## Support

For configuration issues:
1. Check this guide
2. Review logs in console output
3. Verify all required fields are filled
4. Test each component separately

---

Last updated: 2026-02-05
