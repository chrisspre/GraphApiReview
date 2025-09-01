namespace gapir;

using gapir.Models;
using gapir.Services;

/// <summary>
/// Main class for diagnosing individual pull requests
/// </summary>
public class PullRequestDiagnostics
{
    // private readonly Format _format;

    // public PullRequestDiagnosticChecker(Format format)
    // {
    //     // _format = format;
    // }

    // public async Task RunAsync(int prId)
    // {
    //     var result = await GetDiagnosticResultAsync(prId);
        
    //     var renderingService = new PullRequestDiagnosticRenderingService(_format);
    //     renderingService.RenderDiagnosticResult(result);
    // }

    public async Task<PullRequestDiagnosticResult> GetDiagnosticResultAsync(int prId)
    {
        try
        {
            Log.Information("Authenticating with Azure DevOps...");
            var connection = await ConsoleAuth.AuthenticateAsync();

            if (connection == null)
            {
                Log.Error("Authentication failed.");
                return new PullRequestDiagnosticResult
                {
                    PullRequestId = prId,
                    ErrorMessage = "Authentication failed"
                };
            }

            Log.Success("Authentication successful");

            // Get diagnostic data using the diagnostic service
            var diagnosticService = new PullRequestDiagnosticService();
            return await diagnosticService.DiagnosePullRequestAsync(connection, prId);
        }
        catch (Exception ex)
        {
            return new PullRequestDiagnosticResult
            {
                PullRequestId = prId,
                ErrorMessage = ex.Message
            };
        }
    }
}
