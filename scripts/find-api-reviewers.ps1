#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Finds API reviewers from recent pull requests to help populate the fallback list.

.DESCRIPTION
    Analyzes recent PRs to identify who are the required/API reviewers based on reviewer patterns.
#>

# Configuration
$Organization = "https://dev.azure.com/msazure"
$Project = "One"
$Repository = "AD-AggregatorService-Workloads"

Write-Host "Finding API Reviewers from Recent Pull Requests" -ForegroundColor Cyan
Write-Host ("=" * 60)

# Configure Azure DevOps defaults
Write-Host "[INFO] Configuring Azure DevOps defaults..." -ForegroundColor Yellow
try {
    az devops configure --defaults organization=$Organization project=$Project
    Write-Host "[OK] Configured for: $Organization/$Project" -ForegroundColor Green
} catch {
    Write-Error "[ERROR] Failed to configure Azure DevOps defaults"
    exit 1
}

# Get recent PRs
Write-Host "[INFO] Fetching recent pull requests..." -ForegroundColor Yellow
try {
    $prs = az repos pr list --repository $Repository --status completed --top 20 --output json | ConvertFrom-Json
    Write-Host "[OK] Found $($prs.Count) recent completed PRs" -ForegroundColor Green
} catch {
    Write-Error "[ERROR] Failed to fetch pull requests: $($_.Exception.Message)"
    exit 1
}

# Analyze reviewers
Write-Host "[INFO] Analyzing reviewers..." -ForegroundColor Yellow
$allReviewers = @{}
$requiredReviewers = @{}

foreach ($pr in $prs) {
    Write-Host "  PR #$($pr.pullRequestId): $($pr.title.Substring(0, [Math]::Min(50, $pr.title.Length)))..." -ForegroundColor Gray
    
    foreach ($reviewer in $pr.reviewers) {
        $email = $reviewer.uniqueName
        $displayName = $reviewer.displayName
        $isRequired = $reviewer.isRequired -eq $true
        
        # Count all reviewers
        if (-not $allReviewers.ContainsKey($email)) {
            $allReviewers[$email] = @{
                displayName = $displayName
                count = 0
                requiredCount = 0
            }
        }
        $allReviewers[$email].count++
        
        # Track required reviewers
        if ($isRequired) {
            $allReviewers[$email].requiredCount++
            if (-not $requiredReviewers.ContainsKey($email)) {
                $requiredReviewers[$email] = @{
                    displayName = $displayName
                    count = 0
                }
            }
            $requiredReviewers[$email].count++
        }
    }
}

# Display results
Write-Host "`n[RESULTS] Frequent Required Reviewers (likely API reviewers):" -ForegroundColor Cyan
$sortedRequired = $requiredReviewers.GetEnumerator() | Sort-Object { $_.Value.count } -Descending

if ($sortedRequired.Count -eq 0) {
    Write-Host "No required reviewers found in recent PRs." -ForegroundColor Yellow
} else {
    foreach ($reviewer in $sortedRequired) {
        $email = $reviewer.Key
        $info = $reviewer.Value
        $totalCount = $allReviewers[$email].count
        $requiredCount = $info.count
        $percentage = [Math]::Round(($requiredCount / $totalCount) * 100)
        
        Write-Host "  $email" -ForegroundColor White
        Write-Host "    Name: $($info.displayName)" -ForegroundColor Gray
        Write-Host "    Required: $requiredCount/$totalCount PRs ($percentage%)" -ForegroundColor Gray
        Write-Host ""
    }
}

Write-Host "[RESULTS] All Frequent Reviewers:" -ForegroundColor Cyan
$sortedAll = $allReviewers.GetEnumerator() | Sort-Object { $_.Value.count } -Descending | Select-Object -First 10

foreach ($reviewer in $sortedAll) {
    $email = $reviewer.Key
    $info = $reviewer.Value
    $requiredCount = $info.requiredCount
    $totalCount = $info.count
    
    if ($requiredCount -gt 0) {
        $percentage = [Math]::Round(($requiredCount / $totalCount) * 100)
        Write-Host "  $email (Required: $requiredCount/$totalCount - $percentage%)" -ForegroundColor Green
    } else {
        Write-Host "  $email (Optional only: $totalCount)" -ForegroundColor Gray
    }
}

Write-Host "`n[SUGGESTION] Add these to ApiReviewersFallback.cs:" -ForegroundColor Cyan
foreach ($reviewer in $sortedRequired) {
    Write-Host "            `"$($reviewer.Key)`"," -ForegroundColor Yellow
}
