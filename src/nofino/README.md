# NoFino - Microsoft Graph Extensions Manager

A command-line tool for managing Microsoft Graph user extensions, specifically designed to work with the `microsoft.teams.baffino` extension.

## Features

- 🔐 Modern authentication using MSAL with brokered authentication (Windows Hello/PIN/Biometrics)
- 📋 Get, create, update, list, and delete Microsoft Graph user extensions
- 🚀 Fast token caching for improved performance
- 🔄 Automatic fallback to device code flow if brokered authentication fails
- 📱 Automatic browser opening and clipboard integration for device code flow
- ⚙️ Configurable timeAllocation parameter

## Installation

### Prerequisites

- .NET 9.0 or later
- Windows (for brokered authentication support)

### Install as Global Tool

Run the installation script:

```powershell
.\install-tool.ps1
```

This will build and install `nofino` as a global .NET tool.

## Usage

### Commands

- `nofino get` - Get the microsoft.teams.baffino extension
- `nofino set [timeAllocation]` - Create/update the microsoft.teams.baffino extension  
- `nofino list` - List all user extensions
- `nofino delete` - Delete the microsoft.teams.baffino extension

### Examples

```bash
# Get the current baffino extension
nofino get

# Create or update the baffino extension with default timeAllocation (99)
nofino set

# Create or update the baffino extension with custom timeAllocation (75)
nofino set 75

# List all user extensions
nofino list

# Delete the baffino extension
nofino delete
```

## Extension Settings

When using `nofino set`, the following settings are applied with constant values:

```json
{
  "extensionName": "microsoft.teams.baffino",
  "onCallSkip": [""],
  "privateNotifications": ["reviewAssignment", "dailyPRReports", "voteReset"],
  "secondaryOnCallStrategy": "available",
  "timeAllocation": 99  // or the value you specify
}
```

**Note**: The Microsoft Graph API doesn't support PATCH operations for extensions, so the `set` command will delete the existing extension (if it exists) and create a new one with the specified settings.

## Admin Consent Issue Workaround

If you encounter "Admin Consent Required" errors, here are your options:

### Option 1: Use Azure CLI (Recommended)
Instead of using this tool, you can use Azure CLI commands directly:

```bash
# Login to Azure CLI
az login

# Get the baffino extension
az rest --method get --url "https://graph.microsoft.com/v1.0/me/extensions/microsoft.teams.baffino"

# Create/update the baffino extension with custom timeAllocation
az rest --method post --uri "https://graph.microsoft.com/v1.0/me/extensions" \
  --headers "Content-Type=application/json" \
  --body '{
    "@odata.type": "microsoft.graph.openTypeExtension",
    "extensionName": "microsoft.teams.baffino",
    "onCallSkip": [""],
    "privateNotifications": ["reviewAssignment", "dailyPRReports", "voteReset"],
    "secondaryOnCallStrategy": "available",
    "timeAllocation": 75
  }'
```

### Option 2: Use Personal Microsoft Account
Try using a personal Microsoft account (outlook.com, hotmail.com, etc.) instead of your work/school account.

### Option 3: Request Admin Approval
Ask your Azure AD administrator to approve the Microsoft Graph PowerShell application for your organization.

### Option 4: Use Baffino Teams Bot App ID (Current Implementation)
The tool is configured to use the Baffino Teams Bot App ID, which should have the necessary permissions. You need to:
1. Find the Baffino Teams Bot App ID (check Azure Portal > Azure Active Directory > App registrations)
2. Replace `YOUR_BAFFINO_APP_ID_HERE` in `GraphAuth.cs` with the actual app ID
3. The bot app should already have `User.ReadWrite` permissions for managing baffino extensions

### Option 5: Create Custom App Registration
Create your own app registration in Azure AD with the necessary permissions:
1. Go to Azure Portal > Azure Active Directory > App registrations
2. Create a new registration
3. Add Microsoft Graph API permissions: `User.ReadWrite`
4. Update the ClientId in the code

## Current Tool Status

🤖 **Using Baffino Teams Bot App ID**: This tool now uses the Baffino Teams Bot application ID, which should have the proper permissions to manage the `microsoft.teams.baffino` extension without requiring additional admin consent.

**Note**: You need to provide the actual Baffino Teams Bot App ID in the `GraphAuth.cs` file by replacing `YOUR_BAFFINO_APP_ID_HERE` with the real app ID.

## Equivalent Azure CLI Commands

This tool provides equivalent functionality to these Azure CLI commands:

```bash
# Get extension
az rest --method get --url "https://graph.microsoft.com/v1.0/me/extensions/microsoft.teams.baffino"

# Create extension with custom timeAllocation
az rest --method post --uri "https://graph.microsoft.com/v1.0/me/extensions" \
  --headers "Content-Type=application/json" \
  --body '{
    "@odata.type": "microsoft.graph.openTypeExtension",
    "extensionName": "microsoft.teams.baffino",
    "onCallSkip": [""],
    "privateNotifications": ["reviewAssignment", "dailyPRReports", "voteReset"],
    "secondaryOnCallStrategy": "available",
    "timeAllocation": 75
  }'
```

## Architecture

- **GraphAuth.cs**: Handles MSAL authentication with brokered support
- **TokenCacheHelper.cs**: Manages token persistence for improved performance
- **Program.cs**: Main application logic and command handling

## License

MIT License
