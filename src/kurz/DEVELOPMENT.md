# Kurz Development History & Architecture Decisions

This document captures the key architectural decisions and development history of the `kurz` URL redirect service to help future developers and GitHub Copilot understand the context and reasoning behind design choices.

## Project Overview

`kurz` is a lightweight ASP.NET Core minimal API service that provides URL redirection for the `gapir` tool. It enables clean, short URLs like `http://g/pr/{id}` that redirect to full Azure DevOps pull request URLs.

## Key Architectural Decisions

### 1. Minimal API Design

**Decision**: Use ASP.NET Core Minimal APIs instead of full MVC controllers
- **Rationale**: Lightweight, fast startup, simple routing needs
- **Benefits**: Reduced complexity, better performance, less boilerplate code
- **Trade-offs**: Limited to simple routing scenarios (acceptable for this use case)

### 2. Windows Service Integration

**Decision**: Built-in Windows Service support via `Microsoft.Extensions.Hosting.WindowsServices`
- **Problem**: Need reliable URL shortening service always available
- **Solution**: Windows Service with automatic startup
- **Benefits**: Survives reboots, runs without user login, system-level reliability
- **Installation**: PowerShell automation for service deployment

### 3. Base62 Encoding Strategy

**Decision**: Support both Base62 encoded and decimal PR IDs
- **Rationale**: Backward compatibility with existing URLs while enabling shorter URLs
- **Implementation**: Automatic detection and conversion
- **Benefits**: Shorter URLs for new PRs, existing decimal URLs still work

### 4. Route Mapping Architecture

**Decision**: Dictionary-based route configuration for extensibility
```csharp
var routeMappings = new Dictionary<string, string>
{
    ["pr"] = "https://dev.azure.com/msazure/One/_git/AD-AggregatorService-Workloads/pullrequest/"
};
```
- **Benefits**: Easy to add new route types without code changes
- **Future-proofing**: Simple to extend for work items, commits, etc.
- **Maintainability**: Configuration separate from routing logic

### 5. HTTP Status Code Choice

**Decision**: Use 308 Permanent Redirect instead of 302 Temporary Redirect
- **Rationale**: URLs are permanent references to specific PRs
- **Benefits**: Better browser caching, SEO optimization, clear semantic meaning
- **Performance**: Browsers cache permanent redirects more aggressively

## System Architecture

### Core Components

#### Program.cs - Application Entry Point
- **Responsibility**: Service configuration, routing setup, Windows Service integration
- **Key Features**: Port 80 binding, route mapping, error handling

#### Base62.cs - Encoding Utility
- **Shared Component**: Used by both `kurz` and `gapir`
- **Purpose**: Convert decimal PR IDs to/from compact Base62 strings
- **Character Set**: `0-9A-Za-z` (62 characters) for URL-safe encoding

#### Installation Scripts
- **install.ps1**: Developer setup (console mode)
- **install-service.ps1**: Production deployment (Windows Service)
- **Features**: Hosts file modification, automatic service management

### Network Configuration

#### Host Resolution
- **Local Domain**: `g` (internal Microsoft convention)
- **Implementation**: Hosts file entry `127.0.0.1 g`
- **Result**: `http://g/pr/{id}` resolves to localhost service

#### Port Strategy
- **Port 80**: Standard HTTP port for clean URLs without port numbers
- **Requirement**: Administrator privileges for binding
- **Alternative**: Could use higher port with proxy for non-admin deployment

## URL Design Patterns

### Short URL Structure
```PLAIN
http://g/pr/{id}
```

### ID Format Support
1. **Base62 Encoded**: `http://g/pr/OwAc` (compact, new default)
2. **Decimal**: `http://g/pr/12041652` (backward compatibility)

### Target URL Pattern
```PLAIN
https://dev.azure.com/msazure/One/_git/AD-AggregatorService-Workloads/pullrequest/{decimal_id}
```

## Error Handling Strategy

### Unsupported Routes
- **Response**: 404 Not Found with descriptive message
- **Philosophy**: Fail fast with clear feedback

### Invalid PR IDs
- **Base62 Decode Failures**: Graceful fallback to decimal parsing
- **Invalid Decimal**: 404 with error explanation
- **Logging**: Structured logging for debugging production issues

