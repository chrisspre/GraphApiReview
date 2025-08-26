# Helper script to update the gapir tool locally
# Usage: .\update-tool.ps1

Write-Host "🔄 Updating gapir tool..." -ForegroundColor Cyan

# Step 1: Build and pack
Write-Host "📦 Building and packing..." -ForegroundColor Yellow
dotnet pack src/gapir -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Step 2: Copy to local package source
Write-Host "📋 Copying to local package source..." -ForegroundColor Yellow
$version = (Select-Xml -Path "src/gapir/gapir.csproj" -XPath "//PackageVersion").Node.InnerText
$packageFile = "src/gapir/bin/Release/gapir.$version.nupkg"

if (-not (Test-Path $packageFile)) {
    Write-Error "Package file not found: $packageFile"
    exit 1
}

# Ensure LocalPackages directory exists
$localPackagesDir = "..\LocalPackages"
if (-not (Test-Path $localPackagesDir)) {
    New-Item -ItemType Directory -Path $localPackagesDir | Out-Null
}

Copy-Item $packageFile $localPackagesDir -Force

# Step 3: Update the tool
Write-Host "🔧 Updating global tool..." -ForegroundColor Yellow
dotnet tool uninstall --global gapir 2>$null  # Suppress error if not installed
dotnet tool install --global --add-source $localPackagesDir gapir

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Tool updated successfully!" -ForegroundColor Green
    Write-Host "Version: " -NoNewline -ForegroundColor Gray
    gapir --version
} else {
    Write-Error "❌ Tool update failed!"
    exit $LASTEXITCODE
}
