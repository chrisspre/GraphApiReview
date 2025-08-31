#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Install script for kurz URL shortener service

.DESCRIPTION
    This script builds and installs the kurz service either for manual execution 
    or as a Windows service for automatic startup.

.PARAMETER AsService
    Install kurz as a Windows service (requires Administrator privileges)

.PARAMETER Uninstall
    Uninstall the Windows service (only works with -AsService)

.PARAMETER Force
    Force installation, ignoring some errors

.PARAMETER WhatIf
    Show what would be done without actually doing it

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER ServiceName
    Name for the Windows service. Default: kurz

.EXAMPLE
    .\install-kurz.ps1
    Build and publish kurz for manual execution

.EXAMPLE
    .\install-kurz.ps1 -AsService
    Install kurz as a Windows service (requires admin)

.EXAMPLE
    .\install-kurz.ps1 -AsService -Uninstall
    Remove the kurz Windows service

.EXAMPLE
    .\install-kurz.ps1 -WhatIf
    Preview what the script would do
#>

# Install script for kurz URL shortener service
# This script builds and installs the kurz service locally or as a Windows service

param(
    [switch]$AsService,
    [switch]$Uninstall,
    [switch]$Force,
    [switch]$WhatIf,
    [string]$Configuration = "Release",
    [string]$ServiceName = "kurz"
)

$ErrorActionPreference = "Stop"

# Check for Administrator privileges if installing as service
if ($AsService -and -NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "Installing as a service requires Administrator privileges. Please run as Administrator or use without -AsService."
    exit 1
}

if ($AsService) {
    Write-Host "Installing kurz as Windows service..." -ForegroundColor Green
} else {
    Write-Host "Installing kurz URL shortener service..." -ForegroundColor Green
}

try {
    # Handle service uninstallation
    if ($Uninstall -and $AsService) {
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
    } elseif ($Uninstall) {
        Write-Host "Nothing to uninstall for manual installation mode." -ForegroundColor Yellow
        return
    }
    
    # Get the project directory
    $rootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectDir = Join-Path $rootDir "src" "kurz"
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
    
    if ($AsService) {
        # Install as Windows Service
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
        Write-Host "  Remove:  .\install-kurz.ps1 -AsService -Uninstall" -ForegroundColor Gray
    } else {
        # Manual installation instructions
        if (-not $WhatIf) {
            Write-Host ""
            Write-Host "To run the service:" -ForegroundColor Cyan
            Write-Host "  cd $publishDir" -ForegroundColor Gray
            Write-Host "  .\kurz.exe" -ForegroundColor Gray
            Write-Host ""
            Write-Host "Or run directly:" -ForegroundColor Cyan
            Write-Host "  dotnet run --project $projectFile" -ForegroundColor Gray
            Write-Host ""
            Write-Host "To install as a service instead:" -ForegroundColor Cyan
            Write-Host "  .\install-kurz.ps1 -AsService" -ForegroundColor Gray
        }
    }
}
catch {
    if ($AsService) {
        Write-Error "Failed to install kurz service: $_"
    } else {
        Write-Error "Failed to install kurz: $_"
    }
    exit 1
}
