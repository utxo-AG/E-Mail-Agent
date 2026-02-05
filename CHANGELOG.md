# Changelog

All notable changes to the UTXO E-Mail Agent will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
