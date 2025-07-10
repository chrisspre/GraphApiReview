using nofino;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Newtonsoft.Json;
using System.Text;

namespace nofino;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 NoFino - Microsoft Graph Extensions Manager");
        Console.WriteLine("===============================================");
        
        if (args.Length == 0)
        {
            ShowUsage();
            return;
        }

        // Authenticate with Microsoft Graph
        var graphClient = await GraphAuth.AuthenticateAsync();
        if (graphClient == null)
        {
            Console.WriteLine("❌ Authentication failed. Exiting...");
            return;
        }

        // Verify user permissions
        bool hasPermissions = await VerifyUserPermissions(graphClient);
        if (!hasPermissions)
        {
            Console.WriteLine("❌ Insufficient permissions. Exiting...");
            return;
        }

        var command = args[0].ToLower();
        
        try
        {
            switch (command)
            {
                case "get":
                    await GetBaffinoExtension(graphClient);
                    break;
                case "set":
                case "create":
                    int timeAllocation = 99; // default value
                    if (args.Length > 1 && int.TryParse(args[1], out int parsedValue))
                    {
                        timeAllocation = parsedValue;
                    }
                    await SetBaffinoExtension(graphClient, timeAllocation);
                    break;
                case "list":
                    await ListAllExtensions(graphClient);
                    break;
                // case "delete":
                //     await DeleteBaffinoExtension(graphClient);
                //     break;
                default:
                    Console.WriteLine($"❌ Unknown command: {command}");
                    ShowUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error executing command: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage: nofino <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  get                    - Get the microsoft.teams.baffino extension");
        Console.WriteLine("  set [timeAllocation]   - Create/update the microsoft.teams.baffino extension");
        Console.WriteLine("  list                   - List all user extensions");
        // Console.WriteLine("  delete                 - Delete the microsoft.teams.baffino extension");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  nofino get");
        Console.WriteLine("  nofino set             # Uses default timeAllocation of 99");
        Console.WriteLine("  nofino set 75          # Sets timeAllocation to 75");
        Console.WriteLine("  nofino list");
        // Console.WriteLine("  nofino delete");
    }

    static async Task GetBaffinoExtension(GraphServiceClient graphClient)
    {
        Console.WriteLine("📋 Getting microsoft.teams.baffino extension...");
        
        try
        {
            var extension = await graphClient.Me.Extensions["microsoft.teams.baffino"].GetAsync();
            
            if (extension != null)
            {
                Console.WriteLine("✅ Extension found:");
                Console.WriteLine($"ID: {extension.Id}");
                
                // Display additional properties if they exist
                if (extension.AdditionalData != null && extension.AdditionalData.Count > 0)
                {
                    Console.WriteLine("Properties:");
                    foreach (var prop in extension.AdditionalData.Where(p => ! p.Key.StartsWith("@odata.")))
                    {
                        var valueJson = JsonConvert.SerializeObject(prop.Value, Formatting.Indented);
                        Console.WriteLine($"  {prop.Key}: {valueJson}");
                    }
                }
            }
            else
            {
                Console.WriteLine("❌ Extension not found");
            }
        }
        catch (ODataError ex) when (ex.Error?.Code == "NotFound")
        {
            Console.WriteLine("❌ Extension 'microsoft.teams.baffino' not found");
        }
        catch (ODataError ex) when (ex.Error?.Code == "Forbidden" || ex.Error?.Code == "AccessDenied")
        {
            Console.WriteLine("❌ Access Denied: The application doesn't have permission to read user extensions.");
            Console.WriteLine($"   Error details: {ex.Error?.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to get extension: {ex.Message}");
        }
    }

    static async Task SetBaffinoExtension(GraphServiceClient graphClient, int timeAllocation = 99)
    {
        Console.WriteLine($"📝 Creating/updating microsoft.teams.baffino extension with timeAllocation: {timeAllocation}...");
        
        var extension = new OpenTypeExtension
        {
            ExtensionName = "microsoft.teams.baffino",
            AdditionalData = new Dictionary<string, object>
            {
                { "onCallSkip", new[] { "" } },
                { "privateNotifications", new[] { "reviewAssignment", "dailyPRReports", "voteReset" } },
                { "secondaryOnCallStrategy", "available" },
                { "timeAllocation", timeAllocation }
            }
        };

        try
        {
            // Since the API doesn't support PATCH, we need to DELETE first if it exists, then CREATE
            try
            {
                await graphClient.Me.Extensions["microsoft.teams.baffino"].DeleteAsync();
                Console.WriteLine("🗑️  Deleted existing extension");
            }
            catch (ODataError ex) when (ex.Error?.Code == "NotFound")
            {
                // Extension doesn't exist, which is fine - we'll create it
            }

            // Create the extension
            var result = await graphClient.Me.Extensions.PostAsync(extension);
            
            Console.WriteLine("✅ Extension created successfully!");
            Console.WriteLine($"ID: {result?.Id}");
            
            if (result is OpenTypeExtension openTypeResult)
            {
                Console.WriteLine($"Extension Name: {openTypeResult.ExtensionName}");
                
                if (openTypeResult.AdditionalData != null && openTypeResult.AdditionalData.Count > 0)
                {
                    Console.WriteLine("Properties:");
                    foreach (var prop in openTypeResult.AdditionalData)
                    {
                        var valueJson = JsonConvert.SerializeObject(prop.Value, Formatting.Indented);
                        Console.WriteLine($"  {prop.Key}: {valueJson}");
                    }
                }
            }
        }
        catch (ODataError ex) when (ex.Error?.Code == "Forbidden" || ex.Error?.Code == "AccessDenied")
        {
            Console.WriteLine("❌ Access Denied: The application doesn't have permission to manage user extensions.");
            Console.WriteLine("💡 This might be due to:");
            Console.WriteLine("   - Missing User.ReadWrite permission");
            Console.WriteLine("   - Admin consent required for the application");
            Console.WriteLine("   - Organization policy restrictions");
            Console.WriteLine($"   - Error details: {ex.Error?.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to create extension: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner exception: {ex.InnerException.Message}");
            }
        }
    }

    static async Task ListAllExtensions(GraphServiceClient graphClient)
    {
        Console.WriteLine("📋 Listing all user extensions...");
        
        try
        {
            var extensions = await graphClient.Me.Extensions.GetAsync();
            
            if (extensions?.Value != null && extensions.Value.Count > 0)
            {
                Console.WriteLine($"✅ Found {extensions.Value.Count} extension(s):");
                Console.WriteLine();
                
                foreach (var extension in extensions.Value)
                {
                    Console.WriteLine($"🔹 Extension: {extension?.Id}");
                    if (extension is OpenTypeExtension openTypeExt)
                    {
                        Console.WriteLine($"   Extension Name: {openTypeExt.ExtensionName}");
                        
                        if (openTypeExt.AdditionalData != null && openTypeExt.AdditionalData.Count > 0)
                        {
                            Console.WriteLine("   Properties:");
                            foreach (var prop in openTypeExt.AdditionalData)
                            {
                                var valueJson = JsonConvert.SerializeObject(prop.Value, Formatting.None);
                                Console.WriteLine($"     {prop.Key}: {valueJson}");
                            }
                        }
                    }
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("❌ No extensions found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Failed to list extensions: {ex.Message}");
        }
    }

    static async Task<bool> VerifyUserPermissions(GraphServiceClient graphClient)
    {
        try
        {
            Console.WriteLine("🔍 Verifying user permissions...");
            
            // Test basic user read access
            var me = await graphClient.Me.GetAsync();
            Console.WriteLine($"✅ Basic user access: {me?.DisplayName}");
            
            // Test extension read access
            var extensions = await graphClient.Me.Extensions.GetAsync();
            Console.WriteLine($"✅ Extension read access: Found {extensions?.Value?.Count ?? 0} extensions");
            
            return true;
        }
        catch (ODataError ex) when (ex.Error?.Code == "Forbidden" || ex.Error?.Code == "AccessDenied")
        {
            Console.WriteLine("❌ Permission verification failed:");
            Console.WriteLine($"   Error: {ex.Error?.Code} - {ex.Error?.Message}");
            Console.WriteLine("💡 Possible solutions:");
            Console.WriteLine("   1. Run as administrator");
            Console.WriteLine("   2. Contact your Azure AD admin for app permissions");
            Console.WriteLine("   3. Try using a different Azure tenant");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Permission verification failed: {ex.Message}");
            return false;
        }
    }

    // static async Task DeleteBaffinoExtension(GraphServiceClient graphClient)
    // {
    //     Console.WriteLine("🗑️  Deleting microsoft.teams.baffino extension...");

    //     try
    //     {
    //         await graphClient.Me.Extensions["com.microsoft.teams.baffino"].DeleteAsync();
    //         Console.WriteLine("✅ Extension deleted successfully!");
    //     }
    //     catch (ODataError ex) when (ex.Error?.Code == "NotFound")
    //     {
    //         Console.WriteLine("❌ Extension 'microsoft.teams.baffino' not found");
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"❌ Failed to delete extension: {ex.Message}");
    //     }
    // }
}
