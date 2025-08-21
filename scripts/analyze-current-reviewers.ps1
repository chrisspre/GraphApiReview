#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes gapir output to identify potential API reviewers from current PRs.

.DESCRIPTION
    This script runs gapir with verbose logging to capture reviewer information
    from current PRs and helps identify who the API reviewers might be.

.EXAMPLE
    .\analyze-current-reviewers.ps1
#>

Write-Host "Current PR Reviewers Analyzer" -ForegroundColor Cyan
Write-Host ("=" * 40)

Write-Host "[INFO] Running gapir with detailed output to analyze current reviewers..." -ForegroundColor Yellow

# Check if we're in the right directory
if (-not (Test-Path "src/gapir/gapir.csproj")) {
    Write-Error "[ERROR] Please run this script from the GraphApiReview repository root"
    exit 1
}

# Run gapir with verbose output and capture it
Write-Host "[INFO] Fetching current PR data..." -ForegroundColor Yellow
try {
    $gapirOutput = dotnet run --project src/gapir -- -v 2>&1
    Write-Host $gapirOutput
    
    Write-Host "`n[INFO] Analysis complete. To find API reviewers:" -ForegroundColor Cyan
    Write-Host "1. Look at the PR table above" -ForegroundColor Gray
    Write-Host "2. Check which reviewers appear frequently across multiple PRs" -ForegroundColor Gray
    Write-Host "3. Look for reviewers with consistent 'API' or technical expertise" -ForegroundColor Gray
    Write-Host "4. These are likely your API reviewers" -ForegroundColor Gray
    
    Write-Host "`n[INFO] To manually update the fallback file:" -ForegroundColor Cyan
    Write-Host "1. Edit: src/gapir/ApiReviewersFallback.cs" -ForegroundColor Gray
    Write-Host "2. Add reviewer email addresses to the KnownApiReviewers HashSet" -ForegroundColor Gray
    Write-Host "3. Rebuild with: dotnet build" -ForegroundColor Gray
    Write-Host "4. Test with: dotnet run --project src/gapir" -ForegroundColor Gray
    
} catch {
    Write-Error "[ERROR] Failed to run gapir: $($_.Exception.Message)"
    exit 1
}

Write-Host "`nTip: The most common reviewers across your PRs are likely the API reviewers!" -ForegroundColor Yellow
