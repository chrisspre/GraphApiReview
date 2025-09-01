using System.Diagnostics;
using Xunit.Abstractions;

namespace gapir.Tests;

/// <summary>
/// Lightweight integration tests for the gapir command-line tool.
/// These tests verify basic CLI structure without expensive authentication operations.
/// For detailed parsing logic tests, see CommandLineParsingTests.cs
/// </summary>
public class GapirCommandLineTests
{
    private readonly ITestOutputHelper _output;
    private const int TimeoutMs = 10000; // 10 seconds timeout

    public GapirCommandLineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public async Task Help_Flags_ShowUsageInformation(string helpFlag)
    {
        // Arrange & Act
        var result = await RunGapirAsync(helpFlag);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("gapir", result.Output);
        Assert.Contains("Usage:", result.Output);
        Assert.Contains("Commands:", result.Output);
        
        // Should show available subcommands
        Assert.Contains("review", result.Output);
        Assert.Contains("collect", result.Output);
        Assert.Contains("diagnose", result.Output);
        Assert.Contains("approved", result.Output);
    }

    [Fact]
    public async Task RootCommand_WithoutSubcommand_ShowsHelpAndFails()
    {
        // Arrange & Act
        var result = await RunGapirAsync("");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Usage:", result.Output);
        Assert.Contains("Commands:", result.Output);
    }

    [Theory]
    [InlineData("review --help")]
    [InlineData("collect --help")]
    [InlineData("diagnose --help")]
    [InlineData("approved --help")]
    public async Task SubcommandHelp_ShowsSubcommandSpecificHelp(string command)
    {
        // Arrange & Act
        var result = await RunGapirAsync(command);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Output);
        
        // Extract subcommand name for verification
        var subcommand = command.Split(' ')[0];
        Assert.Contains(subcommand, result.Output);
    }

    [Fact]
    public async Task InvalidSubcommand_ShowsError()
    {
        // Arrange & Act
        var result = await RunGapirAsync("invalid-command");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("'invalid-command'", result.Output);
    }

    [Fact]
    public async Task InvalidOption_ShowsError()
    {
        // Arrange & Act
        var result = await RunGapirAsync("review --invalid-option");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--invalid-option", result.Output);
    }

    [Fact]
    public async Task DiagnoseWithoutId_ShowsError()
    {
        // Arrange & Act
        var result = await RunGapirAsync("diagnose");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Required argument missing", result.Output);
    }

    [Fact]
    public async Task DiagnoseWithInvalidId_ShowsError()
    {
        // Arrange & Act
        var result = await RunGapirAsync("diagnose invalid-id");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("invalid-id", result.Output);
    }

    // Helper method to run gapir command with timeout and capture output
    private async Task<(int ExitCode, string Output)> RunGapirAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project src/gapir -- {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = GetWorkspaceRoot()
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Wait for completion with timeout
        var completed = await Task.Run(() => process.WaitForExit(TimeoutMs));

        if (!completed)
        {
            process.Kill();
            throw new TimeoutException($"Command 'dotnet run -- {arguments}' timed out after {TimeoutMs}ms");
        }

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();
        var combinedOutput = string.IsNullOrEmpty(error) ? output : $"{output}\n{error}";

        _output.WriteLine($"Command: dotnet run -- {arguments}");
        _output.WriteLine($"Exit Code: {process.ExitCode}");
        _output.WriteLine($"Output:\n{combinedOutput}");

        return (process.ExitCode, combinedOutput);
    }

    private static string GetWorkspaceRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ApiReview.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find workspace root containing ApiReview.sln");
    }

    [Theory]
    [InlineData("approved", true, false)] // Show approved PRs
    [InlineData("approved --full-urls", true, true)] // Show approved + full URLs
    [InlineData("approved --verbose", true, false)] // Show approved + verbose
    [InlineData("review --verbose --full-urls", false, true)] // Review with verbose + full URLs
    [InlineData("approved --verbose --full-urls", true, true)] // Approved with all flags
    public async Task Subcommand_Combinations_ProduceExpectedBehavior(string subcommand, bool expectApproved, bool expectFullUrls)
    {
        // Arrange & Act - Use JSON output for consistent testing
        var result = await RunGapirAsync($"{subcommand} --format Json".Trim());

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
    [InlineData("invalid-subcommand")]
    public async Task Invalid_Commands_Return_Error_Code(string command)
    {
        // Arrange & Act
        var result = await RunGapirAsync(command);

        // Assert - Should return error exit code for invalid commands
        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData("--unknown-flag")]
    [InlineData("--approved=true")]  // Flags don't accept values
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
    [InlineData("")]                      // No arguments - should show help
    [InlineData("   ")]                   // Whitespace only - should show help
    public async Task Default_Behavior_Shows_Help_With_No_Arguments(string arguments)
    {
        // Arrange & Act
        var result = await RunGapirAsync(arguments.Trim());

        // Assert - With no subcommand, should show help and return error code
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Usage:", result.Output);
        Assert.Contains("Commands:", result.Output);
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
        var outputForTesting = arguments.Contains("--format Json") ? output : 
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
