namespace gapir;

using gapir.Models;
using gapir.Services;
using gapir.Utilities;
using Microsoft.VisualStudio.Services.WebApi;

/// <summary>
/// Main class for diagnosing individual pull requests
/// </summary>
public class PullRequestDiagnosticChecker
{
    private readonly bool _jsonOutput;

    public PullRequestDiagnosticChecker(bool jsonOutput = false)
    {
        _jsonOutput = jsonOutput;
    }

    public async Task RunAsync(int prId)
    {
        var result = await GetDiagnosticResultAsync(prId);
        
        var renderingService = new PullRequestDiagnosticRenderingService(_jsonOutput);
        renderingService.RenderDiagnosticResult(result);
    }

    public async Task<PrDiagnosticResult> GetDiagnosticResultAsync(int prId)
    {
        try
        {
            Log.Information("Authenticating with Azure DevOps...");
            var connection = await ConsoleAuth.AuthenticateAsync(PullRequestDataService.OrganizationUrl, _jsonOutput);

            if (connection == null)
            {
                Log.Error("Authentication failed.");
                return new PrDiagnosticResult
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
            return new PrDiagnosticResult
            {
                PullRequestId = prId,
                ErrorMessage = ex.Message
            };
        }
    }
}
