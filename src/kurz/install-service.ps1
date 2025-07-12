# Install-Service.ps1
# Run this script as Administrator to install the Kurz URL Redirect Service

param(
    [string]$Action = "install"
)

$ServiceName = "KurzUrlRedirectService"
$ServiceDisplayName = "Kurz URL Redirect Service"
$ServiceDescription = "A lightweight extensible URL redirect service"
$ExePath = "$PWD\bin\Debug\net8.0\kurz.exe"
$HostsFile = "$env:SystemRoot\System32\drivers\etc\hosts"
$HostEntry = "127.0.0.1    g"

function Install-KurzService {
    Write-Host "Installing Kurz URL Redirect Service..." -ForegroundColor Green
    
    # Update hosts file first
    $hostsUpdated = Update-HostsFile
    if (-not $hostsUpdated) {
        Write-Host "⚠️  Hosts file update failed, but continuing with service installation..." -ForegroundColor Yellow
    }
    
    # Check if service already exists
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Host "Service already exists. Stopping and removing first..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName
        Start-Sleep -Seconds 2
    }
    
    # Build the project first
    Write-Host "Building the project..." -ForegroundColor Cyan
    dotnet build -c Release
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
    
    $ExePath = "$PWD\bin\Release\net8.0\kurz.exe"
    
    # Create the service
    Write-Host "Creating Windows Service..." -ForegroundColor Cyan
    sc.exe create $ServiceName binPath= "$ExePath" start= auto DisplayName= "$ServiceDisplayName"
    
    if ($LASTEXITCODE -eq 0) {
        # Set service description
        sc.exe description $ServiceName "$ServiceDescription"
        
        # Start the service
        Write-Host "Starting service..." -ForegroundColor Cyan
        Start-Service -Name $ServiceName
        
        Write-Host "✅ Service installed and started successfully!" -ForegroundColor Green
        Write-Host "Service Name: $ServiceName" -ForegroundColor Yellow
        Write-Host "Service will start automatically after OS startup" -ForegroundColor Yellow
        Write-Host "Access your service at: http://g/pr/{id} (or other configured routes)" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Failed to create service!" -ForegroundColor Red
    }
}

function Uninstall-KurzService {
    Write-Host "Uninstalling Kurz URL Redirect Service..." -ForegroundColor Yellow
    
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName
        Write-Host "✅ Service uninstalled successfully!" -ForegroundColor Green
    } else {
        Write-Host "Service not found." -ForegroundColor Yellow
    }
}

function Show-ServiceStatus {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "Service Status: $($service.Status)" -ForegroundColor Cyan
        Write-Host "Service Start Type: $($service.StartType)" -ForegroundColor Cyan
        
        # Check hosts file entry
        try {
            $hostsContent = Get-Content $HostsFile -ErrorAction Stop
            $existingEntry = $hostsContent | Where-Object { $_ -match "^\s*127\.0\.0\.1\s+g\s*$" }
            
            if ($existingEntry) {
                Write-Host "Hosts File: ✅ g entry present" -ForegroundColor Green
            } else {
                Write-Host "Hosts File: ❌ g entry missing" -ForegroundColor Red
                Write-Host "Run '.\Install-Service.ps1 install' to add it" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "Hosts File: ⚠️  Cannot read hosts file" -ForegroundColor Yellow
        }
    } else {
        Write-Host "Service not installed." -ForegroundColor Yellow
    }
}

function Update-HostsFile {
    Write-Host "Checking hosts file for g entry..." -ForegroundColor Cyan
    
    # Check if hosts file exists and is readable
    if (-not (Test-Path $HostsFile)) {
        Write-Host "❌ Cannot find hosts file at $HostsFile" -ForegroundColor Red
        return $false
    }
    
    try {
        $hostsContent = Get-Content $HostsFile -ErrorAction Stop
        
        # Check if g entry already exists
        $existingEntry = $hostsContent | Where-Object { $_ -match "^\s*127\.0\.0\.1\s+g\s*$" }
        
        if ($existingEntry) {
            Write-Host "✅ g entry already exists in hosts file" -ForegroundColor Green
            return $true
        }
        
        # Add the entry
        Write-Host "Adding g entry to hosts file..." -ForegroundColor Yellow
        Add-Content -Path $HostsFile -Value $HostEntry -ErrorAction Stop
        Write-Host "✅ g entry added to hosts file" -ForegroundColor Green
        return $true
        
    } catch {
        Write-Host "❌ Failed to update hosts file: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "You may need to manually add '127.0.0.1    g' to $HostsFile" -ForegroundColor Yellow
        return $false
    }
}

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")

if (-not $isAdmin) {
    Write-Host "❌ This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

switch ($Action.ToLower()) {
    "install" { 
        Install-KurzService 
        Update-HostsFile
    }
    "uninstall" { Uninstall-KurzService }
    "status" { Show-ServiceStatus }
    default {
        Write-Host "Usage: .\Install-Service.ps1 [install|uninstall|status]" -ForegroundColor Yellow
        Write-Host "  install   - Install and start the service (default)"
        Write-Host "  uninstall - Stop and remove the service"
        Write-Host "  status    - Show current service status"
    }
}
