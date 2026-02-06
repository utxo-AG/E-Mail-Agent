# Changelog

All notable changes to the UTXO E-Mail Agent will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-02-06

### Added
- **JsonStringParameter wrapper class** for Claude CLI 2.1.31+ compatibility
  - Provides structured schema that newer Claude CLI versions require
  - Maintains flexibility for different customer API schemas
  - Future-proof solution that works with all Claude CLI versions

### Changed
- MCP tool handler now uses `JsonStringParameter` wrapper instead of plain string
- Tool signature: `Func<JsonStringParameter, Task<string>>`
- Claude passes parameters as: `{"json": "{\"zip\": \"79790\", \"city\": \"Küssaberg\", ...}"}`

### Required Database Update
Update MCP server descriptions in database to use wrapper format:
```
Prüft Internet-Verfügbarkeit.
WICHTIG: Übergebe ein Objekt mit dem Feld "json" das einen JSON-String enthält:
{
  "json": "{\"zip\": \"79790\", \"city\": \"Küssaberg\", \"street\": \"Freudenspiel 70\"}"
}
```

## [1.2.2] - 2026-02-06

### Fixed
- **MCP Tool Parameter Handling**: Reverted to simple string parameter for maximum flexibility
  - Each customer can define their own API schema in the database Description field
  - Claude learns the parameter format from the tool description
  - No need for hardcoded parameter classes
  - Tool signature: `Func<string, Task<string>>`

### Technical Details
- The key insight: Claude can handle string parameters if the tool description explicitly explains the JSON format
- Example description format:
  ```
  Prüft Internet-Verfügbarkeit. Erstelle einen STRING, welcher im JSON
  Format mit folgender Struktur aufgebaut ist:
  {
    "zip": "79798",
    "city": "Jestetten",
    "street": "Birkenstrasse 8"
  }
  Wichtig ist, Du musst dies als String Parameter an den MCP Server übergeben.
  ```
- This approach allows different customers to have completely different API schemas
- Each MCP server configuration in the database defines its own parameter structure

## [1.2.1] - 2026-02-06

### Fixed
- **CRITICAL**: Fixed type mismatch in MCP tool handler causing silent execution failures
  - Root cause: `ExecuteAsync` method expected `Dictionary<string, JsonElement>` but `CreateToolHandler` returned `Func<string, Task<string>>`
  - Type mismatch caused SDK to silently fail when dispatching tool calls
  - Tools appeared in Claude's logs (ToolUseBlock) but handler never executed
  - Fixed by making both signatures consistent: `Func<Dictionary<string, JsonElement>, Task<string>>`
  - Handler now properly converts Dictionary to JSON string for HTTP API calls
  - This fix ensures MCP tools actually execute when Claude calls them

### Technical Details
- The inconsistent type signatures prevented the SDK from properly dispatching calls
- Dictionary<string, JsonElement> provides flexibility for different customer API schemas
- SDK generates schema with `additionalProperties` allowing arbitrary JSON structures
- Each customer can define their own JSON format in the MCP server description

## [1.2.0] - 2026-02-05

### Added
- **Interactive Test Mode**: Press 't' during wait period to test AI responses without sending emails
  - Uses predefined test email content for quick testing
  - Executes full AI processing including MCP tool calls
  - Shows AI response without actually sending emails
  - Perfect for debugging MCP servers and AI responses

### Fixed
- **CRITICAL**: MCP tools now execute correctly when called by Claude
  - Changed tool handler signature from `Func<JsonElement, Task<string>>` to `Func<Dictionary<string, JsonElement>, Task<string>>`
  - Root cause: Claude Agent SDK explicitly excludes `JsonElement` from complex object handling
  - SDK now generates proper input schema with `additionalProperties` support
  - HTTP-based MCP servers defined in database can now be called successfully
  - Tools no longer fail silently - handlers execute and log their activity

### Technical Details
- The SDK uses reflection to inspect delegate signatures for parameter binding
- `JsonElement` parameter type resulted in empty schema generation
- `Dictionary<string, JsonElement>` generates proper JSON schema for dynamic parameters
- This fix is essential for all database-configured MCP servers to function

## [1.1.0] - 2026-02-05

### Added
- Version number display on startup for easy deployment verification
- Git submodule support for Claude Agent SDK
- Comprehensive Claude Agent SDK documentation in README
- Clone instructions for repository with submodules

### Fixed
- MCP tool handler now uses `JsonElement` parameter instead of `string`
- MCP tools now execute correctly (was failing silently before)
- Claude Agent SDK can now properly deserialize JSON arguments to tool handlers

### Changed
- Updated EmailService in Admintool to use Inbound API instead of SMTP
- Renamed project from "NMKR E-Mail Agent" to "UTXO E-Mail Agent"
- Renamed Admintool to "UTXO E-Mail Agent Admintool"
- Updated all namespaces to use UTXO_E_Mail_Agent prefix
- Inbound API configuration now uses `ApiUrl` and `BearerToken` settings

## [1.0.0] - 2026-01-30

### Initial Release
- Multi-provider email support (IMAP, POP3, Exchange, Inbound API)
- Claude AI integration with multi-turn conversations
- MCP (Model Context Protocol) server support
- Blazor-based admin interface for managing clients, agents, and administrators
- Conversation tracking and history
- Package-based conversation limits
- Password reset functionality
- Attachment handling with Base64 conversion
- Docker support

---

## Version Number Guidelines

When updating the version:

1. **Update `Program.cs`**:
   - Increment `Version` constant (e.g., "1.1.0" → "1.2.0")
   - Update `BuildDate` constant to current date

2. **Update `CHANGELOG.md`**:
   - Add new version section at the top
   - Document all changes in appropriate categories:
     - **Added** for new features
     - **Changed** for changes in existing functionality
     - **Deprecated** for soon-to-be removed features
     - **Removed** for now removed features
     - **Fixed** for any bug fixes
     - **Security** for vulnerability fixes

3. **Version Numbering** (Semantic Versioning):
   - **MAJOR** (X.0.0): Breaking changes, major refactoring
   - **MINOR** (1.X.0): New features, backwards compatible
   - **PATCH** (1.1.X): Bug fixes, minor improvements
