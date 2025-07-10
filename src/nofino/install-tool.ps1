# Install script for nofino tool
# This script builds and installs the nofino tool as a global .NET tool

Write-Host "🚀 Installing nofino (Microsoft Graph Extensions Manager)..." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

# Check if .NET is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "✅ .NET Version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "❌ .NET is not installed or not in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 9.0 or later from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Navigate to the project directory
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptPath

try {
    # Restore dependencies
    Write-Host "📦 Restoring dependencies..." -ForegroundColor Yellow
    dotnet restore
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore dependencies"
    }

    # Build the project
    Write-Host "🔨 Building project..." -ForegroundColor Yellow
    dotnet build --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build project"
    }

    # Uninstall previous version if it exists
    Write-Host "🗑️  Uninstalling previous version (if exists)..." -ForegroundColor Yellow
    dotnet tool uninstall -g NoFino 2>$null

    # Pack and install as global tool
    Write-Host "📦 Packing and installing as global tool..." -ForegroundColor Yellow
    dotnet pack --configuration Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pack project"
    }

    # Find the generated package
    $nupkgPath = Get-ChildItem -Path "bin\Release" -Filter "*.nupkg" | Select-Object -First 1
    if (-not $nupkgPath) {
        throw "Could not find generated package"
    }

    # Install the tool globally
    dotnet tool install -g NoFino --add-source ".\bin\Release"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install tool"
    }

    Write-Host "✅ nofino installed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage examples:" -ForegroundColor Cyan
    Write-Host "  nofino get             - Get the microsoft.teams.baffino extension" -ForegroundColor White
    Write-Host "  nofino set             - Create/update with default timeAllocation (99)" -ForegroundColor White
    Write-Host "  nofino set 75          - Create/update with timeAllocation of 75" -ForegroundColor White
    Write-Host "  nofino list            - List all user extensions" -ForegroundColor White
    Write-Host "  nofino delete          - Delete the microsoft.teams.baffino extension" -ForegroundColor White
    Write-Host ""
    Write-Host "🔐 Note: The tool will use modern authentication (Windows Hello/PIN/Biometrics)" -ForegroundColor Green
    Write-Host "    with fallback to device code flow if needed." -ForegroundColor Green

} catch {
    Write-Host "❌ Installation failed: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
}
