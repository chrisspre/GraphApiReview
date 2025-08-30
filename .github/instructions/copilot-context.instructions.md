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
- **Key Feature**: Advanced reviewer filtering based on `IsRequired` flag from Azure DevOps API

### Supporting Service: `kurz` URL Shortener
- **Purpose**: Creates compact URLs (Base62 encoding) for cleaner CLI output
- **Integration**: Loose coupling - CLI falls back to full URLs if service unavailable

### Project Structure
```
src/
├── gapir/          # Main CLI tool
│   ├── Program.cs           # CLI entry point and diagnose subcommand
│   ├── PullRequestChecker.cs # Main orchestration logic
│   ├── Services/
│   │   ├── PullRequestAnalysisService.cs # Core business logic with IsRequired filtering
│   │   └── AzureDevOpsService.cs        # API integration layer
│   └── Models/
│       └── PullRequestInfo.cs           # Data transfer objects
├── kurz/           # URL shortener service
tests/
├── gapir.Tests/    # Comprehensive test suite (22 tests)
│   ├── Services/
│   │   └── PullRequestAnalysisServiceTests.cs # Unit tests for core filtering logic
│   └── Integration/
│       └── DiagnoseIntegrationTests.cs         # Integration tests for CLI commands
```

## Key Implementation Details

### Authentication Flow
1. **Primary**: Brokered authentication (Windows Hello/PIN) - best UX
2. **Fallback**: Device code flow - universal compatibility
3. **Caching**: Secure token storage using DPAPI on Windows

### Core Business Logic (`PullRequestChecker.cs`)
- Connects to specific Azure DevOps org/project/repo
- Fetches PRs assigned to configured API reviewers group
- **Critical Feature**: Filters reviewers based on `IsRequired=true` flag to exclude removed/inactive reviewers
- Analyzes review status and metadata using `PullRequestAnalysisService`
- Categorizes PRs into actionable (pending), approved, and not-required groups
- Formats output with age indicators and status codes

### Reviewer Filtering Logic (Key Implementation Detail)
**Problem Solved**: Azure DevOps API returns reviewers with `IsRequired=false` for audit purposes when reviewers are removed from PRs. This caused incorrect status displays (e.g., showing "NoVote 0/2" when user was removed).

**Solution**: Three-tier filtering in `PullRequestAnalysisService.cs`:
1. **`GetApiApprovalRatio()`**: Only counts `IsRequired=true` API reviewers in approval ratios
2. **`GetMyVoteStatus()`**: Returns "---" when current user has `IsRequired=false` 
3. **`IsApprovedByCurrentUser()`**: Only considers approvals valid when `IsRequired=true`

**Result**: Clean main table showing only actionable PRs, with removed reviewers properly categorized in separate "approved/not-required" section.

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

## Recent Major Improvements (v1.0.5)

### Issue Resolution: Incorrect Reviewer Status Display
**Problem**: PRs showed misleading status like "NoVote 0/2" when user was removed as reviewer
**Root Cause**: Azure DevOps API returns removed reviewers with `IsRequired=false` for audit trail
**Solution**: Implemented comprehensive `IsRequired` flag filtering across all analysis methods

### New Diagnostic Features
- `diagnose <id>` subcommand for investigating specific PR reviewer details
- Raw Azure DevOps API data display including all reviewer flags
- Current user status breakdown (vote, IsRequired, IsContainer, IsFlagged)
- Essential for troubleshooting reviewer assignment issues

### Enhanced Table Organization  
- Clean separation of actionable vs completed/not-required PRs
- Main table shows only PRs requiring user attention (`IsRequired=true`)
- Secondary table shows approved PRs and assignments user is no longer required for
- Improved table headers and explanatory text

### Comprehensive Test Coverage
- 16 unit tests for `PullRequestAnalysisService` filtering logic
- 6 integration tests for CLI command functionality  
- Reflection-based testing of private methods for thorough coverage
- Protection against regressions in critical filtering logic

## Key Commands & Usage

