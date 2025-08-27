# GraphApiReview - Copilot Context & Instructions

## Project Purpose

A CLI tool for Azure DevOps pull request review management, specifically for Microsoft Graph API reviewers. The tool checks for pull requests assigned to you for review and displays them in a clean, actionable format.

## Core Architecture

### Primary Tool: `gapir` CLI
- **Language**: C# (.NET 9.0)
- **Pattern**: Single executable CLI tool using System.CommandLine
- **Authentication**: Brokered auth (Windows Hello/PIN) with device code fallback
- **Output**: Console table with color-coded status indicators
- **Deployment**: .NET global tool

### Supporting Service: `kurz` URL Shortener
- **Purpose**: Creates compact URLs (Base62 encoding) for cleaner CLI output
- **Integration**: Loose coupling - CLI falls back to full URLs if service unavailable

### Project Structure
```
src/
├── gapir/          # Main CLI tool
├── kurz/           # URL shortener service
tests/
├── gapir.Tests/    # Unit tests
```

## Key Implementation Details

### Authentication Flow
1. **Primary**: Brokered authentication (Windows Hello/PIN) - best UX
2. **Fallback**: Device code flow - universal compatibility
3. **Caching**: Secure token storage using DPAPI on Windows

### Core Business Logic (`PullRequestChecker.cs`)
- Connects to specific Azure DevOps org/project/repo
- Fetches PRs assigned to configured API reviewers group
- Analyzes review status and metadata
- Formats output with age indicators and status codes

### Status Codes (6-char mnemonics)
- `Apprvd` = Approved
- `Sugges` = Approved with suggestions  
- `NoVote` = No vote (needs attention)
- `Wait4A` = Waiting for author
- `Reject` = Rejected

### Configuration (in `PullRequestChecker.cs`)
- **Org**: `https://msazure.visualstudio.com/`
- **Project**: `One`
- **Repo**: `AD-AggregatorService-Workloads`
- **Group**: `[TEAM FOUNDATION]\\Microsoft Graph API reviewers`

## Abandoned Approaches & Lessons Learned

### PowerShell Cmdlet (Abandoned)
**Problem**: Attempted to create `Get-ApiReviews` PowerShell cmdlet
**Issues Discovered**:
- MSAL native authentication incompatible with PowerShell execution context
- Forced to use device code flow (worse UX than CLI brokered auth)
- Can't use async/await properly (requires `.GetAwaiter().GetResult()`)
- PowerShell module loading complexity
- Only benefit was avoiding Format-Table implementation

**Decision**: CLI tool with good formatting is better than PowerShell wrapper
**Key Insight**: PowerShell execution context has fundamental limitations for modern authentication

## Development Patterns

### CLI Design
- Use `System.CommandLine` for argument parsing
- Rich console output with colors and spinners
- Graceful error handling with clear messages
- Self-contained executable (no external dependencies)

### Service Integration
- Loose coupling with external services (kurz)
- Fallback strategies when services unavailable
- Clear separation of concerns

### Error Handling
- Retry logic for transient network issues
- Graceful authentication failure handling
- Clear error messages for common scenarios

## Key Commands & Usage

```bash
# Basic usage
gapir

# Common options
gapir --show-approved    # Include already approved PRs
gapir --full-urls       # Use full URLs instead of short links
gapir --detailed-timing # Show detailed age info (slower)
gapir --verbose         # Troubleshooting output
```

## Deployment & Updates

### Global Tool Installation
```bash
dotnet tool install --global --add-source ./nupkg gapir
dotnet tool update --global --add-source ./nupkg gapir
```

### Development Workflow
1. Make changes to `src/gapir/`
2. Update version in `gapir.csproj`  
3. Run `./src/gapir/install-tool.ps1` (builds and installs locally)
4. Test with `gapir` command
5. Commit and push

## Output Design Philosophy

### User Experience Goals
- **Actionable**: Show what needs attention first
- **Compact**: Fit in standard terminal width
- **Informative**: Status, age, author, change summary
- **Scannable**: Color coding and consistent formatting

### Technical Implementation
- Console table with aligned columns
- Color coding based on review status
- Age indicators for urgency (`3d`, `1w`, etc.)
- Smart title truncation and cleaning
- URL shortening for cleaner display

## Future Considerations

### If Adding New Features
- Maintain single executable simplicity
- Preserve fast startup time
- Keep authentication flow reliable
- Don't break existing command-line interface

### If Extending Authentication
- Always prioritize brokered auth when available
- Maintain device code fallback for compatibility
- Keep token caching secure and reliable

### If Adding Output Formats
- JSON output for scripting scenarios
- Maintain default human-readable format
- Consider piping and redirection scenarios

## Testing Strategy

- Unit tests for business logic in `tests/gapir.Tests/`
- Manual testing for authentication flows
- Cross-platform compatibility testing
- Integration testing with real Azure DevOps APIs

## Key Success Factors

1. **Reliable Authentication**: Brokered auth works seamlessly on Windows
2. **Fast Performance**: Quick startup and response times
3. **Clear Output**: Easy to scan and understand at a glance
4. **Simple Deployment**: Single global tool install/update
5. **Robust Fallbacks**: Works even when external services are down

This represents the culmination of iterating from PowerShell wrapper approaches to a clean, focused CLI tool that solves the specific problem of Azure DevOps pull request review management for the Microsoft Graph API team.
