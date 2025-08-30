using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

using System.Text.Json;

namespace gapir.Tests;

/// <summary>
/// Integration tests for the gapir command-line tool.
/// These tests verify different flag combinations by running the actual executable.
/// </summary>
public class GapirCommandLineTests
{
    private readonly ITestOutputHelper _output;
    private const int TimeoutMs = 30000; // 30 seconds timeout

    public GapirCommandLineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("--help", true)]  // Full help with all flag documentation
    [InlineData("-h", false)]     // Short help, basic usage only
    public async Task Help_Flags_ShowUsageInformation(string helpFlag, bool expectDetailedHelp)
    {
        // Arrange & Act
        var result = await RunGapirAsync(helpFlag);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("gapir", result.Output);
        Assert.Contains("Usage:", result.Output);
        
        // Detailed flag documentation only shown for --help
        if (expectDetailedHelp)
        {
            Assert.Contains("show-approved", result.Output);
            Assert.Contains("verbose", result.Output);
            Assert.Contains("full-urls", result.Output);
            Assert.Contains("help", result.Output);
        }
    }

    [Theory]
    [InlineData("", false, false)] // Default: no approved, short URLs
    [InlineData("--verbose", false, false)] // Verbose mode (affects auth but not JSON structure)
    [InlineData("-v", false, false)] // Verbose mode (short flag)
    [InlineData("--show-approved", true, false)] // Show approved PRs
    [InlineData("-a", true, false)] // Show approved PRs (short flag)
    [InlineData("--show-approved --full-urls", true, true)] // Show approved + full URLs
    [InlineData("-a -f", true, true)] // Show approved + full URLs (short flags)
    [InlineData("--show-approved --verbose", true, false)] // Show approved + verbose
    [InlineData("-a -v", true, false)] // Show approved + verbose (short flags)
    [InlineData("--verbose --full-urls", false, true)] // Verbose + full URLs
    [InlineData("-v -f", false, true)] // Verbose + full URLs (short flags)
    [InlineData("--show-approved --verbose --full-urls", true, true)] // All flags
    [InlineData("-a -v -f", true, true)] // All flags (short form)
    public async Task Flag_Combinations_ProduceExpectedBehavior(string flags, bool expectApproved, bool expectFullUrls)
    {
        // Arrange & Act - Use JSON output for consistent testing
        var result = await RunGapirAsync($"{flags} --json".Trim());

        // Assert
        Assert.Equal(0, result.ExitCode);
        
        // Parse JSON output
        var jsonDoc = JsonDocument.Parse(result.Output);
        var root = jsonDoc.RootElement;

        // Verify basic structure
        Assert.True(root.TryGetProperty("title", out var titleProp));
        Assert.Contains("gapir", titleProp.GetString()!);

        // Check authentication success (should be true for valid flags)
        Assert.True(root.TryGetProperty("authenticationSuccessful", out var authProp));
        Assert.True(authProp.GetBoolean());

        // Check data structure presence
        Assert.True(root.TryGetProperty("pendingPRs", out var pendingProp));
        Assert.True(root.TryGetProperty("approvedPRs", out var approvedProp));
        Assert.True(root.TryGetProperty("apiReviewersFoundViaGroup", out var apiReviewersFoundProp));

        // Check that we have PR data arrays (they can be empty but should exist)
        Assert.Equal(JsonValueKind.Array, pendingProp.ValueKind);
        Assert.Equal(JsonValueKind.Array, approvedProp.ValueKind);

        // If we have PR data, verify URL format based on flag
        if (pendingProp.GetArrayLength() > 0)
        {
            var firstPr = pendingProp[0];
            Assert.True(firstPr.TryGetProperty("shortUrl", out var shortUrlProp));
            Assert.True(firstPr.TryGetProperty("fullUrl", out var fullUrlProp));

            if (expectFullUrls)
            {
                // URLs in the data should be full URLs when flag is set
                Assert.Contains("https://msazure.visualstudio.com", fullUrlProp.GetString()!);
            }
            else
            {
                // URLs in the data should include short URLs when flag is not set
                Assert.Contains("http://g/pr/", shortUrlProp.GetString()!);
            }
        }

        // Verify approved PR data is present if requested
        if (expectApproved)
        {
            // The approved PRs array should be available (may be empty but present)
            Assert.Equal(JsonValueKind.Array, approvedProp.ValueKind);
        }
    }

    [Theory]
    [InlineData("--invalid-flag")]
    public async Task Invalid_Flags_Return_Error_Code(string flags)
    {
        // Arrange & Act
        var result = await RunGapirAsync(flags);

        // Assert - Should return error exit code for invalid flags
        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData("--unknown-flag")]
    [InlineData("--show-approved=true")]  // Flags don't accept values
    [InlineData("--verbose extra-arg")]   // Extra arguments
    [InlineData("-x")]                    // Unknown short flag
    public async Task Invalid_Or_Edge_Case_Arguments_HandleGracefully(string arguments)
    {
        // Arrange & Act
        var result = await RunGapirAsync(arguments);

        // Assert - Should return error exit code for invalid arguments
        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData("--help --verbose")]      // Help with other flags (help should take precedence)
    public async Task Help_Takes_Precedence_Over_Other_Flags(string arguments)
    {
        // Arrange & Act
        var result = await RunGapirAsync(arguments);

        // Assert - Should show help and exit successfully
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Output);
    }

    [Theory]
    [InlineData("")]                      // No arguments
    [InlineData("   ")]                   // Whitespace only
    public async Task Default_Behavior_Works_With_No_Arguments(string arguments)
    {
        // Arrange & Act - Use JSON output for consistent testing
        var result = await RunGapirAsync($"{arguments} --json".Trim());

        // Assert
        Assert.Equal(0, result.ExitCode);
        
        // Parse JSON output
        var jsonDoc = JsonDocument.Parse(result.Output);
        var root = jsonDoc.RootElement;

        // Verify basic structure is present
        Assert.True(root.TryGetProperty("title", out var titleProp));
        Assert.Contains("gapir", titleProp.GetString()!);
        
        Assert.True(root.TryGetProperty("authenticationSuccessful", out var authProp));
        Assert.True(authProp.GetBoolean());

        Assert.True(root.TryGetProperty("pendingPRs", out var pendingProp));
        Assert.True(root.TryGetProperty("approvedPRs", out var approvedProp));
        
        // Should have array structure even if empty
        Assert.Equal(JsonValueKind.Array, pendingProp.ValueKind);
        Assert.Equal(JsonValueKind.Array, approvedProp.ValueKind);
    }

    /// <summary>
    /// Helper method to run the gapir executable with specified arguments
    /// </summary>
    private async Task<ProcessResult> RunGapirAsync(string arguments)
    {
        // Find the repository root by looking for .git directory
        var currentDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        var repoRoot = FindRepositoryRoot(currentDir);
        var gapirProjectPath = Path.Combine(repoRoot, "src", "gapir");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run -- {arguments}",
            WorkingDirectory = gapirProjectPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await process.WaitForExitAsync(TimeSpan.FromMilliseconds(TimeoutMs));
        
        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"Process timed out after {TimeoutMs}ms");
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        
        // For JSON output tests, we only want stdout since stderr contains logging
        var outputForTesting = arguments.Contains("--json") ? output : 
            string.IsNullOrEmpty(error) ? output : $"{output}\nSTDERR:\n{error}";

        _output.WriteLine($"Command: dotnet run -- {arguments}");
        _output.WriteLine($"Exit Code: {process.ExitCode}");
        _output.WriteLine($"STDOUT:\n{output}");
        if (!string.IsNullOrEmpty(error))
        {
            _output.WriteLine($"STDERR:\n{error}");
        }

        return new ProcessResult(process.ExitCode, outputForTesting, output, error);
    }

    /// <summary>
    /// Finds the repository root by looking for the .git directory
    /// </summary>
    private static string FindRepositoryRoot(string startPath)
    {
        var currentDir = new DirectoryInfo(startPath);
        
        while (currentDir != null)
        {
            if (Directory.Exists(Path.Combine(currentDir.FullName, ".git")))
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }
        
        throw new InvalidOperationException("Could not find repository root (.git directory)");
    }

    /// <summary>
    /// Result of running a process
    /// </summary>
    private record ProcessResult(int ExitCode, string Output, string StdOut, string StdErr);
}
