using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using groupcheck.Services;
using gapir.Services;

namespace groupcheck;

/// <summary>
/// GroupCheck - A CLI tool to check Azure AD group membership using Microsoft Graph API
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        // Create the root command
        var rootCommand = new RootCommand("GroupCheck - Check Azure AD group membership");

        // Add expand command for recursive group membership
        var expandCommand = new Command("expand", "Recursively expand group membership to find all user members");
        var groupExpandOption = new Option<string>("--group", "-g")
        {
            Description = "Group name or ID to expand recursively"
        };

        var outputFormatOption = new Option<string>("--format", "-f")
        {
            Description = "Output format: table (default), json, csv",
            DefaultValueFactory = _ => "table"
        };

        expandCommand.Options.Add(groupExpandOption);
        expandCommand.Options.Add(outputFormatOption);

        expandCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var group = parseResult.GetValue(groupExpandOption);
            var format = parseResult.GetValue(outputFormatOption);
            await HandleExpandCommand(group!, format!);
            return 0;
        });

        // Add check command
        var checkCommand = new Command("check", "Check if a user is a member of specified groups");
        var userOption = new Option<string>("--user", "-u")
        {
            Description = "User email or UPN to check"
        };

        var groupOption = new Option<string[]>("--groups", "-g")
        {
            Description = "Group names or IDs to check membership against",
            AllowMultipleArgumentsPerToken = true
        };

        checkCommand.Options.Add(userOption);
        checkCommand.Options.Add(groupOption);

        checkCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var user = parseResult.GetValue(userOption);
            var groups = parseResult.GetValue(groupOption);
            await HandleCheckCommand(user!, groups!);
            return 0;
        });

        // Add list command for user's groups
        var listCommand = new Command("list", "List all groups for a user");
        var listUserOption = new Option<string>("--user", "-u")
        {
            Description = "User email or UPN to list groups for"
        };

        listCommand.Options.Add(listUserOption);
        listCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            var user = parseResult.GetValue(listUserOption);
            await HandleListCommand(user!);
            return 0;
        });

        rootCommand.Subcommands.Add(expandCommand);
        rootCommand.Subcommands.Add(checkCommand);
        rootCommand.Subcommands.Add(listCommand);

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }

    private static async Task HandleExpandCommand(string group, string format)
    {
        try
        {
            var host = CreateHostBuilder().Build();
            var groupService = host.Services.GetRequiredService<GroupMembershipService>();
            
            Console.WriteLine($"🔍 Recursively expanding group: {group}");
            Console.WriteLine();

            var users = await groupService.GetAllUsersInGroupRecursiveAsync(group);
            
            if (users.Any())
            {
                Console.WriteLine($"Found {users.Count} users:");
                Console.WriteLine();

                switch (format.ToLower())
                {
                    case "json":
                        var jsonUsers = users.Select(u => new { 
                            DisplayName = u.DisplayName, 
                            UserPrincipalName = u.UserPrincipalName, 
                            Id = u.Id 
                        });
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(jsonUsers, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        break;
                    
                    case "csv":
                        Console.WriteLine("DisplayName,UserPrincipalName,Id");
                        foreach (var user in users.OrderBy(u => u.DisplayName))
                        {
                            Console.WriteLine($"\"{user.DisplayName}\",{user.UserPrincipalName},{user.Id}");
                        }
                        break;
                    
                    default: // table format
                        foreach (var user in users.OrderBy(u => u.DisplayName))
                        {
                            Console.WriteLine($"• {user.DisplayName}");
                            Console.WriteLine($"  Email: {user.UserPrincipalName}");
                            Console.WriteLine($"  ID: {user.Id}");
                            Console.WriteLine();
                        }
                        break;
                }
            }
            else
            {
                Console.WriteLine("No users found in this group.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task HandleCheckCommand(string user, string[] groups)
    {
        try
        {
            var host = CreateHostBuilder().Build();
            var groupService = host.Services.GetRequiredService<GroupMembershipService>();
            
            Console.WriteLine($"🔍 Checking group membership for: {user}");
            Console.WriteLine();

            foreach (var group in groups)
            {
                try
                {
                    var isMember = await groupService.IsUserMemberOfGroupAsync(user, group);
                    var status = isMember ? "✅ Member" : "❌ Not a member";
                    Console.WriteLine($"{status}: {group}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error checking {group}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task HandleListCommand(string user)
    {
        try
        {
            var host = CreateHostBuilder().Build();
            var groupService = host.Services.GetRequiredService<GroupMembershipService>();
            
            Console.WriteLine($"📋 Listing groups for: {user}");
            Console.WriteLine();

            var groups = await groupService.GetUserGroupsAsync(user);
            
            if (groups.Any())
            {
                Console.WriteLine($"Found {groups.Count} groups:");
                Console.WriteLine();
                
                foreach (var group in groups.OrderBy(g => g.DisplayName))
                {
                    Console.WriteLine($"• {group.DisplayName}");
                    if (!string.IsNullOrEmpty(group.Description))
                    {
                        Console.WriteLine($"  {group.Description}");
                    }
                    Console.WriteLine($"  ID: {group.Id}");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No groups found for this user.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return new HostBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<ConsoleLogger>();
                services.AddSingleton<GroupAuthenticationService>();
                services.AddSingleton<GroupMembershipService>();
            });
    }
}
