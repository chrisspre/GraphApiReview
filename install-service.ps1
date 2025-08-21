# kurz install-service script (moved to root)
$ErrorActionPreference = 'Stop'

Write-Host "Building kurz..."
dotnet build "$PSScriptRoot/src/kurz/kurz.csproj" -c Release

Write-Host "Installing Windows Service..."
$kurzBinPath = Join-Path $PSScriptRoot 'src/kurz/bin/Release'
$serviceExe = Get-ChildItem -Path $kurzBinPath -Recurse -Filter kurz.exe | Select-Object -First 1
if ($null -eq $serviceExe) {
    Write-Error "kurz.exe not found in build output."
    exit 1
}

$serviceName = "kurz-url-redirect"
$serviceDisplayName = "Kurz URL Redirect Service"
$servicePath = $serviceExe.FullName

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Write-Host "Service $serviceName already exists. Removing..."
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating service $serviceName..."
sc.exe create $serviceName binPath= $servicePath DisplayName= "$serviceDisplayName" start= auto
Write-Host "Service $serviceName installed."
