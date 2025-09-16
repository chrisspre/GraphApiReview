using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Graph;
using gapir.Services;
using gapir;
using System.Runtime.InteropServices;

namespace groupcheck.Services;

/// <summary>
/// Provides authentication for Microsoft Graph API access with group-specific permissions
/// </summary>
public class GroupAuthenticationService
{
    private static readonly string Authority = "https://login.microsoftonline.com/common";
    private static readonly string[] Scopes = ["https://graph.microsoft.com/User.Read", "https://graph.microsoft.com/GroupMember.Read.All", "https://graph.microsoft.com/Group.Read.All"];

    // Using Graph Explorer App ID for group membership operations
    private const string GroupCheckClientId = "de8bc8b5-d9f9-48b1-a8ad-b748da725064";

    private GraphServiceClient? _graphClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConsoleLogger _logger;

    public GroupAuthenticationService(ConsoleLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets an authenticated GraphServiceClient
    /// </summary>
    public async Task<GraphServiceClient> GetGraphClientAsync()
    {
        if (_graphClient != null)
            return _graphClient;

        await _semaphore.WaitAsync();
        try
        {
            if (_graphClient == null)
            {
                _graphClient = await CreateGraphClientAsync();
            }
            return _graphClient;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<GraphServiceClient> CreateGraphClientAsync()
    {
        try
        {
            Log.Information("Authenticating with Microsoft Graph for group operations...");

            // Create cache directory for better token persistence
            var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "groupcheck");
            Directory.CreateDirectory(cacheDir);

            // Create the MSAL client with brokered authentication support
            var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows);
            var app = PublicClientApplicationBuilder
                .Create(GroupCheckClientId)
                .WithAuthority(Authority)
                .WithRedirectUri($"ms-appx-web://microsoft.aad.brokerplugin/{GroupCheckClientId}")
                .WithBroker(brokerOptions) // Enable brokered authentication (WAM)
                .Build();

            // Configure token cache for better persistence
            var cacheHelper = new TokenCacheHelper(cacheDir);
            cacheHelper.EnableSerialization(app.UserTokenCache);

            AuthenticationResult? result = null;

            try
            {
                // Try to get token silently first (from cache)
                var accounts = await app.GetAccountsAsync();
                if (accounts.Any())
                {
                    Log.Information("Found cached account, attempting silent authentication...");
                    try
                    {
                        result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                            .ExecuteAsync();
                        Log.Success("Silent authentication successful using cached token!");
                    }
                    catch (MsalUiRequiredException)
                    {
                        Log.Warning("Cached token expired, attempting interactive authentication...");
                        result = await PerformInteractiveAuthenticationAsync(app);
                    }
                }
                else
                {
                    Log.Information("No cached accounts found, starting interactive authentication...");
                    result = await PerformInteractiveAuthenticationAsync(app);
                }
            }
            catch (MsalUiRequiredException)
            {
                Log.Information("Silent authentication failed, starting interactive authentication...");
                result = await PerformInteractiveAuthenticationAsync(app);
            }

            if (result != null)
            {
                Log.Information("Creating GraphServiceClient with access token...");
                
                // Create TokenCredential from MSAL result
                var tokenCredential = new MsalTokenCredential(app, Scopes);
                
                // Create GraphServiceClient
                var graphClient = new GraphServiceClient(tokenCredential);
                
                // Test the connection
                Log.Information("Testing Graph connection...");
                var me = await graphClient.Me.GetAsync();
                Log.Success($"Connection test successful! Authenticated as: {me?.DisplayName} ({me?.UserPrincipalName})");

                return graphClient;
            }

            throw new InvalidOperationException("Authentication result was null");
        }
        catch (Exception ex)
        {
            Log.Error($"Authentication failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Log.Error($"Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    private async Task<AuthenticationResult> PerformInteractiveAuthenticationAsync(IPublicClientApplication app)
    {
        try
        {
            _logger.Information("🔐 Attempting brokered authentication (Windows Hello/PIN/Biometrics)...");

            // Try brokered authentication first (best UX)
            var result = await app.AcquireTokenInteractive(Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .WithParentActivityOrWindow(GetParentWindow())
                .ExecuteAsync();

            _logger.Success("Brokered authentication successful!");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Brokered authentication failed: {ex.Message}");
            _logger.Information("📱 Falling back to device code flow...");
            return await PerformDeviceCodeFlowAsync(app);
        }
    }

    private async Task<AuthenticationResult> PerformDeviceCodeFlowAsync(IPublicClientApplication app)
    {
        return await app.AcquireTokenWithDeviceCode(Scopes, deviceCodeResult =>
        {
            _logger.Information($"📱 Please visit: {deviceCodeResult.VerificationUrl}");
            _logger.Information($"🔢 And enter code: {deviceCodeResult.UserCode}");
            _logger.Information("⏳ Waiting for authentication...");
            return Task.FromResult(0);
        }).ExecuteAsync();
    }

    private static IntPtr GetParentWindow()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                return GetConsoleWindow();
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
        return IntPtr.Zero;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    public void Dispose()
    {
        _graphClient?.Dispose();
        _semaphore.Dispose();
    }
}
