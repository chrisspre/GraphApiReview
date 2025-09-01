#!/usr/bin/env pwsh

# Install script for kurz URL shortener service
# This script builds and installs the kurz service locally

param(
    [switch]$Force,
    [switch]$WhatIf,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Installing kurz URL shortener service..." -ForegroundColor Green

try {
    # Get the project directory
    $projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectFile = Join-Path $projectDir "kurz.csproj"
    
    if (-not (Test-Path $projectFile)) {
        throw "Could not find kurz.csproj at $projectFile"
    }
    
    Write-Host "Building project in $Configuration configuration..." -ForegroundColor Yellow
    if ($WhatIf) {
        Write-Host "Would run: dotnet build $projectFile --configuration $Configuration" -ForegroundColor Cyan
    } else {
        dotnet build $projectFile --configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
    }
    
    Write-Host "Publishing application..." -ForegroundColor Yellow
    $publishDir = Join-Path $projectDir "bin" $Configuration "net8.0" "publish"
    
    if ($WhatIf) {
        Write-Host "Would run: dotnet publish $projectFile --configuration $Configuration --output $publishDir" -ForegroundColor Cyan
    } else {
        dotnet publish $projectFile --configuration $Configuration --output $publishDir
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed"
        }
    }
    
    Write-Host "✓ kurz service built successfully!" -ForegroundColor Green
    Write-Host "Published to: $publishDir" -ForegroundColor Gray
    
    if (-not $WhatIf) {
        Write-Host ""
        Write-Host "To run the service:" -ForegroundColor Cyan
        Write-Host "  cd $publishDir" -ForegroundColor Gray
        Write-Host "  .\kurz.exe" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Or run directly:" -ForegroundColor Cyan
        Write-Host "  dotnet run --project $projectFile" -ForegroundColor Gray
    }
}
catch {
    Write-Error "Failed to install kurz service: $_"
    exit 1
}
