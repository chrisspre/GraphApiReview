using Microsoft.Extensions.Hosting.WindowsServices;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure the app to listen on port 80
        builder.WebHost.UseUrls("http://localhost:80");
        
        // Add Windows Service support
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Kurz URL Redirect Service";
        });

        var app = builder.Build();

        // Route mappings - easy to extend with new route types
        var routeMappings = new Dictionary<string, string>
        {
            ["pr"] = "https://dev.azure.com/msazure/One/_git/AD-AggregatorService-Workloads/pullrequest/"
        };

        // Handle dynamic routes based on the dictionary
        foreach (var route in routeMappings)
        {
            var routeKey = route.Key;
            var baseUrl = route.Value;
            
            app.MapGet($"/{routeKey}/{{*path}}", (string? path, ILogger<Program> logger) =>
            {
                logger.LogInformation("{RouteType} redirect processed for path: {Path}", routeKey, path ?? "empty");

                string redirectUrl = string.IsNullOrEmpty(path) ? baseUrl : baseUrl + path;
                
                logger.LogInformation("Redirecting to: {RedirectUrl}", redirectUrl);

                return Results.Redirect(redirectUrl, permanent: true);
            });
        }

        // Handle favicon.ico requests
        app.MapGet("/favicon.ico", (ILogger<Program> logger) =>
        {
            // logger.LogInformation("Favicon request received");            
            return Results.NotFound();
        });

        // Handle all other paths - show helpful message
        app.MapGet("/{*path}", (string? path, ILogger<Program> logger) =>
        {
            logger.LogInformation("Unsupported path accessed: {Path}", path ?? "root");

            var supportedRoutes = string.Join(", ", routeMappings.Keys.Select(k => $"/{k}/*"));
            return Results.Text($"Only {supportedRoutes} routes are supported. Example: /pr/12041652", "text/plain");
        });

        // Handle the root path specifically
        app.MapGet("/", (ILogger<Program> logger) =>
        {
            logger.LogInformation("Root path accessed");

            var supportedRoutes = string.Join(", ", routeMappings.Keys.Select(k => $"/{k}/*"));
            return Results.Text($"Only {supportedRoutes} routes are supported. Example: /pr/12041652", "text/plain");
        });

        app.Run();
    }
}