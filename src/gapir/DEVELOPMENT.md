# Development History & Architecture Decisions

This document captures the key architectural decisions and development history of the `gapir` (Graph API Review) tool to help future developers and GitHub Copilot understand the context and reasoning behind design choices.

## Project Overview

`gapir` is a CLI tool for checking Azure DevOps pull requests assigned to you for review. It provides a clean, tabular view of pending PRs with intelligent status analysis.

## Key Architectural Decisions

### 1. Command Line Interface Design

**Decision**: Use `System.CommandLine` instead of basic argument parsing
- **Rationale**: Better maintainability, automatic help generation, type safety, consistent CLI patterns
- **Migration**: Moved from `args.Contains()` checks to proper options with descriptions and aliases
- **Benefits**: Professional help output, validation, extensibility

### 2. Configuration Pattern

**Decision**: Implement Options Pattern with `PullRequestCheckerOptions` class
- **Before**: Multiple boolean parameters in constructor: `PullRequestChecker(bool showApproved, bool useShortUrls, bool showDetailedTiming, bool showDetailedInfo)`
- **After**: Single options object: `PullRequestChecker(PullRequestCheckerOptions options)`
- **Rationale**: Better maintainability, easier to extend, clearer parameter organization
- **Benefits**: Type safety, IntelliSense support, easier testing

### 3. User Experience Improvements

**Decision**: Add professional progress indication with spinners
- **Problem**: Users were left wondering if the tool was working during API calls (1-2 seconds)
- **Solution**: Unicode Braille spinner patterns with clear status messages
- **Implementation**: Custom `Spinner` class with success/error states
- **CLI Best Practice**: Always show progress for operations >1 second

**Decision**: Default behavior optimized for speed
- **Before**: Detailed information shown by default
- **After**: Summary table by default, detailed info on request (`-d` flag)
- **Rationale**: Most users want quick overview, detailed info available when needed

### 4. Code Organization

**Decision**: Separate utility classes for focused responsibilities
- **Spinner.cs**: Progress indication with professional animations
- **PullRequestCheckerOptions.cs**: Configuration object following Options Pattern
- **Base62.cs**: URL shortening utility
- **Log.cs**: Logging abstraction

## Command Line Interface

### Available Options
```
-a, --show-approved       Show table of already approved PRs
-v, --verbose             Show diagnostic messages during execution  
-f, --full-urls           Use full Azure DevOps URLs instead of short g URLs
-t, --detailed-timing     Show detailed age column - slower due to API calls
-d, --show-detailed-info  Show detailed information section for each pending PR
```

### Design Principles
1. **Sensible defaults**: Most common use case works without flags
2. **Progressive disclosure**: Basic info by default, detailed on request
3. **Consistent aliases**: Short (-x) and long (--word) forms for all options
4. **Self-documenting**: Help text explains impact and trade-offs

## Performance Optimizations

### Build Performance
- **Package Lock File**: Added `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>`
- **Benefits**: Faster restores, deterministic builds, better CI/CD performance

### Runtime Performance  
- **Caching**: API reviewers group cached to avoid repeated Identity API calls
- **Selective timing**: Detailed timing only when requested (slower due to additional API calls)
- **Parallel processing**: Ready for future async improvements

## Authentication Strategy

**Current**: Interactive authentication with token caching via `Microsoft.Identity.Client`
- **Broker support**: Uses system authentication when available
- **Token persistence**: Reduces authentication frequency
- **Fallback**: Graceful degradation if broker unavailable

## Data Processing Logic

### PR Status Analysis
1. **Vote Classification**: Maps Azure DevOps vote values to human-readable status
2. **API Reviewer Detection**: Multiple fallback strategies for identifying API reviewers
3. **Approval Logic**: Sophisticated reasoning about why PRs are not completed

### Status Codes
- **REJ**: Rejected by any reviewer
- **WFA**: Waiting for author (changes requested)
- **PRA**: Pending reviewer approval (API reviewers needed)
- **POA**: Pending other approvals (non-API reviewers)
- **POL**: Policy/build issues (approvals satisfied but PR blocked)

## URL Strategy

**Short URLs**: `http://g/pr/{base62id}` using Base62 encoding
- **Rationale**: Microsoft internal shortener, cleaner terminal output
- **Fallback**: Full Azure DevOps URLs available with `--full-urls`

## Future Considerations

### Extensibility Points
1. **Configuration**: Options pattern ready for config files, environment variables
2. **Output Formats**: Table printing abstracted for future JSON/CSV output
3. **Authentication**: Modular design supports multiple auth methods
4. **API Clients**: Abstracted for potential GitHub Enterprise, other systems

### Potential Enhancements
- Configuration file support (JSON/YAML)
- Multiple organization support
- Custom status reason logic
- Export capabilities (JSON, CSV, Markdown)
- Watch mode for continuous monitoring
- Integration with other tools via JSON output

## Development Practices

### Code Quality
- **Nullable reference types**: Enabled for better null safety
- **Modern C#**: Uses latest language features (pattern matching, collection expressions)
- **Error handling**: Graceful degradation with fallback strategies
- **Logging**: Structured logging with different verbosity levels

### Testing Strategy
- Ready for unit testing with dependency injection
- Options pattern facilitates testing different configurations
- Spinner class isolated for UI testing

## Dependencies

### Core Dependencies
- **Microsoft.TeamFoundationServer.Client**: Azure DevOps API access
- **Microsoft.Identity.Client**: Authentication
- **System.CommandLine**: Modern CLI framework

### Design Philosophy
- Minimal dependencies for faster startup
- Microsoft ecosystem alignment
- Battle-tested libraries for reliability
