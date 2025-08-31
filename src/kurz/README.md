# Kurz - URL Redirect Service

A lightweig## Windows Service Installation

For production use, install as a Windows service that starts automatically:

**Run PowerShell as Administrator:**
```powershell
.\Install-Service.ps1
```

This will:
- Build the project in Release mode
- Copy service files to `C:\Program Files\Kurz\`
- Add g to your hosts file automatically
- Install and start the Windows service
- Configure auto-start after OS restart

**Service management:**
```powershell
.\Install-Service.ps1 status      # Check service status
.\Install-Service.ps1 uninstall   # Remove the service and files
```

**Installation Details:**
- **Service Location**: `C:\Program Files\Kurz\`
- **Service Name**: `KurzUrlRedirectService`
- **Startup Type**: Automatic
- **User Account**: Local System minimal API that provides URL redirection services with support for custom short domains and Base62 encoding.

## Features

- Lightweight ASP.NET Core minimal API
- Base62 encoding/decoding for compact PR URLs
- Automatic detection of Base62 vs decimal PR IDs
- Extensible route mapping via Dictionary configuration
- Windows Service support with auto-start capability
- 308 Permanent Redirect for better SEO and caching
- Custom domain support (e.g., g)
- Clean error handling for unsupported paths
- Favicon handling for better browser experience
- Automatic hosts file management via PowerShell script

## Usage

The service listens on `http://localhost:80` and supports configurable routes defined in the route mapping dictionary.

**Current configured routes:**
- `http://localhost/pr/OwAc` → redirects to pull request URL (Base62 encoded ID)
- `http://localhost/pr/12041652` → redirects to pull request URL (decimal ID)
- `http://localhost/pr/` → redirects to the base pull request URL
- `http://localhost/anything-else` → shows "Only /pr/* routes are supported" message

## Base62 Encoding

The service automatically detects whether a PR ID is in Base62 or decimal format:
- **Base62**: Uses characters 0-9, a-z, A-Z for compact URLs (e.g., `OwAc` = 12041652)
- **Decimal**: Standard numeric format (e.g., `12041652`)

Both formats are accepted and will redirect to the same pull request.

## Running Locally

1. Start the application (requires Administrator privileges for port 80):
   ```
   dotnet run
   ```

2. The service will be available at: `http://localhost:80`

## Windows Service Installation

For production use, install as a Windows service that starts automatically:

**Run PowerShell as Administrator:**
```powershell
.\Install-Service.ps1
```

This will:
- Add g.io to your hosts file automatically
- Build the project
- Install and start the Windows service
- Configure auto-start after OS restart

**Service management:**
```powershell
.\Install-Service.ps1 status      # Check service status
.\Install-Service.ps1 uninstall   # Remove the service
```

## Custom Domain Setup

The installation script automatically adds the custom domain, but you can also do it manually:

**Run as Administrator:**
```powershell
Add-Content -Path "$env:SystemRoot\System32\drivers\etc\hosts" -Value "127.0.0.1 g" -Force
```

Then access via: `http://g/pr/{id}` (where {id} can be Base62 or decimal)

## Configuration

Routes are configured via a Dictionary in `Program.cs` for easy extensibility:

```csharp
var routeMappings = new Dictionary<string, string>
{
    ["pr"] = "https://dev.azure.com/msazure/One/_git/AD-AggregatorService-Workloads/pullrequest/"
    // Add more routes here as needed
};
```

**Adding new route types:**
```csharp
var routeMappings = new Dictionary<string, string>
{
    ["pr"] = "https://dev.azure.com/msazure/One/_git/AD-AggregatorService-Workloads/pullrequest/",
    ["jira"] = "https://yourcompany.atlassian.net/browse/",  // Example: /jira/PROJ-123
    ["docs"] = "https://docs.yourcompany.com/"              // Example: /docs/api-guide
};
```

## Examples

- `http://g/pr/OwAc` → redirects to pull request URL with ID 12041652 (Base62 decoded)
- `http://g/pr/12041652` → redirects to pull request URL with ID 12041652 (decimal)
- `http://g/pr/` → redirects to the base pull request URL  
- `http://g/` → "Only /pr/* routes are supported. Example: /pr/12041652"
