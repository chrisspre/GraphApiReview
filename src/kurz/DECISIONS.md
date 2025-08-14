# Kurz Code Context & Decisions Log

## Service Overview

`kurz` is a URL redirect service that powers the short URL functionality for `gapir`. It's a lightweight ASP.NET Core minimal API that runs as a Windows Service to provide reliable URL shortening.

## Architecture Decisions

### Minimal API Choice

**Decision**: ASP.NET Core Minimal APIs over full MVC controllers
**Date**: Initial implementation
**Rationale**: 
- Simple routing requirements (single redirect endpoint)
- Fast startup essential for service reliability
- Minimal resource footprint for dedicated service role
- Reduced complexity for maintenance

**Implementation**:
```csharp
app.MapGet("/{routeType}/{id}", async (string routeType, string id) => {
    // Redirect logic
});
```

### Windows Service Architecture

**Problem**: Need reliable URL shortening always available
**Solution**: Windows Service with automatic startup
**Benefits**:
- Survives system reboots
- Runs without user login
- System-level reliability and management
- Integrates with Windows Service infrastructure

**Trade-offs**: Windows-specific deployment (acceptable for Microsoft internal tool)

### Dual ID Format Support

**Decision**: Support both Base62 and decimal PR IDs in same endpoint
**Rationale**:
- Backward compatibility with existing decimal URLs
- Forward compatibility with compact Base62 encoding
- Automatic detection eliminates breaking changes

**Implementation Strategy**:
1. Try Base62 decoding first
2. Fall back to decimal parsing if Base62 fails
3. Both formats redirect to same target URL structure

### Route Mapping Design

**Decision**: Dictionary-based configuration for extensibility
```csharp
var routeMappings = new Dictionary<string, string>
{
    ["pr"] = "https://dev.azure.com/msazure/One/_git/AD-AggregatorService-Workloads/pullrequest/"
};
```

**Benefits**:
- Easy to add new route types (work items, commits, etc.)
- Configuration separate from routing logic
- No code changes required for new URL patterns
- Clear, readable mapping structure

### HTTP Status Code Decision

