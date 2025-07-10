namespace gapir;

using Microsoft.Identity.Client;

public class TokenCacheHelper(string cacheDir)
{
    private readonly string _cacheFilePath = Path.Combine(cacheDir, "msalcache.bin");

    public void EnableSerialization(ITokenCache tokenCache)
    {
        tokenCache.SetBeforeAccess(BeforeAccessNotification);
        tokenCache.SetAfterAccess(AfterAccessNotification);
    }

    private void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        if (File.Exists(_cacheFilePath))
        {
            try
            {
                var data = File.ReadAllBytes(_cacheFilePath);
                args.TokenCache.DeserializeMsalV3(data);
            }
            catch (Exception ex)
            {
                Log.Warn($"Error reading token cache: {ex.Message}");
            }
        }
    }

    private void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        if (args.HasStateChanged)
        {
            try
            {
                var data = args.TokenCache.SerializeMsalV3();
                File.WriteAllBytes(_cacheFilePath, data);
                Log.Success("Token cache updated for faster future authentication!");
            }
            catch (Exception ex)
            {
                Log.Warn($"Error writing token cache: {ex.Message}");
            }
        }
    }
}