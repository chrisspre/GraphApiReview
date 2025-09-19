# Architecture Documentation

This document provides comprehensive technical details about the architecture, design decisions, and development patterns for the ApiReview Tools project.

## System Overview

### Project Structure

- **`src/gapir/`** - Main CLI tool (Graph API Review)
- **`src/mcp/`** - Model Context Protocol server
- **`tests/gapir.Tests/`** - Unit tests

### Core Architecture Principles

1. **Separation of Concerns**: Clean service layer architecture
2. **Performance First**: Conditional data fetching to avoid unnecessary API calls
3. **User Experience**: Progress indication, clean output, modern authentication
4. **Testability**: Dependency injection, options pattern, isolated services
5. **Maintainability**: Modern CLI framework, comprehensive error handling

## gapir CLI Tool Architecture

### Service Layer Design

The application uses a clean service layer architecture with dependency injection:

```csharp
// Service Registration (Program.cs)
services.AddScoped<ConsoleLogger>();
services.AddScoped<ConnectionService>();
services.AddScoped<PullRequestRenderingService>();
services.AddScoped<PullRequestDataService>();
services.AddScoped<PullRequestAnalysisService>();
```

#### Core Services

- **PullRequestChecker**: Thin orchestrator handling authentication and coordination
- **PullRequestDataService**: Isolated expensive data fetching logic with performance optimization
- **PullRequestRenderingService**: Clean output formatting (text/JSON) and table display
- **PullRequestAnalysisService**: PR analysis and status determination logic
- **ConnectionService**: Azure DevOps API integration
- **ConsoleLogger**: Simplified logging with settable verbosity

### Performance Architecture

**Conditional Data Fetching**: Expensive operations are only performed when requested:

```csharp
// Before: Always fetched approved PRs regardless of usage
result.ApprovedPRs = approvedPRs.Concat(notRequiredPRs).ToList();

// After: Conditional fetching based on command line options
if (options.ShowApproved)
{
    result.ApprovedPRs = approvedPRs.Concat(notRequiredPRs).ToList();
}
// ApprovedPRs remains null when not requested
```

**Benefits Achieved**:
- 50%+ speed improvement for common usage patterns
- Cleaner JSON output without diagnostic pollution
- Better resource utilization

### JSON Output Architecture

Clean data model designed for automation integration:

```csharp
public class GapirResult
{
    public string? Title { get; set; }
    public List<PullRequestInfo> PendingPRs { get; set; } = new();
    public List<PullRequestInfo>? ApprovedPRs { get; set; } // Nullable for performance
    public string? ErrorMessage { get; set; }
}
```

**Design Principles**:
- No diagnostic properties polluting automation output
- Nullable collections for performance optimization
- Clean separation between UI feedback and data output

## Authentication Architecture

### Modern Authentication Strategy

- **Primary**: Microsoft.Identity.Client with broker support (Windows Hello/PIN)
- **Fallback**: Device code flow for environments without broker support
- **Caching**: Secure token persistence using DPAPI
- **UX Priority**: User experience over pure automation

### Implementation Pattern

```csharp
// Authentication Flow in ConsoleAuth.cs
1. Try brokered authentication (Windows Hello/PIN)
2. Fall back to device code flow
3. Cache tokens securely using DPAPI
4. Silent refresh when possible
```

### Design Decisions

- **Interactive Authentication Acceptable**: For developer tools, UX takes priority
- **Token Caching**: Reduces authentication friction for repeated usage
- **Graceful Fallback**: Multiple authentication strategies for different environments

## Command Line Interface Design

### System.CommandLine Integration

**Migration from Manual Parsing**:

```csharp
// Before: Manual argument parsing
bool showApproved = args.Contains("--approved") || args.Contains("-a");
bool verbose = args.Contains("--verbose") || args.Contains("-v");

// After: Professional CLI framework
var showApprovedOption = new Option<bool>(
    aliases: ["--approved", "-a"],
    description: "Show table of already approved PRs");
```

**Benefits**:
- Automatic help generation
- Type safety for options
- Consistent error handling
- Professional CLI conventions

### Options Pattern Implementation

**Problem Solved**: Constructor parameter explosion

```csharp
// Before: Hard to maintain
public PullRequestChecker(bool showApproved, bool useShortUrls, bool showDetailedTiming, bool showDetailedInfo)

// After: Clean and extensible
public class PullRequestCheckerOptions { /* properties */ }
public PullRequestChecker(PullRequestCheckerOptions options)
```

**Design Decision**: Default values in options class for clarity and testability.

## API Integration Patterns

### Azure DevOps Integration Challenges

**Enterprise API Reliability**: Multiple fallback strategies required

```csharp
// Group membership queries - Multiple fallback strategies
1. Direct group search with expanded membership
2. Alternative search patterns
3. Heuristic analysis of PR reviewer history
4. Conservative fallback to empty set
```

**Key Learning**: Enterprise APIs require defensive programming with multiple fallback strategies.

### Vote Status Mapping

Clear mapping from Azure DevOps vote values to human-readable status:

- `10` → "Approved" (APP)
- `5` → "Approved with suggestions" (APS)  
- `0` → "No vote" (NOV)
- `-5` → "Waiting for author" (WFA)
- `-10` → "Rejected" (REJ)

### PR Status Analysis Priority

Priority-based logic for determining completion blockers:

