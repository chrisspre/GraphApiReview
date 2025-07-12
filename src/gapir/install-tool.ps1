#!/usr/bin/env pwsh
# install-tool.ps1
# Script to build, pack, and install the gapir (Graph API review) tool

Write-Host "Starting gapir tool installation process..." -ForegroundColor Green
Write-Host ""

# Step 1: Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
try {
    if (Test-Path "bin") {
        Remove-Item -Path "bin" -Recurse -Force
        Write-Host "Cleaned bin directory" -ForegroundColor Green
    }
    if (Test-Path "obj") {
        Remove-Item -Path "obj" -Recurse -Force
        Write-Host "Cleaned obj directory" -ForegroundColor Green
    }
} catch {
    Write-Host "Warning: Could not clean directories: $($_.Exception.Message)" -ForegroundColor Yellow
}
Write-Host ""

# Step 2: Build the project
Write-Host "Building the project..." -ForegroundColor Yellow
try {
    dotnet build --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Host "Build completed successfully" -ForegroundColor Green
} catch {
    Write-Host "Build failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 3: Pack the project
Write-Host "Packing the project..." -ForegroundColor Yellow
try {
    dotnet pack --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Pack failed with exit code $LASTEXITCODE"
    }
    Write-Host "Pack completed successfully" -ForegroundColor Green
} catch {
    Write-Host "Pack failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 4: Uninstall existing tool (if any)
Write-Host "Uninstalling existing gapir tool..." -ForegroundColor Yellow
try {
    dotnet tool uninstall -g GraphApiReview 2>$null
    Write-Host "Existing tool uninstalled" -ForegroundColor Green
} catch {
    Write-Host "No existing tool found to uninstall" -ForegroundColor Cyan
}
Write-Host ""

# Step 5: Install the new tool
Write-Host "Installing gapir tool globally..." -ForegroundColor Yellow
try {
    dotnet tool install -g --add-source ./bin/Release GraphApiReview
    if ($LASTEXITCODE -ne 0) {
        throw "Tool installation failed with exit code $LASTEXITCODE"
    }
    Write-Host "gapir tool installed successfully" -ForegroundColor Green
} catch {
    Write-Host "Tool installation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 6: Verify installation
Write-Host "Verifying installation..." -ForegroundColor Yellow
try {
    $toolList = dotnet tool list -g | Select-String "GraphApiReview"
    if ($toolList) {
        Write-Host "Tool verification successful:" -ForegroundColor Green
        Write-Host "   $toolList" -ForegroundColor Cyan
    } else {
        Write-Host "Tool installed but not found in global tool list" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Could not verify tool installation: $($_.Exception.Message)" -ForegroundColor Yellow
}
Write-Host ""

# Success message
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "You can now use the tool with: " -NoNewline -ForegroundColor White
Write-Host "gapir" -ForegroundColor Cyan
Write-Host ""
Write-Host "Examples:" -ForegroundColor White
Write-Host "  gapir                    # Check pull requests" -ForegroundColor Cyan
Write-Host "  gapir --show-approved    # Include already approved PRs" -ForegroundColor Cyan
Write-Host "  gapir --help             # Show all options" -ForegroundColor Cyan
Write-Host ""
