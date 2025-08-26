# kurz install-service script
$ErrorActionPreference = 'Stop'

Write-Host "Building kurz..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot/src/kurz/kurz.csproj" -c Release

Write-Host "Installing kurz Windows Service..." -ForegroundColor Yellow
$kurzBinPath = Join-Path $PSScriptRoot 'src/kurz/bin/Release'
$serviceExe = Get-ChildItem -Path $kurzBinPath -Recurse -Filter kurz.exe | Select-Object -First 1

if ($null -eq $serviceExe) {
    Write-Error "kurz.exe not found in build output!"
    exit 1
}

$serviceExePath = $serviceExe.FullName
$serviceName = "KurzUrlShortener"

Write-Host "Found kurz.exe at: $serviceExePath" -ForegroundColor Gray

# Check if service already exists
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service '$serviceName' already exists. Stopping and removing..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName
    Start-Sleep -Seconds 2
}

# Install the service
Write-Host "Installing Windows Service '$serviceName'..." -ForegroundColor Green
sc.exe create $serviceName binPath= $serviceExePath start= auto

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Service installed successfully!" -ForegroundColor Green
    Write-Host "Starting service..." -ForegroundColor Yellow
    Start-Service -Name $serviceName
    Write-Host "✅ Service started!" -ForegroundColor Green
    Write-Host ""
    Write-Host "kurz URL shortener is now running as a Windows Service" -ForegroundColor Cyan
    Write-Host "Service name: $serviceName" -ForegroundColor Gray
} else {
    Write-Error "❌ Failed to install service!"
    exit 1
}
