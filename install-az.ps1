$packageId = "Microsoft.AzureCLI"

# Check if the package is already installed
$installed = winget list --id $packageId | Select-String $packageId

if (-not $installed) {
    Write-Host "Installing $packageId via winget..."
    winget install --id $packageId --exact --silent
} else {
    Write-Host "$packageId is already installed."
}