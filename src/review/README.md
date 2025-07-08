# Azure DevOps Pull Request Checker

A CLI tool that helps you quickly check Azure DevOps pull requests assigned to you for review.

## Features

- 🔐 **Secure Authentication**: Uses Azure Device Code Flow with token caching
- 📋 **Smart Lists**: Shows approved PRs (short format) and pending PRs (detailed format)
- 🚀 **Great UX**: Auto-opens browser, copies device code to clipboard
- 💾 **Token Caching**: Remembers your authentication for faster subsequent runs
- 🧹 **Clean Titles**: Automatically cleans and shortens PR titles for better readability

## Installation

Install as a global .NET tool:

```bash
dotnet tool install --global AzureDevOpsPRChecker
```

## Usage

Run the tool from anywhere:

```bash
apipr
```

On first run, it will:
1. Open your browser automatically
2. Copy the device code to your clipboard
3. Prompt you to sign in to Azure DevOps
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
- Windows (for clipboard and browser auto-open features)

## Token Storage

Authentication tokens are securely cached in:
`%LocalAppData%\AzureDevOpsPRChecker\msalcache.bin`

## Uninstall

```bash
dotnet tool uninstall --global AzureDevOpsPRChecker
```
