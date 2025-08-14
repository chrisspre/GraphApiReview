# ApiReview Tools

A collection of CLI tools for Azure DevOps workflow optimization.

## Projects

### 🔍 gapir (Graph API Review)
A CLI tool to check Azure DevOps pull requests assigned to you for review.

### 🔗 kurz (URL Shortener Service)
A web service for creating short URLs used by the gapir tool for cleaner output. Features Base62 encoding for compact PR URLs.

## Features

### gapir Features
- 🔍 **Pull Request Checker**: View PRs assigned to you for review
- 🔐 **Modern Authentication**: Brokered authentication (Windows Hello/PIN) with device code fallback
- 🌐 **Cross-platform**: Works on Windows, macOS, and Linux
- 💾 **Token Caching**: Remembers your authentication for faster subsequent runs
- 🧹 **Clean Titles**: Automatically cleans and shortens PR titles for better readability
- 🔗 **Short URLs**: Integrates with kurz service for compact URL display using Base62 encoding

### kurz Features
- 🌐 **Web Service**: HTTP API for URL shortening
- 🔢 **Base62 Encoding**: Compact PR URLs using Base62 encoding (e.g., w8t8c instead of 12041652)
- 🔍 **Smart Detection**: Automatically detects Base62 vs decimal PR IDs
- 🏃 **Fast Redirects**: High-performance redirect handling
- 🔧 **Self-hostable**: Can be deployed as Windows service or web app
- 📍 **Custom Domain**: Uses short 'g' domain for minimal URL length

## Installation

### gapir Installation

**Option 1: Using the Install Script (Recommended)**
Use the provided PowerShell script for easy installation:

```powershell
cd src\gapir
.\install-tool.ps1
```

This script will:
- Build the project in Release mode
- Pack it as a NuGet package
- Uninstall any existing version
- Install the new version globally

**Option 2: Manual Installation**
Install as a global .NET tool:

```bash
dotnet tool install --global GraphApiReview
```

### kurz Installation
See [kurz README.md](src/kurz/README.md) for detailed installation instructions.

## Usage

Run the tool from anywhere to check your Azure DevOps pull requests:

```bash
gapir
```

On first run, it will:
1. Try Windows Authentication Broker (Windows Hello/PIN/Biometrics) first
2. Fall back to device code flow if brokered auth fails
3. Auto-open browser and copy device code to clipboard (fallback mode)
4. Cache your token for future runs

## Configuration

Currently, the tool is configured for:
- Organization: `https://dev.azure.com/your-org/`
- Project: `YourProject`
- Repository: `YourRepository`

To modify these settings, you'll need to update the source code and rebuild.

## Output

The tool shows two lists:

### ✅ Approved PRs (Table Format)
```
Author             | Title                                    | URL
-------------------+------------------------------------------+--------------------
John Smith         | Feature Add new validation logic        | https://example...
Jane Doe           | Fix Update configuration handling       | https://example...
```

### ⏳ Pending PRs (Detailed Format)
```
ID: 12345
Title: Clean Title
Author: John Doe
Status: Active
Created: 2025-01-01 12:00:00
URL: https://...
Reviewers:
  - Jane Smith: No vote
  - Bob Johnson: Approved
```

## Development

### Building from Source

```bash
git clone <repository-url>
cd ApiReview
dotnet build
```

## Requirements

- .NET 9.0 or later
- Access to Azure DevOps organization
- Windows (for brokered authentication and clipboard/browser features)
- Windows 10 version 1903 or later (for optimal brokered authentication experience)

## Token Storage

Authentication tokens are securely cached in:
`%LocalAppData%\gapir\msalcache.bin`

## Architecture

The project is organized into:
- `Program.cs` - Main application logic and PR checking
- `ConsoleAuth.cs` - Reusable authentication module using MSAL
- `TokenCacheHelper.cs` - Token persistence helper

The `ConsoleAuth` class can be easily reused in other Azure DevOps tools.

## Uninstall

```bash
dotnet tool uninstall --global GraphApiReview
```

## Development & Contributing

For developers wanting to contribute or understand the codebase:

- **[DEVELOPMENT.md](DEVELOPMENT.md)** - Architecture decisions, patterns, and development history
- **[DECISIONS.md](DECISIONS.md)** - Detailed log of design choices and rationale
- **[.copilot-context.md](.copilot-context.md)** - GitHub Copilot context for consistent development

These files preserve the decision-making process and architectural context to help future maintainers and GitHub Copilot understand the reasoning behind the current implementation.

## License

MIT License - see LICENSE file for details.