### Service Failures
- **Startup Issues**: Clear error messages for port binding, permissions
- **Runtime Errors**: Graceful degradation, service restart capability

## Performance Considerations

### Startup Performance
- **Minimal API**: Fast startup compared to full MVC
- **Dependency Injection**: Minimal service registration
- **Memory Footprint**: Lightweight for dedicated service role

### Runtime Performance
- **Route Lookup**: Dictionary O(1) lookup for route types
- **Encoding Performance**: Base62 conversion is CPU-efficient
- **Caching**: Relies on HTTP 308 for browser-level caching

### Resource Usage
- **Memory**: Minimal - no heavy frameworks or caching layers
- **CPU**: Low - simple string operations and redirects
- **Network**: Minimal - just redirect responses

## Security Considerations

### Access Control
- **Scope**: Internal tool, no authentication required
- **Network**: Localhost binding only (not exposed externally)
- **Trust Model**: Trusted internal network environment

### Input Validation
- **PR ID Validation**: Automatic via Base62/decimal parsing
- **Route Validation**: Dictionary key lookup prevents injection
- **URL Construction**: Template-based, safe parameter insertion

## Deployment Strategy

### Development Deployment
```powershell
.\install.ps1
```
- Console application mode
- Manual startup for testing
- Easy debugging and development

### Production Deployment
```powershell
.\install-service.ps1
```
- Windows Service installation
- Automatic startup configuration
- Service management capabilities
- File deployment to Program Files

### Service Management
- **Installation**: Automated via PowerShell
- **Updates**: Stop service, replace files, restart
- **Monitoring**: Windows Service infrastructure
- **Logs**: ASP.NET Core logging to Windows Event Log

## Integration with gapir

### Dependency Relationship
- **gapir**: Generates short URLs using Base62 encoding
- **kurz**: Resolves short URLs to full Azure DevOps URLs
- **Shared Code**: Base62.cs utility class

### URL Generation Flow
1. `gapir` encodes PR ID using Base62
2. Constructs short URL: `http://g/pr/{encoded_id}`
3. User clicks short URL
4. `kurz` service receives request
5. Decodes ID and redirects to full URL

### Failure Modes
- **Service Down**: URLs become non-functional
- **Mitigation**: Windows Service auto-restart, fallback to full URLs in gapir
- **Monitoring**: Service status checks, health endpoints

## Future Enhancements

### Extensibility Points
1. **New Route Types**: Add to route mapping dictionary
2. **Multiple Organizations**: Extend route mapping structure
3. **Analytics**: Add usage tracking and metrics
4. **Configuration**: External config file support
5. **Load Balancing**: Multiple service instances

### Potential Features
- **Health Endpoints**: `/health` for monitoring
- **Metrics**: Request counting, response time tracking
- **Configuration API**: Dynamic route management
- **Authentication**: Optional security for external access
- **HTTPS Support**: SSL certificate management

## Development Practices

### Code Quality
- **Nullable Reference Types**: Enabled for safety
- **Minimal Dependencies**: Only essential packages
- **Error Handling**: Comprehensive exception management
- **Logging**: Structured logging throughout

### Testing Strategy
- **Unit Tests**: Base62 encoding/decoding logic
- **Integration Tests**: End-to-end URL redirection
- **Service Tests**: Windows Service installation/management
- **Load Testing**: High-volume redirection scenarios

## Dependencies

### Core Dependencies
- **Microsoft.Extensions.Hosting.WindowsServices**: Windows Service support
- **ASP.NET Core**: Web framework (minimal APIs)
- **Base62 Utility**: Shared encoding logic

### System Dependencies
- **Windows**: Service host platform
- **Administrator Rights**: Port 80 binding, service installation
- **Hosts File**: Local domain resolution

## Operational Considerations

### Monitoring
- **Service Status**: Windows Service Manager
- **Application Logs**: Windows Event Log integration
- **Performance Counters**: ASP.NET Core metrics
- **Health Checks**: HTTP endpoint availability

### Maintenance
- **Updates**: Service stop/start cycle
- **Backup**: Service executable and configuration
- **Recovery**: Automated service restart policies
- **Debugging**: Console mode for development troubleshooting

This architecture provides a robust, lightweight URL shortening service that integrates seamlessly with the gapir tool while maintaining simplicity and reliability.
