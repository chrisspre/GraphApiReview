# API Reviewers Configuration

## Automatic Method (Recommended)

If you have Azure CLI installed, you can automatically populate the API reviewers configuration:

1. Install Azure CLI if not already installed:
   - Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli

2. Install Azure DevOps extension:
   ```bash
   az extension add --name azure-devops
   ```

3. Login to Azure:
   ```bash
   az login
   ```

4. Run the update script:
   ```powershell
   .\scripts\update-api-reviewers.ps1
   ```

## Manual Method

If Azure CLI is not available or doesn't have sufficient permissions, you can manually populate the config file:

1. Edit `config/api-reviewers.json`

2. Add the API reviewers to the `apiReviewers` array. For each reviewer, include:
   ```json
   {
     "id": "azure-devops-user-id-guid",
     "displayName": "Reviewer Display Name", 
     "mailAddress": "reviewer@microsoft.com",
     "uniqueName": "reviewer@microsoft.com"
   }
   ```

   **Note**: You need at least one of `id`, `mailAddress`, or `uniqueName` for each reviewer. The tool will use any of these to match against PR reviewers.

3. Example complete configuration:
   ```json
   {
     "lastUpdated": "2025-08-21T12:00:00Z",
     "organization": "https://dev.azure.com/One",
     "project": "AD-AggregatorService-Workloads", 
     "groupName": "[TEAM FOUNDATION]\\Microsoft Graph API reviewers",
     "groupId": "",
     "source": "manual",
     "apiReviewers": [
       {
         "id": "12345678-1234-1234-1234-123456789abc",
         "displayName": "John Smith",
         "mailAddress": "john.smith@microsoft.com",
         "uniqueName": "john.smith@microsoft.com"
       },
       {
         "id": "87654321-4321-4321-4321-cba987654321", 
         "displayName": "Jane Doe",
         "mailAddress": "jane.doe@microsoft.com",
         "uniqueName": "jane.doe@microsoft.com"
       }
     ]
   }
   ```

## Verification

After updating the configuration, run gapir to verify it's working:

```bash
cd src/gapir
dotnet run
```

You should see:
- `✓ Loaded X API reviewers` (where X > 0)
- Ratio column showing actual ratios like "1/2", "2/2" instead of "?/?" or "0/0"

## Troubleshooting

- **"Config file not found"**: Ensure `config/api-reviewers.json` exists in the repository root
- **"Loaded 0 API reviewers"**: The config file exists but the `apiReviewers` array is empty
- **Still showing "0/0"**: The reviewers in the config don't match the reviewers assigned to the PRs (check IDs/emails)
- **"?/?"**: The config file couldn't be loaded or parsed (check JSON syntax)
