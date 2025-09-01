namespace gapir;

using gapir.Models;
using gapir.Services;
using Microsoft.VisualStudio.Services.WebApi;

public class PullRequestChecker
{
   
   [Obsolete("use PullRequestDataService.GetPullRequestDataAsync")]
    public async Task<GapirResult> GetResultsAsync(VssConnection connection)
    {
        // Get pull request data using the data service
        var dataService = new PullRequestDataService();
        return await dataService.GetPullRequestDataAsync(connection);
    }
}
