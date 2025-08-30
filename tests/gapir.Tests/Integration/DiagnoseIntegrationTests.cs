using Xunit;
using Xunit.Abstractions;

namespace gapir.Tests.Integration;

/// <summary>
/// Integration tests for the diagnose subcommand functionality
/// These tests validate the diagnostic output format and basic command handling
/// </summary>
public class DiagnoseIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public DiagnoseIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Diagnose_WithInvalidPrId_ShowsErrorMessage()
    {
        // Arrange
        var testHelper = new TestHelper(_output);

        // Act
        var result = testHelper.RunGapir("diagnose invalid-pr-id");

        // Assert
        Assert.NotEqual(0, result.ExitCode); // Should fail with invalid PR ID
        Assert.Contains("Cannot parse argument 'invalid-pr-id'", result.Error);
    }

    [Fact]
    public void Diagnose_WithHelpFlag_ShowsHelp()
    {
        // Arrange
        var testHelper = new TestHelper(_output);

        // Act
        var result = testHelper.RunGapir("diagnose --help");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("diagnose", result.Output);
        Assert.Contains("Diagnose a specific PR ID to show raw reviewer data", result.Output);
    }

    [Fact]
    public void Diagnose_WithoutValue_ShowsError()
    {
        // Arrange
        var testHelper = new TestHelper(_output);

        // Act
        var result = testHelper.RunGapir("diagnose");

        // Assert
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Required argument missing for command: 'diagnose'", result.Error);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("13300322")] 
    [InlineData("999999999")]
    public void Diagnose_WithValidFormat_AttemptsAuthentication(string prId)
    {
        // Arrange
        var testHelper = new TestHelper(_output);

        // Act
        var result = testHelper.RunGapir($"diagnose {prId}");

        // Assert
        // The command should attempt to diagnose the PR
        Assert.Contains($"Investigating PR {prId} reviewer details", result.Output);
        
        // Either succeeds with PR details or fails with "not found" error
        var containsPrDetails = result.Output.Contains("REVIEWER DETAILS") || 
                               result.Output.Contains("TF401180: The requested pull request was not found");
        Assert.True(containsPrDetails, $"Should either show PR details or 'not found' error for PR {prId}");
    }
}

/// <summary>
/// Helper class for running gapir commands in tests
/// </summary>
public class TestHelper
{
    private readonly ITestOutputHelper _output;

    public TestHelper(ITestOutputHelper output)
    {
        _output = output;
    }

    public TestResult RunGapir(string arguments)
    {
        using (var process = new System.Diagnostics.Process())
        {
            process.StartInfo.FileName = "dotnet";
            process.StartInfo.Arguments = $"run --project \"d:\\microsoft\\GraphApiReview\\src\\gapir\" -- {arguments}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            var output = new System.Text.StringBuilder();
            var error = new System.Text.StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                    _output.WriteLine($"STDOUT: {e.Data}");
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                    _output.WriteLine($"STDERR: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait up to 30 seconds for command to complete
            bool finished = process.WaitForExit(30000);
            
            if (!finished)
            {
                process.Kill();
                throw new TimeoutException($"Command timed out: dotnet run -- {arguments}");
            }

            return new TestResult
            {
                ExitCode = process.ExitCode,
                Output = output.ToString(),
                Error = error.ToString()
            };
        }
    }
}

/// <summary>
/// Result of running a gapir command
/// </summary>
public class TestResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
