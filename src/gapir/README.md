# gapir (Graph API Review)

A CLI tool to check Azure DevOps pull requests assigned to you for review.

## Features

- 🔍 **Pull Request Checker**: View PRs assigned to you for review
- 🔐 **Modern Authentication**: Brokered authentication (Windows Hello/PIN) with device code fallback
- 🌐 **Cross-platform**: Works on Windows, macOS, and Linux
- 💾 **Token Caching**: Remembers your authentication for faster subsequent runs
- 🧹 **Clean Titles**: Automatically cleans and shortens PR titles for better readability

## Installation

Install as a global .NET tool:

```bash
dotnet tool install --global AzureDevOpsPRChecker
```

## Usage

Run the tool from anywhere to check your Azure DevOps pull requests:

```bash
gapir
```

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
- Organization: `https://msazure.visualstudio.com/`
- Project: `One`
- Repository: `AD-AggregatorService-Workloads`

To modify these settings, you'll need to update the source code and rebuild.

## Output

The tool shows two lists:

### ✅ Approved PRs (Short Format)
```
Author - Short Title - URL
```

### ⏳ Pending PRs (Detailed Format)
```
ID: 12345
Title: Clean Title
Author: John Doe
Status: Active
Created: 2025-01-01 12:00:00
Source: feature/branch -> main
URL: https://...
Reviewers:
  - Jane Smith: No vote
  - Bob Johnson: Approved
```

## Requirements

- .NET 9.0 or later
- Access to Azure DevOps organization
- Windows (for brokered authentication and clipboard/browser features)
- Windows 10 version 1903 or later (for optimal brokered authentication experience)

## Token Storage

Authentication tokens are securely cached in:
`%LocalAppData%\AzureDevOpsPRChecker\msalcache.bin`

## Uninstall

```bash
dotnet tool uninstall --global AzureDevOpsPRChecker
```
