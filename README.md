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
- **Separation of Concerns**: Clean architecture with separated data fetching and rendering

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

A web service for creating short URLs used by the gapir tool for cleaner output.

**Features:**
- 🔗 **Base62 Encoding**: Creates compact PR URLs like `g/abc123`
- ⚡ **Fast Redirects**: Lightweight service for quick URL resolution
- 🛠️ **Simple Setup**: Single executable with minimal configuration

### Setup
```bash
cd src/kurz
dotnet run
```

The service runs on `http://localhost:5067` by default.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes  
4. Add tests if applicable
5. Submit a pull request

## 📄 License

This project is licensed under the MIT License.
