using Azure.Core;
using Microsoft.Identity.Client;

namespace groupcheck.Services;

/// <summary>
/// Token credential implementation that wraps MSAL for use with Microsoft Graph SDK
/// </summary>
public class MsalTokenCredential : TokenCredential
{
    private readonly IPublicClientApplication _app;
    private readonly string[] _scopes;

    public MsalTokenCredential(IPublicClientApplication app, string[] scopes)
    {
        _app = app;
        _scopes = scopes;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        try
        {
            var accounts = await _app.GetAccountsAsync();
            var result = await _app.AcquireTokenSilent(_scopes, accounts.FirstOrDefault())
                .ExecuteAsync(cancellationToken);

            return new AccessToken(result.AccessToken, result.ExpiresOn);
        }
        catch (Exception)
        {
            // If silent token acquisition fails, we might need to re-authenticate
            // For now, rethrow - in a production app you might want to handle this differently
            throw;
        }
    }
}
