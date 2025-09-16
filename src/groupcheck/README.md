# GroupCheck

GroupCheck is a CLI tool to check Azure AD group membership using Microsoft Graph API.

## Features

- **Check group membership**: Verify if a user is a member of specific groups
- **List user groups**: Display all groups that a user belongs to  
- **Interactive authentication**: Uses Windows Hello/PIN/Biometrics when available
- **Token caching**: Remembers authentication for better performance

## Installation

```bash
dotnet tool install -g groupcheck
```

## Configuration

Before using GroupCheck, you need to:

1. Register an application in Azure AD with the following permissions:
   - `User.Read` (for basic user info)
   - `GroupMember.Read.All` (to read group memberships)
   - `Group.Read.All` (to read group information)

2. Update the `GroupCheckClientId` in `GroupAuthenticationService.cs` with your app registration ID

## Usage

### Check if a user is a member of specific groups

```bash
groupcheck check --user user@contoso.com --groups "Marketing Team" "Sales Team"
groupcheck check -u user@contoso.com -g "Marketing Team" "Sales Team"
```

### List all groups for a user

```bash
groupcheck list --user user@contoso.com
groupcheck list -u user@contoso.com
```

## Authentication

GroupCheck uses the same authentication mechanism as the gapir tool but with different app permissions:

- **Brokered authentication**: Uses Windows Hello, PIN, or biometrics when available
- **Device code flow**: Falls back to device code authentication if brokered auth fails
- **Token caching**: Stores tokens securely for subsequent runs

## Examples

```bash
# Check if john@contoso.com is in the "Developers" group
groupcheck check --user john@contoso.com --groups "Developers"

# Check multiple groups at once
groupcheck check --user jane@contoso.com --groups "Marketing" "Sales" "Admin"

# List all groups for a user
groupcheck list --user admin@contoso.com
```

## Output

The tool provides clear visual feedback:
- ✅ Member: User is a member of the group
- ❌ Not a member: User is not a member of the group
- ❌ Error: There was an issue checking the group

For the list command, it shows:
- Group display name
- Group description (if available)
- Group ID

## Requirements

- .NET 9.0 or later
- Windows (for brokered authentication)
- Azure AD app registration with appropriate permissions
