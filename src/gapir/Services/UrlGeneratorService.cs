using gapir.Utilities;

namespace gapir.Services;

/// <summary>
/// Service responsible for generating URLs for pull requests in both short and full formats
/// </summary>
public class UrlGeneratorService
{
    /// <summary>
    /// Generates a URL for the pull request, either short (Base62) or full
    /// </summary>
    /// <param name="pullRequestId">The PR ID</param>
    /// <param name="useShortUrl">Whether to generate a short URL</param>
    /// <returns>Generated URL string</returns>
    public string GenerateUrl(int pullRequestId, bool useShortUrl)
    {
        if (useShortUrl)
        {
            // Generate short URL using Base62 encoding
            var base62Id = Base62.Encode(pullRequestId);
            return $"http://g/pr/{base62Id}";
        }
        else
        {
            // Generate full Azure DevOps URL
            return $"https://msazure.visualstudio.com/One/_git/AD-AggregatorService-Workloads/pullrequest/{pullRequestId}";
        }
    }
}