1. **REJ** (Rejected) - Highest priority, any rejection blocks
2. **WFA** (Waiting For Author) - Author must address feedback
3. **PRA** (Pending Reviewer Approval) - Need more API reviewer approvals
4. **POA** (Pending Other Approvals) - Non-API reviewers pending
5. **POL** (Policy/Build Issues) - Approvals satisfied but other blocks

## Terminal Output Architecture

### OSC 8 Hyperlinks Implementation

**Modern Terminal Integration**: Clickable PR titles using OSC 8 escape sequences

- **Escape Sequence**: `\u001b]8;;{url}\u001b\\{text}\u001b]8;;\u001b\\`
- **Auto-Detection**: Terminal capability detection for graceful fallback
- **Direct URLs**: Full Azure DevOps pull request URLs without shortening
- **Zero Dependencies**: No external services or configuration required

**TerminalLinkService Architecture**:
```csharp
public static class TerminalLinkService
{
    public static string CreateLink(string url, string text)
    public static string CreatePullRequestLink(int pullRequestId, string title)
    public static bool SupportsOsc8()
}
```

**Integration Pattern**:
- Clean separation between PR data and terminal rendering
- Automatic fallback to plain text for unsupported terminals
- Consistent URL generation using Azure DevOps configuration

## Error Handling Philosophy

### Graceful Degradation Principles

- **Prefer degradation over hard failures**
- **Multiple fallback strategies** for unreliable operations
- **User-friendly error messages** for common scenarios
- **Verbose logging available** for debugging

### Implementation Patterns

```csharp
// Example: Authentication fallback
try {
    // Try brokered authentication
    result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();
} catch (MsalUiRequiredException) {
    // Fall back to device code flow
    result = await app.AcquireTokenWithDeviceCode(scopes, callback).ExecuteAsync();
}
```

## User Experience Design

### Progress Indication Strategy

**Problem**: Tool appeared to "hang" during API calls (1-2 seconds)

**Solution**: Custom Spinner class with Unicode Braille patterns

```csharp
public class Spinner
{
    // Non-blocking animation using Timer
    // Cursor management for clean updates
    // Success/error states with symbols (✓/✗)
}
```

**UX Principle**: Show immediate feedback for operations >1 second.

### Default Behavior Optimization

**Design Decision**: Optimize for the most common use case

- **Default**: Quick summary table (most common usage)
- **Opt-in**: Detailed information with explicit flags
- **Performance**: Skip expensive operations unless requested

## Testing Architecture

### Testability Design

**Service Layer Benefits**:
- Individual service testing without full integration overhead
- Mock Azure DevOps APIs for reliable unit tests
- Options pattern facilitates configuration testing
- Spinner isolated from business logic

### Testing Strategy

- **Unit Tests**: Service-level testing with mocked dependencies
- **Integration Tests**: JSON output validation, command line to JSON output
- **Performance Tests**: Conditional data fetching validation
- **Authentication Tests**: Different scenarios and error handling paths

## Development History & Key Decisions

### Major Refactoring: Separation of Concerns (August 29, 2025)

**Motivation**: Improve testability, performance, and maintainability

**Key Changes**:
- Service layer separation for clean architecture
- Performance optimization through conditional fetching
- JSON output architecture without diagnostic pollution

### System.CommandLine Integration (August 14, 2025)

**Motivation**: Manual command-line parsing becoming unwieldy

**Impact**: Professional CLI framework with automatic help, type safety, and maintainability

### Flag Behavior Optimization

**Change**: Flipped `--hide-detailed-info` to `--show-detailed-info`
**Rationale**: Default to most common use case, detailed information on explicit request

## Configuration Management

### Azure DevOps Settings

Default configuration for Microsoft Graph API team:

```csharp
// Located in ConnectionService.cs
OrganizationUrl: "https://msazure.visualstudio.com/"
ProjectName: "One"
RepositoryName: "AD-AggregatorService-Workloads"
ApiReviewersGroup: "[TEAM FOUNDATION]\\Microsoft Graph API reviewers"
```

### Service Integration

- **Authentication**: Cached tokens with secure DPAPI storage
- **Terminal Output**: OSC 8 hyperlinks for supported terminals

## Future Architecture Considerations

### Extensibility Points

- **Options pattern** ready for configuration files
- **Output formatting** abstracted for future formats
- **Authentication** modularized for different providers
- **Async patterns** throughout for performance scaling

### Avoided Over-Engineering

- **Minimal dependencies** for faster startup
- **Core use case focus** rather than premature generalization
- **Simple file structure** appropriate for current tool scope

## Key Architectural Learnings

1. **CLI UX Matters**: Even 1-2 second delays need progress indication
2. **Enterprise APIs**: Always plan for inconsistent behavior and multiple fallback strategies
3. **Options Pattern**: Essential for maintainable configuration as tools grow
4. **Modern CLI Frameworks**: System.CommandLine significantly improves developer and user experience
5. **Default Behavior**: Optimize for the most common use case, make advanced features opt-in
6. **Performance Matters**: Expensive operations should be conditional, not default
7. **Clean Separation**: Diagnostic information belongs in logging, not result data
8. **Architecture Enables Testing**: Proper separation makes comprehensive testing feasible

This architecture documentation should serve as a comprehensive reference for understanding the technical decisions and patterns used throughout the codebase.
