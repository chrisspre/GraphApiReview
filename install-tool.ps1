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

# Install the tool
Write-Host "Installing gapir as a global .NET tool..."
$installResult = dotnet tool install --global --add-source $nupkgOut gapir --ignore-failed-sources --no-cache 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "gapir installed successfully as a global .NET CLI tool."
} else {
    # Check if it's a conflict error
    if ($installResult -match "conflicts with an existing command") {
        Write-Host "ERROR: You have a conflicting tool installed with command 'gapir'." -ForegroundColor Red
        Write-Host "Please run 'dotnet tool list --global' to see installed tools," -ForegroundColor Yellow
        Write-Host "then uninstall the conflicting tool first using 'dotnet tool uninstall --global <package-id>'" -ForegroundColor Yellow
        exit 1
    } else {
        Write-Error "Failed to install gapir as a global tool: $installResult"
        exit 1
    }
}
