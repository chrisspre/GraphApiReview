# Publishing gapir Tool

## Publishing to GitHub Packages (Interactive Auth)

### 1. One-time setup

```powershell
# Add GitHub Packages as a source (no password stored)
dotnet nuget add source --name github "https://nuget.pkg.github.com/chrisspre/index.json"
```

### 2. Build and publish

```powershell
# Build the package
dotnet pack src/gapir/gapir.csproj -c Release

# Push with interactive authentication
dotnet nuget push src/gapir/bin/Release/gapir.1.0.0.nupkg --source github --interactive
```

The `--interactive` flag will:

- Prompt you for GitHub username/token when needed
- Use browser authentication if available
- Not store credentials anywhere

## Installing for Users

For users to install the tool from GitHub Packages:

```powershell
dotnet tool install -g gapir --add-source "https://nuget.pkg.github.com/chrisspre/index.json"
```

## Alternative: NuGet.org (Public)

For maximum ease of use, you can publish to NuGet.org:

### You publish once

```powershell
# Get API key from nuget.org (one-time)
dotnet nuget push src/gapir/bin/Release/gapir.1.0.0.nupkg --api-key YOUR_NUGET_KEY --source https://api.nuget.org/v3/index.json
```

### Users install with zero setup

```powershell
dotnet tool install -g gapir
```
