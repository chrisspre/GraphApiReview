# Install-Service.ps1
# Run this script as Administrator to install the Kurz URL Redirect Service

param(
    [string]$Action = "install"
)

$ServiceName = "KurzUrlRedirectService"
$ServiceDisplayName = "Kurz URL Redirect Service"
$ServiceDescription = "A lightweight extensible URL redirect service"
$InstallPath = "$env:ProgramFiles\Kurz"
$ExePath = "$InstallPath\kurz.exe"
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
    
    # Create installation directory
    Write-Host "Creating installation directory..." -ForegroundColor Cyan
    if (-not (Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Host "✅ Created directory: $InstallPath" -ForegroundColor Green
    }
    
    # Copy service files to installation directory
    Write-Host "Copying service files to $InstallPath..." -ForegroundColor Cyan
    $SourcePath = "$PWD\bin\Release\net8.0"
    
    if (-not (Test-Path $SourcePath)) {
        Write-Host "❌ Build output not found at $SourcePath" -ForegroundColor Red
        exit 1
    }
    
    # Copy all files from the build output
    Copy-Item -Path "$SourcePath\*" -Destination $InstallPath -Recurse -Force
    Write-Host "✅ Service files copied successfully" -ForegroundColor Green
    
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
    
    # Stop and remove service
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Host "Stopping service..." -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        Write-Host "Removing service..." -ForegroundColor Cyan
        sc.exe delete $ServiceName
        Write-Host "✅ Service uninstalled successfully!" -ForegroundColor Green
    } else {
        Write-Host "Service not found." -ForegroundColor Yellow
    }
    
    # Remove installation directory
    if (Test-Path $InstallPath) {
        Write-Host "Removing installation directory..." -ForegroundColor Cyan
        try {
            Remove-Item -Path $InstallPath -Recurse -Force
            Write-Host "✅ Installation directory removed: $InstallPath" -ForegroundColor Green
        } catch {
            Write-Host "⚠️  Failed to remove installation directory: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "You may need to manually remove: $InstallPath" -ForegroundColor Yellow
        }
    }
}

function Show-ServiceStatus {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        Write-Host "Service Status: $($service.Status)" -ForegroundColor Cyan
        Write-Host "Service Start Type: $($service.StartType)" -ForegroundColor Cyan
        Write-Host "Installation Path: $InstallPath" -ForegroundColor Cyan
        
        # Check if installation files exist
        if (Test-Path $ExePath) {
            Write-Host "Installation Files: ✅ Present" -ForegroundColor Green
        } else {
            Write-Host "Installation Files: ❌ Missing" -ForegroundColor Red
        }
        
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
