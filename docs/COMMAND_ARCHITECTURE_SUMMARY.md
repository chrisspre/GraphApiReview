# GAPIR Command-Handler-Service Architecture

## Quick Reference Chart

| CLI Command | Handler | Business Service | Data Service | Rendering |
|-------------|---------|------------------|--------------|-----------|
| `gapir` (default) | ReviewCommandHandler | PendingPullRequestService | PullRequestDataLoader | **PullRequestRenderingService** |
| `gapir approved` | ApprovedCommandHandler | ApprovedPullRequestService | PullRequestDataLoader | **PullRequestRenderingService** |
| `gapir completed` | CompletedCommandHandler | CompletedPullRequestService | CompletedPullRequestDataLoader | **PullRequestRenderingService** |
| `gapir diagnose <id>` | DiagnoseCommandHandler | PullRequestDiagnostics | PullRequestDiagnosticService | *(Direct console output)* |
| `gapir collect` | CollectCommandHandler | ReviewerCollector | *(Direct ADO API)* | *(Direct console output)* |
| `gapir preferences` | PreferencesCommandHandler | PreferencesService | GraphAuthenticationService | *(Direct console output)* |

## Key Observations

### **Shared Services**
- **PullRequestRenderingService**: Used by 3 main commands (review/approved/completed) for consistent table formatting
- **ConnectionService**: Azure DevOps authentication - used by all commands except preferences
- **ConsoleLogger**: Logging - used by all command handlers

### **Service Patterns**
- **review/approved/completed**: Follow consistent pattern with dedicated business service + shared rendering
- **diagnose/collect/preferences**: Simple pattern with direct console output

### **Data Access Patterns**
- **Standard PRs**: PullRequestDataLoader → PullRequestAnalysisService
- **Completed PRs**: CompletedPullRequestDataLoader (specialized for date filtering)
- **Diagnostics**: PullRequestDiagnosticService (raw ADO data)

## Architecture Benefits

✅ **Consistent UX**: review/approved/completed commands use same rendering service
✅ **Clean Separation**: Business logic in gapir.core, UI in gapir  
✅ **Single Responsibility**: Each command handler has focused dependencies
✅ **Testability**: Services are injected via DI container

## Previous Confusion Resolved

❌ **PullRequestDiagnosticRenderingService**: Unused service has been removed
✅ **Clear Naming**: Services ending in "Service" are business logic, "RenderingService" is UI
✅ **Consistent Location**: All rendering services in gapir/Services/, business services in gapir.core/Services/