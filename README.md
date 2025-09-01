# ApiReview Tools

A collection of CLI tools for Azure DevOps workflow optimization.

## gapir (Graph API Review)

A CLI tool to check Azure DevOps pull requests assigned to you for review.

**Features:**
- **Pull Request Checker**: View PRs assigned to you for review
- **JSON Output**: Structured JSON output for automation and integration (`--format Json`)
- **Performance Optimized**: Only fetches approved PRs when requested (`--approved`)
- **Modern Authentication**: Brokered authentication (Windows Hello/PIN) with device code fallback  
- **Cross-platform**: Works on Windows, macOS, and Linux
- **Token Caching**: Remembers your authentication for faster subsequent runs
- **Clean Titles**: Automatically cleans and shortens PR titles for better readability
- **Age Tracking**: Shows how long PRs have been waiting for review
- **Smart Filtering**: Focus on what needs your attention

📖 **[Architecture Documentation](ARCHITECTURE.md)** - Detailed technical documentation, design decisions, and development patterns

## Quick Start

### Install as Global Tool
```bash
dotnet tool install --global --add-source ./nupkg gapir
gapir
```

### Run from Source
```bash
git clone https://github.com/chrisspre/GraphApiReview.git
cd GraphApiReview
dotnet run --project src/gapir
```

## 📋 Usage

```bash
# Basic usage - show pending reviews
gapir

# Show approved PRs too (performance: only fetches when requested)
gapir --approved

# JSON output for automation/integration
gapir --format Json

# JSON output with approved PRs
gapir --format Json --approved

# Use full URLs instead of short links
gapir --full-urls

# Show detailed timing (slower)
gapir --detailed-timing

# Verbose output for troubleshooting
gapir --verbose
```

### JSON Output Format

When using `--format Json`, gapir outputs clean structured data:

```json
{
  "title": "gapir (Graph API Review) - Azure DevOps Pull Request Checker",
  "pendingPRs": [
    {
      "id": 12345,
      "title": "Add new API endpoint",
      "authorName": "John Doe",
      "createdDate": "2025-08-29T10:00:00Z",
      "url": "https://msazure.visualstudio.com/...",
      "myVoteStatus": "NoVote",
      "isApprovedByMe": false
    }
  ],
  "approvedPRs": [
    // Only included when --approved is specified
  ],
  "errorMessage": null
}
```

**Performance Note**: The `approvedPRs` property is only populated when `--approved` is specified, avoiding expensive queries when not needed.

## 🔗 kurz (URL Shortener Service)

A lightweight web service for creating short URLs used by the gapir tool for cleaner terminal output.

**Features:**
- 🔗 **Base62 Encoding**: Creates compact PR URLs like `http://g/pr/OwAc` (Base62) or `http://g/pr/12041652` (decimal)
- ⚡ **Fast Redirects**: Lightweight ASP.NET Core minimal API for quick URL resolution
- 🛠️ **Windows Service**: Easy installation with auto-start capability
- 🌐 **Custom Domain**: Automatic hosts file management for clean URLs

### Quick Start
```bash
# Run locally (requires Administrator for port 80)
cd src/kurz
dotnet run

# Install as Windows service (PowerShell as Administrator)
cd src/kurz
.\Install-Service.ps1
```

### Service Management
```powershell
.\Install-Service.ps1 status      # Check service status  
.\Install-Service.ps1 uninstall   # Remove the service
```

**Examples:**
- `http://g/pr/OwAc` → redirects to PR #12041652 (Base62 decoded)
- `http://g/pr/12041652` → redirects to PR #12041652 (decimal)

The service runs on `http://localhost:80` by default and automatically manages the hosts file entry for the `g` domain.

## 🧪 Testing

Comprehensive integration tests are available in `tests/gapir.Tests/`:

```bash
# Run all tests
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal
```

**Test Coverage:**
- Help command functionality (`--help`, `-h`)
- Flag combinations (`--verbose`, `--approved`, `--full-urls`)
- Edge cases and error handling
- JSON output validation

## 🤝 Contributing

1`. Fork the repository
1. Create a feature branch
1. Make your changes  
1. Add tests if applicable
1. Submit a pull request

## 📄 License

This project is licensed under the MIT License.
