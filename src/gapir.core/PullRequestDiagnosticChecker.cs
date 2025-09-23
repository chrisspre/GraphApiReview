namespace gapir;

using gapir.Models;
using gapir.Services;

/// <summary>
/// Main class for diagnosing individual pull requests
/// </summary>
public class PullRequestDiagnostics
{
    private readonly ConsoleAuth _consoleAuth;
    private readonly PullRequestDiagnosticService _diagnosticService;

    public PullRequestDiagnostics(ConsoleAuth consoleAuth, PullRequestDiagnosticService diagnosticService)
    {
        _consoleAuth = consoleAuth;
        _diagnosticService = diagnosticService;
    }

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
            var connection = await _consoleAuth.AuthenticateAsync();

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
            return await _diagnosticService.DiagnosePullRequestAsync(connection, prId);
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
