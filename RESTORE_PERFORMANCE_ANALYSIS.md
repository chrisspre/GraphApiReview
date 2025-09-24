# .NET Restore Performance Analysis

## Issue
Sometimes `dotnet build` restore takes 10+ seconds, other times it's ~2 seconds with no apparent changes.

## Investigation Results

### Current Measurements (2025-01-17)
- Clean restore: ~2.5 seconds (consistent across multiple runs)
- Build without restore: ~2.6 seconds (consistent)
- Total build time: ~6-7 seconds (first time after clean)

### Potential Causes of Intermittent Slow Restores

1. **Network Latency to NuGet Feeds**
   - NuGet.org connectivity can vary
   - Corporate proxies may cause delays
   - DNS resolution timing

2. **Package Cache State**
   - Global packages cache (%USERPROFILE%\.nuget\packages) may need refresh
   - HTTP cache in temp folders may be stale
   - Lock file validation overhead

3. **System Resources**
   - Antivirus scanning during file access
   - I/O contention with other processes
   - Memory pressure affecting caching

4. **Project Configuration Impact**
   - `RestorePackagesWithLockFile=true` enables lock file validation
   - Multiple projects in solution restore in parallel
   - Preview .NET version (net9.0) may have different timing

## Recommendations

### Monitoring
To identify patterns, users can:
```powershell
# Time restore specifically
Measure-Command { dotnet restore }

# Clear various caches if issues persist:
dotnet nuget locals all --clear
```

### Optimization Options
1. **Remove lock files** if not needed in development:
   - Remove `RestorePackagesWithLockFile` from .csproj
   - Delete packages.lock.json files

2. **Use local NuGet cache**:
   - Configure local NuGet feed if in corporate environment

3. **Profile with diagnostics**:
   ```powershell
   dotnet restore --verbosity diagnostic
   ```

## Status
This appears to be a common .NET ecosystem issue rather than project-specific.
Current restore times (2-3 seconds) are reasonable for a project with 10+ package dependencies.
The 10-second times likely occur due to network/cache factors outside our control.