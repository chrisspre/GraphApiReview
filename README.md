# ApiReview Tools

A collection of CLI tools for Azure DevOps workflow optimization.

## gapir (Graph API Review)

A CLI tool to check Azure DevOps pull requests assigned to you for review.

**Features:**
- **Pull Request Checker**: View PRs assigned to you for review
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

# Use full URLs instead of short links
gapir --full-urls

# Show detailed timing (slower)
gapir --detailed-timing

# Verbose output for troubleshooting
gapir --verbose
```

## 🔗 OSC 8 Terminal Hyperlinks

The gapir tool uses modern terminal hyperlink support (OSC 8 escape sequences) to make pull request titles clickable directly in your terminal, eliminating the need for separate URL columns or external URL shortening services.

**Features:**
- �️ **Clickable Titles**: PR titles become clickable links that open directly in your browser
- 🔧 **Terminal Detection**: Automatically detects terminal support for hyperlinks
- � **Zero Configuration**: No additional services or setup required
- 🔗 **Direct URLs**: Links point directly to Azure DevOps pull request URLs

**Supported Terminals:**
- Windows Terminal
- Visual Studio Code integrated terminal  
- Modern terminals that support OSC 8 escape sequences

**How it Works:**
- PR titles in terminal output are automatically rendered as clickable hyperlinks
- If your terminal supports OSC 8, you can click/Ctrl+click titles to open PRs
- Fallback to regular text display for terminals without hyperlink support
- No URL columns needed - cleaner, more focused output

**Example Output:**
```text
Title                                    | Status | Author          | Age     | Ratio
[Fix GraphQL schema validation bug]     | ⭐     | John Doe        | 2 days  | 3/4
[Add new API endpoint for webhooks]     | ⏳     | Jane Smith      | 1 day   | 1/3
```

> **Note:** Titles shown above would be clickable links in supported terminals

The hyperlink implementation automatically generates proper Azure DevOps URLs and handles terminal capability detection without requiring any external dependencies.

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

## 🛠️ Development

### Feature Flags

The tool includes optional features that can be enabled at compile time:

**Statistics Display** (disabled by default):
```bash
# Enable statistics in output (shows Total/Pending/Approved counts)
dotnet build -p:DefineConstants=ENABLE_STATISTICS

# Build normally (statistics hidden)
dotnet build
```

## 🤝 Contributing

1`. Fork the repository
1. Create a feature branch
1. Make your changes  
1. Add tests if applicable
1. Submit a pull request

## 📄 License

This project is licensed under the MIT License.