**Decision**: 308 Permanent Redirect instead of 302 Temporary
**Rationale**:
- PR URLs are permanent references to specific pull requests
- Better browser caching behavior
- Semantic correctness (these redirects don't change)
- SEO benefits for any indexed short URLs

### Port 80 Binding

**Decision**: Bind to port 80 for clean URLs
**Requirements**: Administrator privileges for installation
**Benefits**: Clean URLs without port numbers (`http://g/pr/xyz`)
**Alternative Considered**: Higher port with proxy (rejected for complexity)

## Service Integration Patterns

### gapir Integration

**Dependency Flow**:
1. `gapir` generates Base62-encoded PR IDs
2. Constructs short URLs: `http://g/pr/{encoded_id}`
3. Users click short URLs in terminal output
4. `kurz` service resolves and redirects to full Azure DevOps URLs

**Shared Components**:
- **Base62.cs**: Encoding/decoding utility used by both services
- **URL Patterns**: Consistent Base62 ID format

### Host Resolution Strategy

**Implementation**: Hosts file modification via PowerShell
```
127.0.0.1 g
```

**Benefits**:
- Clean domain name (`g`) following Microsoft internal conventions
- No external DNS dependencies
- Automatic setup via installation scripts
- Easy to remove/modify

## Error Handling Philosophy

### Graceful Degradation
- **Invalid Base62**: Fall back to decimal parsing
- **Invalid Routes**: 404 with descriptive error message
- **Service Unavailable**: gapir falls back to full URLs

### Error Response Strategy
- **404 for Invalid Routes**: Clear feedback about unsupported paths
- **Descriptive Messages**: Help users understand what went wrong
- **Logging**: Structured logging for production debugging

## Performance Design

### Startup Optimization
- **Minimal API**: Faster startup than full MVC
- **Lean Dependencies**: Only essential packages
- **Simple Configuration**: Dictionary-based routing

### Runtime Performance
- **O(1) Route Lookup**: Dictionary key access
- **Efficient Encoding**: Base62 conversion is lightweight
- **Browser Caching**: 308 redirects cached aggressively
- **No Database**: In-memory operation only

### Resource Efficiency
- **Low Memory**: Minimal framework overhead
- **Low CPU**: Simple string operations and redirects
- **Network Minimal**: Only redirect responses

## Deployment Strategies

### Development Mode
```powershell
.\install.ps1
```
- Console application for easy debugging
- Manual startup and testing
- Direct output for development feedback

### Production Mode
```powershell
.\install-service.ps1
```
- Windows Service installation
- Automatic startup configuration
- Service management capabilities
- Deployment to Program Files

### Installation Automation
**PowerShell Scripts Handle**:
- Build process (Release configuration)
- File deployment to system directories
- Hosts file modification
- Service registration and startup
- Cleanup and uninstallation

## Security Model

### Trust Boundaries
- **Internal Tool**: No authentication required
- **Localhost Only**: Not exposed externally
- **Trusted Network**: Microsoft internal environment

### Input Validation
- **Route Validation**: Dictionary lookup prevents injection
- **ID Validation**: Automatic via parsing (Base62/decimal)
- **URL Construction**: Template-based parameter insertion

## Service Management

### Installation Process
1. **Build**: Release configuration compilation
2. **Deploy**: Copy files to Program Files
3. **Configure**: Hosts file modification
4. **Register**: Windows Service installation
5. **Start**: Service activation and verification

### Operational Commands
```powershell
.\install-service.ps1 status      # Check service status
.\install-service.ps1 uninstall   # Complete removal
```

### Monitoring Strategy
- **Windows Service Manager**: Service status and control
- **Event Log Integration**: ASP.NET Core logging
- **Health Endpoint**: Could add `/health` for monitoring
- **Performance Counters**: Built-in ASP.NET Core metrics

## Failure Modes and Recovery

### Service Failures
- **Port Binding Issues**: Clear error messages for permission problems
- **Startup Failures**: Event log entries for debugging
- **Runtime Exceptions**: Graceful error responses

### Recovery Strategies
- **Automatic Restart**: Windows Service restart policies
- **Service Monitoring**: Windows Service infrastructure
- **Manual Recovery**: Service control commands
- **Fallback**: gapir can use full URLs when service unavailable

## Future Enhancement Points

### Easy Extensions
1. **New Route Types**: Add entries to route mapping dictionary
2. **Multiple Organizations**: Extend dictionary structure
3. **Analytics**: Add request counting and metrics
4. **Health Endpoints**: Monitoring and diagnostics

### Potential Features
- **Configuration File**: External JSON/YAML configuration
- **Dynamic Routes**: Runtime route management API
- **Load Balancing**: Multiple service instances
- **HTTPS Support**: SSL certificate management
- **Authentication**: Optional security layer

### Code Structure for Extensions
- **Route Handler**: Easily replaceable with more complex logic
- **Configuration**: Already abstracted into dictionary
- **Logging**: Structured logging ready for metrics
- **Service Interface**: Clean separation for testing

## Integration Testing

### Test Scenarios
1. **Base62 URL Resolution**: Verify encoding/decoding cycle
2. **Decimal URL Compatibility**: Ensure backward compatibility
3. **Invalid ID Handling**: Test error responses
4. **Service Installation**: Verify PowerShell automation
5. **Service Management**: Test start/stop/restart scenarios

### Development Testing
- **Console Mode**: Easy debugging and testing
- **Manual Verification**: Test URLs in browser
- **Integration with gapir**: End-to-end workflow testing

## Key Design Principles

1. **Simplicity**: Minimal API, simple routing, clear code structure
2. **Reliability**: Windows Service, automatic restart, error handling
3. **Performance**: Fast startup, efficient redirects, minimal resources
4. **Maintainability**: Clear separation, dictionary configuration, good logging
5. **Extensibility**: Easy to add new route types and features

This service design provides robust URL shortening while maintaining simplicity and reliability for the gapir ecosystem.
