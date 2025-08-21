#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analyzes past pull requests to find required reviewers and build API reviewers list.

.DESCRIPTION
    This script fetches recent pull requests and analyzes the required reviewers
    to build a comprehensive list of API reviewers based on actual PR data.

.PARAMETER MaxPRs
    Maximum number of PRs to analyze. Defaults to 50.

.PARAMETER OutputPath
    Path where the C# file will be created. Defaults to '../src/gapir/ApiReviewersFallback.cs'

.EXAMPLE
    .\collect-required-reviewers.ps1
    
.EXAMPLE
    .\collect-required-reviewers.ps1 -MaxPRs 100
#>

param(
    [int]$MaxPRs = 50,
    [string]$OutputPath = "../src/gapir/ApiReviewersFallback.cs"
)

# Configuration
$Organization = "https://dev.azure.com/msazure"
$Project = "One"
$Repository = "AD-AggregatorService-Workloads"

Write-Host "Pull Request Required Reviewers Analyzer" -ForegroundColor Cyan
Write-Host ("=" * 50)

# Check if Azure CLI is installed
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "[OK] Azure CLI found: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Error "[ERROR] Azure CLI not found. Please install Azure CLI first."
    exit 1
}

# Configure Azure DevOps defaults
Write-Host "[INFO] Configuring Azure DevOps defaults..." -ForegroundColor Yellow
try {
    az devops configure --defaults organization=$Organization project=$Project
    Write-Host "[OK] Configured for: $Organization/$Project" -ForegroundColor Green
} catch {
    Write-Error "[ERROR] Failed to configure Azure DevOps defaults"
    exit 1
}

# Check authentication
Write-Host "[INFO] Checking authentication..." -ForegroundColor Yellow
try {
    $user = az devops user show --user me --output json 2>&1
    if ($user -match "TF400813.*not authorized") {
        Write-Error "[ERROR] Access denied. You need access to the Azure DevOps organization."
        Write-Error "Visit: $Organization and ensure you can access the project."
        exit 1
    }
    $userJson = $user | ConvertFrom-Json
    Write-Host "[OK] Authenticated as: $($userJson.displayName)" -ForegroundColor Green
} catch {
    Write-Error "[ERROR] Authentication failed. Please run 'az login' first."
    exit 1
}

# Fetch recent pull requests
Write-Host "[INFO] Fetching recent pull requests (limit: $MaxPRs)..." -ForegroundColor Yellow
try {
    $prListCommand = "az repos pr list --repository '$Repository' --status completed --top $MaxPRs --output json"
    $pullRequests = Invoke-Expression $prListCommand | ConvertFrom-Json
    
    if (-not $pullRequests -or $pullRequests.Count -eq 0) {
        Write-Warning "[WARN] No completed pull requests found"
        exit 1
    }
    
    Write-Host "[OK] Found $($pullRequests.Count) completed pull requests" -ForegroundColor Green
} catch {
    Write-Error "[ERROR] Failed to fetch pull requests: $($_.Exception.Message)"
    exit 1
}

# Analyze required reviewers
Write-Host "[INFO] Analyzing required reviewers..." -ForegroundColor Yellow
$requiredReviewers = @{}
$totalPRsAnalyzed = 0
$prsWithRequiredReviewers = 0

