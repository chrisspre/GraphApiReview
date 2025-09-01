# Developer Quick Notes

> **📖 For comprehensive architecture documentation, see [ARCHITECTURE.md](ARCHITECTURE.md)**

Quick reference for common development tasks and patterns.

## Quick Commands

### Build and Test
```bash
# Build everything
dotnet build

# Run tests
dotnet test

# Run gapir from source
dotnet run --project src/gapir

# Run kurz service (requires Admin for port 80)
dotnet run --project src/kurz
```

### Global Tool Management
```bash
# Package and install gapir globally
./src/gapir/install-tool.ps1

# Update existing installation  
dotnet tool update --global --add-source ./nupkg gapir
```

## Current Configuration

### Azure DevOps Settings
- **Organization**: `https://msazure.visualstudio.com/`
- **Project**: `One`
- **Repository**: `AD-AggregatorService-Workloads`
- **API Reviewers Group**: `[TEAM FOUNDATION]\\Microsoft Graph API reviewers`

### Service Endpoints
- **kurz service**: `http://localhost:80` (or `http://g` after service install)
- **Authentication**: MSAL with broker support + device code fallback

## Development Patterns

### Authentication Flow
1. Try brokered authentication (Windows Hello/PIN)
2. Fall back to device code flow  
3. Cache tokens securely using DPAPI

### Error Handling
- Graceful degradation preferred over hard failures
- Multiple fallback strategies for unreliable operations
- Clear error messages for common scenarios

## Key Learnings

- **PowerShell Cmdlet Approach Abandoned**: MSAL auth incompatible with PowerShell context
- **Performance Matters**: Expensive operations should be conditional (`--approved` flag)
- **CLI UX**: Show progress for operations >1 second
- **Enterprise APIs**: Always plan for multiple fallback strategies
