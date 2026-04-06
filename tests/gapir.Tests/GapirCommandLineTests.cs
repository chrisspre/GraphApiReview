using System.Diagnostics;
using System.Text;
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
        Assert.Contains("report", result.Output);
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
    [InlineData("report --help")]
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
    public async Task ReportHelp_ShowsIncludeCurrentWeekOption()
    {
        // Arrange & Act
        var result = await RunGapirAsync("report --help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--include-current-week", result.Output);
    }

    [Theory]
    [InlineData("report --weeks 0", "Weeks must be between 1 and 52 unless --include-current-week is set.")]
    [InlineData("report --weeks 53", "Weeks must be between 0 and 52.")]
    [InlineData("report -i --weeks 53", "Weeks must be between 0 and 52.")]
    public async Task ReportCommand_InvalidWeeks_ShowsValidationError(string command, string expectedMessage)
    {
        // Arrange & Act
        var result = await RunGapirAsync(command);

        // Assert
        Assert.Equal(1, result.ExitCode);
        Assert.Contains(expectedMessage, result.Output);
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

    [Theory]
    [InlineData("approved --help")]
    [InlineData("approved --verbose --help")]
    [InlineData("review --verbose --help")]
    public async Task Subcommand_Combinations_ShowExpectedHelpSurface(string subcommand)
    {
        // Arrange & Act
        var result = await RunGapirAsync(subcommand);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Usage:", result.Output);
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

        var outputForTesting = string.IsNullOrEmpty(error) ? output : $"{output}\nSTDERR:\n{error}";

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
