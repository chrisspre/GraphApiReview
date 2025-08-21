#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates the API reviewers fallback configuration for gapir tool.

.DESCRIPTION
    This script uses Azure CLI to fetch members of the Microsoft Graph API reviewers group
    and generates a C# file with a static HashSet that can be copied into the gapir project
    as a fallback when Azure DevOps Identity API calls fail due to permission issues.

.PARAMETER OutputPath
    Path where the C# file will be created. Defaults to '../src/gapir/ApiReviewersFallback.cs'

.EXAMPLE
    .\update-api-reviewers.ps1
    
.EXAMPLE
    .\update-api-reviewers.ps1 -OutputPath "C:\custom\path\ApiReviewers.cs"
#>

param(
    [string]$OutputPath = "../src/gapir/ApiReviewersFallback.cs"
)

# Configuration
$Organization = "https://dev.azure.com/One"
$Project = "AD-AggregatorService-Workloads"
$GroupName = "[TEAM FOUNDATION]\Microsoft Graph API reviewers"

Write-Host "🔧 Azure DevOps API Reviewers Configuration Updater" -ForegroundColor Cyan
Write-Host "=" * 60

# Check if Azure CLI is installed
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "✅ Azure CLI found: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Error "❌ Azure CLI not found. Please install Azure CLI first: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Check if Azure DevOps extension is installed
try {
    $extensions = az extension list --output json | ConvertFrom-Json
    $devopsExtension = $extensions | Where-Object { $_.name -eq "azure-devops" }
    
    if (-not $devopsExtension) {
        Write-Host "📦 Installing Azure DevOps extension..." -ForegroundColor Yellow
        az extension add --name azure-devops
        Write-Host "✅ Azure DevOps extension installed" -ForegroundColor Green
    } else {
        Write-Host "✅ Azure DevOps extension found: $($devopsExtension.version)" -ForegroundColor Green
    }
} catch {
    Write-Error "❌ Failed to check/install Azure DevOps extension"
    exit 1
}

# Configure Azure DevOps defaults
Write-Host "🔧 Configuring Azure DevOps defaults..." -ForegroundColor Yellow
try {
    az devops configure --defaults organization=$Organization project=$Project
    Write-Host "✅ Azure DevOps configured for organization: $Organization" -ForegroundColor Green
    Write-Host "✅ Default project set to: $Project" -ForegroundColor Green
} catch {
    Write-Error "❌ Failed to configure Azure DevOps defaults"
    exit 1
}

# Check authentication
Write-Host "🔐 Checking Azure DevOps authentication..." -ForegroundColor Yellow
try {
    $user = az devops user show --user me --output json | ConvertFrom-Json
    Write-Host "✅ Authenticated as: $($user.displayName) ($($user.mailAddress))" -ForegroundColor Green
} catch {
    Write-Error "❌ Not authenticated with Azure DevOps. Please run 'az login' first."
    exit 1
}

# List all security groups to find the target group
Write-Host "🔍 Searching for API reviewers group..." -ForegroundColor Yellow
try {
    $groups = az devops security group list --output json | ConvertFrom-Json
    $apiGroup = $groups.graphGroups | Where-Object { $_.displayName -eq $GroupName }
    
    if (-not $apiGroup) {
        Write-Warning "⚠️ Group '$GroupName' not found."
        Write-Host "Available groups:" -ForegroundColor Cyan
        $groups.graphGroups | Where-Object { $_.displayName -like "*API*" -or $_.displayName -like "*review*" } | 
            ForEach-Object { Write-Host "  - $($_.displayName)" -ForegroundColor Gray }
        exit 1
    }
    
    Write-Host "✅ Found group: $($apiGroup.displayName)" -ForegroundColor Green
    Write-Host "   Group ID: $($apiGroup.descriptor)" -ForegroundColor Gray
} catch {
    Write-Error "❌ Failed to list security groups: $($_.Exception.Message)"
    exit 1
}

# Get group members
Write-Host "👥 Fetching group members..." -ForegroundColor Yellow
try {
    $members = az devops security group membership list --id $apiGroup.descriptor --output json | ConvertFrom-Json
    
    if (-not $members -or $members.Count -eq 0) {
        Write-Warning "⚠️ No members found in group or insufficient permissions to read group membership"
        Write-Host "This may be due to permission restrictions on nested groups." -ForegroundColor Yellow
        $reviewers = @()
    } else {
        # Extract user information
        $reviewers = @()
        foreach ($member in $members) {
            if ($member.subjectKind -eq "user") {
                # Collect all possible identifiers for each user
                $identifiers = @()
                if ($member.descriptor) { $identifiers += "`"$($member.descriptor)`"" }
                if ($member.mailAddress) { $identifiers += "`"$($member.mailAddress)`"" }
                if ($member.uniqueName) { $identifiers += "`"$($member.uniqueName)`"" }
                
                $reviewers += @{
                    displayName = $member.displayName
                    identifiers = $identifiers
                }
            }
        }
        
        Write-Host "✅ Found $($reviewers.Count) API reviewers" -ForegroundColor Green
        foreach ($reviewer in $reviewers) {
            Write-Host "  - $($reviewer.displayName)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Warning "⚠️ Failed to fetch group members: $($_.Exception.Message)"
    Write-Host "Creating empty fallback class - you may need to manually populate it." -ForegroundColor Yellow
    $reviewers = @()
}

# Generate C# file
Write-Host "📄 Creating C# file..." -ForegroundColor Yellow

# Build the HashSet initialization
$hashSetItems = @()
foreach ($reviewer in $reviewers) {
    foreach ($identifier in $reviewer.identifiers) {
        $hashSetItems += "        $identifier"
    }
}

# Create the C# file content
$csharpContent = @"
// This file is auto-generated by scripts/update-api-reviewers.ps1
// Do not edit manually - it will be overwritten
// Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

using System.Collections.Generic;

namespace gapir
{
    public static class ApiReviewersFallback
    {
        /// <summary>
        /// Known API reviewers based on the Microsoft Graph API reviewers group
        /// This is used as a fallback when the group membership API is not accessible
        /// </summary>
        public static readonly HashSet<string> KnownApiReviewers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
$($hashSetItems -join ",`n")
        };
    }
}
"@

try {
    $csharpContent | Out-File -FilePath $OutputPath -Encoding UTF8 -Force
    Write-Host "✅ Successfully created $OutputPath" -ForegroundColor Green
    
    if ($reviewers.Count -eq 0) {
        Write-Host "⚠️ Note: The generated class contains no reviewers." -ForegroundColor Yellow
        Write-Host "   You may need to manually add reviewers or check group permissions." -ForegroundColor Yellow
    } else {
        Write-Host "📊 Generated HashSet with $($hashSetItems.Count) identifiers for $($reviewers.Count) reviewers" -ForegroundColor Green
    }
    
    Write-Host "`n🚀 Next steps:" -ForegroundColor Cyan
    Write-Host "   1. Copy the generated file to your gapir project" -ForegroundColor Gray
    Write-Host "   2. Update your code to use ApiReviewersFallback.KnownApiReviewers" -ForegroundColor Gray
    Write-Host "   3. Rebuild and test your application" -ForegroundColor Gray
    
} catch {
    Write-Error "❌ Failed to create C# file: $($_.Exception.Message)"
    exit 1
}

Write-Host "`n🚀 Done!" -ForegroundColor Green
