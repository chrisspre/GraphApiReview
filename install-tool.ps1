# gapir install script (moved to root)
$ErrorActionPreference = 'Stop'

Write-Host "Restoring .NET tools..."
dotnet tool restore

Write-Host "Building gapir..."
dotnet build ./src/gapir/gapir.csproj -c Release

Write-Host "Copying output to ./bin..."
$buildOutput = Get-ChildItem -Path ./src/gapir/bin/Release -Recurse -Filter gapir.exe | Select-Object -First 1
if ($null -eq $buildOutput) {
    Write-Error "gapir.exe not found in build output."
    exit 1
}

$destDir = Join-Path -Path $PSScriptRoot -ChildPath 'bin'
if (-not (Test-Path $destDir)) {
    New-Item -ItemType Directory -Path $destDir | Out-Null
}
Copy-Item $buildOutput.FullName $destDir -Force
Write-Host "gapir.exe copied to $destDir"
