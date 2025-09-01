#!/usr/bin/env pwsh

# Service installation script for kurz URL shortener
# This script installs kurz as a Windows service

param(
    [switch]$Uninstall,
    [switch]$WhatIf,
    [string]$ServiceName = "kurz",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Require Administrator privileges
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script requires Administrator privileges. Please run as Administrator."
    exit 1
}

try {
    if ($Uninstall) {
        Write-Host "Uninstalling kurz service..." -ForegroundColor Yellow
        
        # Stop service if running
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service) {
            if ($service.Status -eq "Running") {
                Write-Host "Stopping service..." -ForegroundColor Yellow
                if ($WhatIf) {
                    Write-Host "Would run: Stop-Service -Name $ServiceName" -ForegroundColor Cyan
                } else {
                    Stop-Service -Name $ServiceName -Force
                }
            }
            
            Write-Host "Removing service..." -ForegroundColor Yellow
            if ($WhatIf) {
                Write-Host "Would run: sc.exe delete $ServiceName" -ForegroundColor Cyan
            } else {
                & sc.exe delete $ServiceName
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to remove service"
                }
            }
            
            Write-Host "✓ Service uninstalled successfully!" -ForegroundColor Green
        } else {
            Write-Host "Service not found." -ForegroundColor Yellow
        }
        return
    }
    
    Write-Host "Installing kurz as Windows service..." -ForegroundColor Green
    
    # Get the project directory and build
    $projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectFile = Join-Path $projectDir "kurz.csproj"
    
    if (-not (Test-Path $projectFile)) {
        throw "Could not find kurz.csproj at $projectFile"
    }
    
    # Build and publish
    Write-Host "Building and publishing application..." -ForegroundColor Yellow
    $publishDir = Join-Path $projectDir "bin" $Configuration "net8.0" "publish"
    
    if ($WhatIf) {
        Write-Host "Would run: dotnet publish $projectFile --configuration $Configuration --output $publishDir" -ForegroundColor Cyan
    } else {
        dotnet publish $projectFile --configuration $Configuration --output $publishDir
        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed"
        }
    }
    
    $exePath = Join-Path $publishDir "kurz.exe"
    if (-not (Test-Path $exePath) -and -not $WhatIf) {
        throw "Could not find kurz.exe at $exePath"
    }
    
    # Stop and remove existing service if it exists
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Host "Removing existing service..." -ForegroundColor Yellow
        if ($existingService.Status -eq "Running") {
            if ($WhatIf) {
                Write-Host "Would run: Stop-Service -Name $ServiceName" -ForegroundColor Cyan
            } else {
                Stop-Service -Name $ServiceName -Force
            }
        }
        
        if ($WhatIf) {
            Write-Host "Would run: sc.exe delete $ServiceName" -ForegroundColor Cyan
        } else {
            & sc.exe delete $ServiceName
        }
        
        # Wait a moment for service to be fully removed
        Start-Sleep -Seconds 2
    }
    
    # Create the service
    Write-Host "Creating service..." -ForegroundColor Yellow
    $serviceArgs = @(
        "create",
        $ServiceName,
        "binPath= `"$exePath`"",
        "DisplayName= `"Kurz URL Shortener`"",
        "start= auto"
    )
    
    if ($WhatIf) {
        Write-Host "Would run: sc.exe $($serviceArgs -join ' ')" -ForegroundColor Cyan
    } else {
        & sc.exe @serviceArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to create service"
        }
    }
    
    # Start the service
    Write-Host "Starting service..." -ForegroundColor Yellow
    if ($WhatIf) {
        Write-Host "Would run: Start-Service -Name $ServiceName" -ForegroundColor Cyan
    } else {
        Start-Service -Name $ServiceName
        
        # Wait a moment and check status
        Start-Sleep -Seconds 3
        $service = Get-Service -Name $ServiceName
        if ($service.Status -eq "Running") {
            Write-Host "✓ Service installed and started successfully!" -ForegroundColor Green
        } else {
            Write-Warning "Service was created but failed to start. Check event logs for details."
        }
    }
    
    Write-Host ""
    Write-Host "Service management commands:" -ForegroundColor Cyan
    Write-Host "  Start:   Start-Service -Name $ServiceName" -ForegroundColor Gray
    Write-Host "  Stop:    Stop-Service -Name $ServiceName" -ForegroundColor Gray
    Write-Host "  Status:  Get-Service -Name $ServiceName" -ForegroundColor Gray
    Write-Host "  Remove:  .\install-service.ps1 -Uninstall" -ForegroundColor Gray
}
catch {
    Write-Error "Failed to install kurz service: $_"
    exit 1
}
