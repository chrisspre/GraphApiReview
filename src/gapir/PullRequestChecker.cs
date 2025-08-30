namespace gapir;

using gapir.Models;
using gapir.Services;
using gapir.Utilities;
using Microsoft.VisualStudio.Services.WebApi;

public class PullRequestChecker
{
    private readonly PullRequestCheckerOptions _options;

    public PullRequestChecker(PullRequestCheckerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task RunAsync()
    {
        var result = await GetResultsAsync();
        
        var renderingService = new PullRequestRenderingService(_options);
        renderingService.RenderResult(result);
    }

    public async Task<GapirResult> GetResultsAsync()
    {
        // Azure DevOps organization URL
        const string OrganizationUrl = "https://msazure.visualstudio.com/";
        
        VssConnection? connection;

        // Authentication phase
        try
        {
            if (Log.IsVerbose && !_options.JsonOutput)
            {
                using (var authSpinner = new Spinner("Authenticating with Azure DevOps..."))
                {
                    connection = await ConsoleAuth.AuthenticateAsync(OrganizationUrl, _options.JsonOutput);

                    if (connection == null)
                    {
                        authSpinner.Error("Authentication failed");
                        return new GapirResult
                        {
                            Title = "gapir (Graph API Review) - Azure DevOps Pull Request Checker",
                            ErrorMessage = "Authentication failed"
                        };
                    }

                    authSpinner.Success("Authentication successful");
                }
            }
            else
            {
                connection = await ConsoleAuth.AuthenticateAsync(OrganizationUrl, _options.JsonOutput);

                if (connection == null)
                {
                    return new GapirResult
                    {
                        Title = "gapir (Graph API Review) - Azure DevOps Pull Request Checker",
                        ErrorMessage = "Authentication failed"
                    };
                }
            }

            // Get pull request data using the data service
            var dataService = new PullRequestDataService();
            return await dataService.GetPullRequestDataAsync(connection, _options);
        }
        catch (Exception ex)
        {
            return new GapirResult
            {
                Title = "gapir (Graph API Review) - Azure DevOps Pull Request Checker",
                ErrorMessage = ex.Message
            };
        }
    }
}
