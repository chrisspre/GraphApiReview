# gapir install script for global dotnet tool
$ErrorActionPreference = 'Stop'

Write-Host "Packing gapir as a .NET global tool..."
$nupkgOut = Join-Path $PSScriptRoot 'nupkg'
if (-not (Test-Path $nupkgOut)) {
    New-Item -ItemType Directory -Path $nupkgOut | Out-Null
}
$csproj = Join-Path $PSScriptRoot 'src/gapir/gapir.csproj'
dotnet pack $csproj -c Release -o $nupkgOut

# Find the .nupkg
$nupkg = Get-ChildItem -Path $nupkgOut -Filter 'gapir.*.nupkg' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $nupkg) {
    Write-Error ".nupkg not found. Ensure gapir.csproj is configured as a .NET tool."
    exit 1
}

# Install or update the tool
Write-Host "Installing/updating gapir as a global .NET tool..."

# Check if gapir is already installed
$existingTool = dotnet tool list --global | Select-String "gapir"
if ($existingTool) {
    Write-Host "gapir is already installed. Updating to latest version..."
    $updateResult = dotnet tool update --global --add-source $nupkgOut gapir --ignore-failed-sources --no-cache 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "gapir updated successfully!" -ForegroundColor Green
    } else {
        Write-Host "Update failed. Trying uninstall and reinstall..." -ForegroundColor Yellow
        dotnet tool uninstall --global gapir
        $installResult = dotnet tool install --global --add-source $nupkgOut gapir --ignore-failed-sources --no-cache 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "gapir installed successfully as a global .NET CLI tool." -ForegroundColor Green
        } else {
            Write-Error "Failed to install gapir: $installResult"
            exit 1
        }
    }
} else {
    Write-Host "Installing gapir for the first time..."
    $installResult = dotnet tool install --global --add-source $nupkgOut gapir --ignore-failed-sources --no-cache 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "gapir installed successfully as a global .NET CLI tool." -ForegroundColor Green
    } else {
        Write-Error "Failed to install gapir: $installResult"
        exit 1
    }
}