foreach ($pr in $pullRequests) {
    $totalPRsAnalyzed++
    Write-Host "  Analyzing PR #$($pr.pullRequestId): $($pr.title.Substring(0, [Math]::Min(50, $pr.title.Length)))..." -ForegroundColor Gray
    
    try {
        # Get detailed PR info including reviewers
        $prDetail = az repos pr show --id $pr.pullRequestId --output json | ConvertFrom-Json
        
        if ($prDetail.reviewers -and $prDetail.reviewers.Count -gt 0) {
            $hasRequiredReviewers = $false
            
            foreach ($reviewer in $prDetail.reviewers) {
                # Check if this is a required reviewer (isRequired = true or vote indicates requirement)
                if ($reviewer.isRequired -eq $true -or $reviewer.vote -gt 0) {
                    $hasRequiredReviewers = $true
                    
                    # Collect all identifiers for this reviewer
                    $reviewerKey = $reviewer.displayName
                    if (-not $requiredReviewers.ContainsKey($reviewerKey)) {
                        $requiredReviewers[$reviewerKey] = @{
                            displayName = $reviewer.displayName
                            uniqueName = $reviewer.uniqueName
                            mailAddress = if ($reviewer.mailAddress) { $reviewer.mailAddress } else { $reviewer.uniqueName }
                            id = $reviewer.id
                            count = 0
                            identifiers = @()
                        }
                    }
                    
                    $requiredReviewers[$reviewerKey].count++
                    
                    # Add unique identifiers
                    $identifiers = $requiredReviewers[$reviewerKey].identifiers
                    if ($reviewer.id -and $reviewer.id -notin $identifiers) {
                        $identifiers += $reviewer.id
                    }
                    if ($reviewer.uniqueName -and $reviewer.uniqueName -notin $identifiers) {
                        $identifiers += $reviewer.uniqueName
                    }
                    if ($reviewer.mailAddress -and $reviewer.mailAddress -notin $identifiers) {
                        $identifiers += $reviewer.mailAddress
                    }
                }
            }
            
            if ($hasRequiredReviewers) {
                $prsWithRequiredReviewers++
            }
        }
    } catch {
        Write-Warning "    Failed to get details for PR #$($pr.pullRequestId): $($_.Exception.Message)"
    }
}

Write-Host "[OK] Analyzed $totalPRsAnalyzed PRs, found $prsWithRequiredReviewers with required reviewers" -ForegroundColor Green

# Sort reviewers by frequency (most common required reviewers first)
$sortedReviewers = $requiredReviewers.GetEnumerator() | Sort-Object { $_.Value.count } -Descending

Write-Host "`n[INFO] Required reviewers found:" -ForegroundColor Cyan
foreach ($reviewer in $sortedReviewers) {
    $name = $reviewer.Value.displayName
    $count = $reviewer.Value.count
    $email = $reviewer.Value.mailAddress
    Write-Host "  $name ($email) - Required in $count PRs" -ForegroundColor White
}

# Generate C# file
Write-Host "`n[INFO] Generating C# fallback file..." -ForegroundColor Yellow

# Build the HashSet initialization with all identifiers
$hashSetItems = @()
foreach ($reviewer in $sortedReviewers) {
    foreach ($identifier in $reviewer.Value.identifiers) {
        if ($identifier -and $identifier.Trim() -ne "") {
            $hashSetItems += "        `"$($identifier.Trim())`""
        }
    }
}

# Create the C# file content
$csharpContent = @"
// This file is auto-generated by scripts/collect-required-reviewers.ps1
// Do not edit manually - it will be overwritten
// Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
// Analyzed $totalPRsAnalyzed completed PRs from $Repository

using System.Collections.Generic;

namespace gapir
{
    public static class ApiReviewersFallback
    {
        /// <summary>
        /// Known API reviewers based on analysis of required reviewers in past pull requests
        /// This is used as a fallback when the group membership API is not accessible
        /// 
        /// Reviewers found (sorted by frequency):
$(foreach ($reviewer in $sortedReviewers) { "        /// - $($reviewer.Value.displayName) ($($reviewer.Value.mailAddress)) - Required in $($reviewer.Value.count) PRs`n" } -join "")        /// </summary>
        public static readonly HashSet<string> KnownApiReviewers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
$($hashSetItems -join ",`n")
        };
    }
}
"@

try {
    $csharpContent | Out-File -FilePath $OutputPath -Encoding UTF8 -Force
    Write-Host "[OK] Successfully created $OutputPath" -ForegroundColor Green
    
    if ($hashSetItems.Count -eq 0) {
        Write-Host "[WARN] No required reviewers found. The generated class is empty." -ForegroundColor Yellow
    } else {
        Write-Host "[INFO] Generated HashSet with $($hashSetItems.Count) identifiers for $($sortedReviewers.Count) reviewers" -ForegroundColor Green
    }
    
    Write-Host "`nNext steps:" -ForegroundColor Cyan
    Write-Host "   1. Review the generated file: $OutputPath" -ForegroundColor Gray
    Write-Host "   2. Rebuild your gapir application: dotnet build" -ForegroundColor Gray
    Write-Host "   3. Test with: dotnet run --project src/gapir" -ForegroundColor Gray
    
} catch {
    Write-Error "[ERROR] Failed to create C# file: $($_.Exception.Message)"
    exit 1
}

Write-Host "`nDone!" -ForegroundColor Green
