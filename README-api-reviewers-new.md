# API Reviewers Configuration

This document explains how to set up the API reviewers list for the gapir tool to populate the "Ratio" column in the pull request table.

## Overview

The gapir tool shows a "Ratio" column that displays the approval ratio from Microsoft Graph API reviewers. This uses the "[TEAM FOUNDATION]\Microsoft Graph API reviewers" group from Azure DevOps.

## Setup Instructions

### Prerequisites

1. **Azure CLI**: Install from [/install-azure-cli](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
2. **Azure DevOps Extension**: Install with `az extension add --name azure-devops`
3. **Authentication**: Log in with `az login`
4. **DevOps Configuration**: Set defaults with:
   ```powershell
   az devops configure --defaults organization=https://dev.azure.com/msazure project=GraphAPI
   ```

### Generate API Reviewers List

1. Run the PowerShell script:
   ```powershell
   .\scripts\update-api-reviewers.ps1
   ```

2. This script will:
   - Find the Microsoft Graph API reviewers group in Azure DevOps
   - Fetch all group members and their identifiers
   - Generate `src/gapir/ApiReviewersFallback.cs` with a static HashSet
   - Create compile-time inclusion of reviewer data

3. If successful, rebuild gapir:
   ```powershell
   dotnet build
   ```

### Generated File Structure

The script creates `src/gapir/ApiReviewersFallback.cs`:

```csharp
namespace gapir
{
    public static class ApiReviewersFallback
    {
        public static readonly HashSet<string> KnownApiReviewers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "user1@microsoft.com",
            "aad.descriptor.id.here",
            "unique.name@microsoft.com"
            // ... more identifiers
        };
    }
}
```

## How It Works

1. **Primary Method**: gapir tries to query the Azure DevOps group API directly
2. **Fallback Method**: If the API call fails (due to permissions), it uses the static list from `ApiReviewersFallback.KnownApiReviewers`
3. **Ratio Calculation**: Counts how many API reviewers have approved vs. total API reviewers assigned to the PR

## Troubleshooting

### "0/0" Ratios Displayed
- The group API call failed and no static reviewers match current PR reviewers
- Run the script to populate the static list: `.\scripts\update-api-reviewers.ps1`
- Check if you have permissions to read the group membership

### Script Fails to Find Group
- Verify you're connected to the correct Azure DevOps organization
- Check if the group name "[TEAM FOUNDATION]\Microsoft Graph API reviewers" exists
- Ensure you have permissions to read security groups

### Permission Issues
- The group membership API may require special permissions
- The static fallback approach works around this limitation
- Consider manually updating `ApiReviewersFallback.cs` if needed

## Manual Updates

If the script doesn't work, you can manually edit `src/gapir/ApiReviewersFallback.cs`:

```csharp
public static readonly HashSet<string> KnownApiReviewers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "reviewer1@microsoft.com",
    "reviewer2@microsoft.com",
    "azure-devops-descriptor-id"
};
```

Include any identifier that Azure DevOps uses for the reviewer (email, unique name, or descriptor).

## Deployment Benefits

- **No External Dependencies**: Reviewer data is compiled into the application
- **Works with Global Tools**: No configuration files needed for `dotnet tool install`
- **Offline Operation**: Fallback works without network access to Azure DevOps
- **Simple Updates**: Re-run script and rebuild to refresh reviewer list

## Verification

After updating, run gapir to verify it's working:

```powershell
dotnet run --project src/gapir
```

You should see:
- `✓ Loaded X API reviewers` (where X > 0)
- Ratio column showing actual ratios like "1/2", "2/2" instead of "0/0"