```bash
# Basic usage - shows actionable PRs requiring your review
gapir

# Common options
gapir --approved    # Include already approved PRs and PRs you're not required to review
gapir --full-urls       # Use full URLs instead of short links
gapir --detailed-timing # Show detailed age info (slower)
gapir --verbose         # Troubleshooting output

# Diagnostic features
gapir diagnose 13300322  # Investigate specific PR's reviewer details from Azure DevOps API
                              # Shows raw reviewer data including IsRequired, IsContainer, IsFlagged flags
                              # Useful for troubleshooting reviewer status issues

gapir collect     # Generate ApiReviewersFallback.cs from recent PR data
gapir --version              # Show tool version
```

### Understanding the Output

**Main Table**: Shows only PRs requiring your attention (IsRequired=true)
- Status codes indicate your current vote status
- API column shows approval ratio (approved/total required API reviewers)
- Age column helps prioritize by urgency

**Approved/Not-Required Table**: Shows PRs you've approved or are no longer required to review
- "Why" column explains why PR isn't completed (Policy, Wait4A, etc.)
- Helps track previously reviewed work

**Diagnostic Output** (diagnose subcommand):
- Raw Azure DevOps API response for specific PR
- Shows all reviewers with their flags (IsRequired, IsContainer, IsFlagged)
- Displays your reviewer status and vote details
- Essential for troubleshooting reviewer assignment issues

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

### Comprehensive Test Coverage (22 tests total)
- **Unit Tests** (`PullRequestAnalysisServiceTests.cs`): 16 tests covering core filtering logic
  - Tests `IsRequired=false` filtering for all analysis methods
  - Edge cases: null reviewers, mixed API/non-API reviewers, different vote values
  - Uses reflection to test private methods for thorough coverage
- **Integration Tests** (`DiagnoseIntegrationTests.cs`): 6 tests covering CLI functionality
  - Command-line argument parsing and validation
  - Error handling for invalid inputs
  - `diagnose` subcommand output format verification
- **Manual Testing**: Authentication flows, real Azure DevOps API integration
- **Regression Protection**: Tests ensure IsRequired filtering continues working correctly

### Test Execution
```bash
# Run all tests
dotnet test tests/gapir.Tests

# Run specific test categories
dotnet test --filter "PullRequestAnalysisServiceTests"  # Unit tests
dotnet test --filter "DiagnoseIntegrationTests"       # Integration tests
```

## Key Success Factors

1. **Reliable Authentication**: Brokered auth works seamlessly on Windows
2. **Fast Performance**: Quick startup and response times
3. **Clear Output**: Easy to scan and understand at a glance
4. **Simple Deployment**: Single global tool install/update
5. **Robust Fallbacks**: Works even when external services are down

This represents the culmination of iterating from PowerShell wrapper approaches to a clean, focused CLI tool that solves the specific problem of Azure DevOps pull request review management for the Microsoft Graph API team.

---

## Comprehensive Development Prompt

When working on this project, use this complete context to understand and extend the functionality:

**"I need help with the GraphApiReview project - a C# .NET 9.0 CLI tool called `gapir` that helps Microsoft Graph API reviewers manage Azure DevOps pull requests. 

The tool connects to the `msazure.visualstudio.com/One` organization's `AD-AggregatorService-Workloads` repository and displays PRs assigned to the 'Microsoft Graph API reviewers' group. 

**Core Issue Solved**: Azure DevOps API returns reviewers with `IsRequired=false` when they're removed from PRs (for audit purposes), causing incorrect status displays. The tool implements three-tier filtering in `PullRequestAnalysisService.cs` to only show actionable PRs where the user has `IsRequired=true`.

**Key Features**:
- Main table shows only actionable PRs requiring attention
- Separate approved/not-required table for completed/removed assignments  
- `diagnose <id>` subcommand for investigating reviewer status issues
- Brokered authentication (Windows Hello/PIN) with device code fallback
- URL shortening integration with `kurz` service
- 22 comprehensive tests covering filtering logic and CLI functionality

**Architecture**: Single executable using System.CommandLine, with `PullRequestChecker.cs` orchestrating the workflow, `PullRequestAnalysisService.cs` handling core business logic with IsRequired filtering, and `AzureDevOpsService.cs` managing API integration.

The tool uses 6-character status mnemonics (Apprvd, Sugges, NoVote, Wait4A, Reject) and displays approval ratios, age indicators, and change summaries in a clean console table format."

This prompt provides complete context for understanding the project's purpose, technical implementation, key solved problems, and current functionality.**
