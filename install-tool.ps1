#!/usr/bin/env pwsh

# Install script for gapir .NET Global Tool
# This script builds and installs the gapir tool locally for development

param(
    [switch]$Force,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "Installing gapir .NET Global Tool..." -ForegroundColor Green

try {
    # Get the project directory
    $rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectDir = Join-Path $rootDir "src" "gapir"
    $projectFile = Join-Path $projectDir "gapir.csproj"
    
    if (-not (Test-Path $projectFile)) {
        throw "Could not find gapir.csproj at $projectFile"
    }
    
    Write-Host "Building project..." -ForegroundColor Yellow
    if ($WhatIf) {
        Write-Host "Would run: dotnet build $projectFile" -ForegroundColor Cyan
    } else {
        dotnet build $projectFile
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
    }
    
    Write-Host "Packing tool..." -ForegroundColor Yellow
    if ($WhatIf) {
        Write-Host "Would run: dotnet pack $projectFile" -ForegroundColor Cyan
    } else {
        dotnet pack $projectFile
        if ($LASTEXITCODE -ne 0) {
            throw "Pack failed"
        }
    }
    
    # Find the generated nupkg file
    $binDir = Join-Path $projectDir "bin"
    $nupkgFiles = Get-ChildItem -Path $binDir -Filter "*.nupkg" -Recurse | Sort-Object LastWriteTime -Descending
    
    if ($nupkgFiles.Count -eq 0) {
        throw "No .nupkg file found in $binDir"
    }
    
    $nupkgFile = $nupkgFiles[0].FullName
    Write-Host "Found package: $nupkgFile" -ForegroundColor Yellow
    
    # Uninstall existing version if it exists
    Write-Host "Checking for existing installation..." -ForegroundColor Yellow
    $existingTool = dotnet tool list --global | Select-String "gapir"
    if ($existingTool) {
        Write-Host "Uninstalling existing version..." -ForegroundColor Yellow
        if ($WhatIf) {
            Write-Host "Would run: dotnet tool uninstall --global gapir" -ForegroundColor Cyan
        } else {
            dotnet tool uninstall --global gapir
        }
    }
    
    # Install the tool
    Write-Host "Installing tool from $nupkgFile..." -ForegroundColor Yellow
    $installArgs = @("tool", "install", "--global", "gapir", "--add-source", (Split-Path $nupkgFile), "--version", "*")
    
    if ($Force) {
        $installArgs += "--ignore-failed-sources"
    }
    
    if ($WhatIf) {
        Write-Host "Would run: dotnet $($installArgs -join ' ')" -ForegroundColor Cyan
    } else {
        & dotnet @installArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Tool installation failed"
        }
    }
    
    Write-Host "✓ gapir tool installed successfully!" -ForegroundColor Green
    
    if (-not $WhatIf) {
        Write-Host "Testing installation..." -ForegroundColor Yellow
        $version = gapir --version
        Write-Host "Installed version: $version" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "Usage examples:" -ForegroundColor Cyan
        Write-Host "  gapir collect --help" -ForegroundColor Gray
        Write-Host "  gapir diagnose --help" -ForegroundColor Gray
    }
}
catch {
    Write-Error "Failed to install gapir tool: $_"
    exit 1
}
