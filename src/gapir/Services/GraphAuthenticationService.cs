using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using System.Runtime.InteropServices;
using gapir.Models;

namespace gapir.Services;

/// <summary>
/// Provides authentication for Microsoft Graph API using the same app credentials as gapir
/// with MSAL brokered authentication and token caching.
/// </summary>
public class GraphAuthenticationService
{
    private const string Authority = "https://login.microsoftonline.com/common";
    private static readonly string[] Scopes = ["https://graph.microsoft.com/.default"];
    private const string TenantId = "common";

    private readonly string _cacheDir;
    private readonly AuthenticationConfiguration _authConfig;
    private IPublicClientApplication? _app;
    private GraphServiceClient? _graphClient;

    public GraphAuthenticationService(AuthenticationConfiguration authConfig)
    {
        _authConfig = authConfig;
        _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "gapir");
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Gets an authenticated GraphServiceClient instance
    /// </summary>
    /// <returns>Authenticated GraphServiceClient</returns>
    public async Task<GraphServiceClient> GetGraphClientAsync()
    {
        if (_graphClient != null)
        {
            return _graphClient;
        }

        var accessToken = await GetAccessTokenAsync();
        
        // Create a simple HTTP client with the authorization header
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        
        _graphClient = new GraphServiceClient(httpClient);
        return _graphClient;
    }

    /// <summary>
    /// Gets an access token for Microsoft Graph API
    /// </summary>
    /// <returns>Access token string</returns>
    public async Task<string> GetAccessTokenAsync()
    {
        await InitializeAppAsync();

        AuthenticationResult? result = null;

        try
        {
            // Try to get token silently first (from cache)
            var accounts = await _app!.GetAccountsAsync();
            if (accounts.Any())
            {
                Log.Information("Found cached account, attempting silent authentication...");
                try
                {
                    result = await _app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                        .ExecuteAsync();
                    Log.Success("Silent authentication successful using cached token!");
                }
                catch (MsalUiRequiredException)
                {
                    Log.Warning("Cached token expired, attempting interactive authentication...");
                    result = await PerformInteractiveAuthenticationAsync();
                }
            }
            else
            {
                Log.Information("No cached accounts found, starting interactive authentication...");
                result = await PerformInteractiveAuthenticationAsync();
            }
        }
        catch (MsalUiRequiredException)
        {
            Log.Information("Silent authentication failed, starting interactive authentication...");
            result = await PerformInteractiveAuthenticationAsync();
        }

        if (result?.AccessToken == null)
        {
            throw new InvalidOperationException("Failed to acquire access token");
        }

        return result.AccessToken;
    }

    private Task InitializeAppAsync()
    {
        if (_app != null) return Task.CompletedTask;

        // Create the MSAL client with brokered authentication support
        var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);
        _app = PublicClientApplicationBuilder
            .Create(_authConfig.ClientId) // Use same app ID as gapir
            .WithAuthority(Authority)
            .WithTenantId(TenantId)
            .WithRedirectUri($"ms-appx-web://microsoft.aad.brokerplugin/{_authConfig.ClientId}")
            .WithBroker(brokerOptions) // Enable brokered authentication (WAM)
            .Build();

        // Configure token cache for better persistence
        var cacheHelper = new TokenCacheHelper(_cacheDir);
        cacheHelper.EnableSerialization(_app.UserTokenCache);
        
        return Task.CompletedTask;
    }

    private async Task<AuthenticationResult> PerformInteractiveAuthenticationAsync()
    {
        Log.Information("Starting interactive authentication with Graph Explorer credentials...");
        
        var result = await _app!.AcquireTokenInteractive(Scopes)
            .WithPrompt(Microsoft.Identity.Client.Prompt.SelectAccount)
            .WithParentActivityOrWindow(GetParentWindow())
            .ExecuteAsync();

        Log.Success("Interactive authentication successful!");
        return result;
    }

    private static IntPtr GetParentWindow()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Try to get the console window handle
                return GetConsoleWindow();
            }
            catch
            {
                // If that fails, return IntPtr.Zero (no parent window)
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr GetConsoleWindow();
#pragma warning restore SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
}