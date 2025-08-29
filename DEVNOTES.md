# Developer Notes

## Architecture Overview

### Core Projects

- **`src/gapir/`** - Main CLI tool (Graph API Review)
- **`src/kurz/`** - URL shortener service  
- **`tests/gapir.Tests/`** - Unit tests

### Key Components

#### gapir CLI Tool
- **`Program.cs`** - Command-line interface using System.CommandLine
- **`PullRequestChecker.cs`** - Core business logic
- **`Services/`** - Azure DevOps integration services
- **`Models/`** - Data transfer objects

#### Authentication
- **`ConsoleAuth.cs`** - Handles authentication flow
- **`TokenCacheHelper.cs`** - Secure token persistence
- Uses brokered authentication (Windows Hello/PIN) with device code fallback

#### Data Services
- **`AzureDevOpsService.cs`** - Azure DevOps API integration
- **`PullRequestAnalysisService.cs`** - PR data processing
- **`PullRequestDisplayService.cs`** - Output formatting

## Development Workflow

### Build and Test
```bash
# Build everything
dotnet build

# Run tests
dotnet test

# Run CLI tool from source
dotnet run --project src/gapir

# Package as global tool
./src/gapir/install-tool.ps1
```

### Publishing Global Tool
```bash
# Build and pack
dotnet pack src/gapir/gapir.csproj -c Release -o nupkg

# Install locally  
dotnet tool install --global --add-source ./nupkg gapir

# Update existing installation
dotnet tool update --global --add-source ./nupkg gapir
```

### Key Development Patterns

#### Authentication Flow
1. Try brokered authentication (Windows Hello/PIN)
2. Fall back to device code flow
3. Cache tokens securely using DPAPI

#### PR Analysis Pipeline
1. Connect to Azure DevOps
2. Fetch pull requests for configured repository
3. Analyze review status and metadata
4. Format for console output with color coding
5. Generate short URLs via kurz service

#### Error Handling
- Graceful degradation for authentication failures
- Retry logic for transient network issues
- Clear error messages for common scenarios

## Configuration

### Azure DevOps Settings
Located in `PullRequestChecker.cs`:
- **OrganizationUrl**: `https://msazure.visualstudio.com/`
- **ProjectName**: `One`
- **RepositoryName**: `AD-AggregatorService-Workloads`
- **ApiReviewersGroup**: `[TEAM FOUNDATION]\\Microsoft Graph API reviewers`

### URL Shortener Integration
- Default kurz service: `http://localhost:5067`
- Base62 encoding for compact URLs
- Fallback to full URLs if service unavailable

## Abandoned Approaches

### PowerShell Cmdlet (Abandoned)
**Why abandoned:**
- MSAL native authentication incompatible with PowerShell context
- Forced device code flow (worse UX than CLI)
- Can't use async/await properly (requires `.GetAwaiter().GetResult()`)
- Only benefit was avoiding Format-Table implementation
- CLI tool approach is cleaner and more reliable

**Lessons learned:**
- PowerShell execution context has fundamental limitations for modern auth
- Direct CLI tool with good formatting is better than PowerShell wrapper
- Native authentication flows don't work well in hosted environments

## Architecture Decisions

### Single Executable Approach
- Self-contained CLI tool
- No external dependencies beyond .NET runtime
- Simple deployment and updates

### Authentication Strategy
- Prioritize user experience (brokered auth first)
- Secure token caching
- Clear fallback paths

### Output Design
- Color-coded status indicators
- Compact table format
- Smart URL shortening
- Age indicators for urgency

### Service Integration
- Loose coupling with kurz service
- Graceful degradation when services unavailable
- Clear separation of concerns

## 🔄 Update Process

### For Users
```bash
# Update global tool
dotnet tool update --global gapir --add-source ./nupkg
```

### For Developers
1. Make changes
2. Update version in `gapir.csproj`
3. Run `./src/gapir/install-tool.ps1`
4. Test the updated tool
5. Commit and push changes

## Testing Strategy

- Unit tests for core business logic
- Integration tests for Azure DevOps API
- Manual testing for authentication flows
- Cross-platform testing (Windows/macOS/Linux)

## 📝 Code Style

- Modern C# with nullable reference types
- Async/await throughout
- Clear separation of concerns
- Comprehensive error handling
- Descriptive variable and method names
