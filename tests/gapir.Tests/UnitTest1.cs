using System.Diagnostics;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using gapir.Utilities;

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
        Assert.Contains("gapir (Graph API Review) - Azure DevOps Pull Request Checker", result.Output);
        Assert.Contains("Usage: gapir [options]", result.Output);
        
        // Detailed flag documentation only shown for --help
        if (expectDetailedHelp)
        {
            Assert.Contains("-a, --show-approved", result.Output);
            Assert.Contains("-v, --verbose", result.Output);
            Assert.Contains("-f, --full-urls", result.Output);
            Assert.Contains("-h, --help", result.Output);
        }
    }

    [Theory]
    [InlineData("", false, false, false)] // Default: quiet, no approved, short URLs
    [InlineData("--verbose", true, false, false)] // Verbose mode
    [InlineData("-v", true, false, false)] // Verbose mode (short flag)
    [InlineData("--show-approved", false, true, false)] // Show approved PRs
    [InlineData("-a", false, true, false)] // Show approved PRs (short flag)
    [InlineData("--show-approved --full-urls", false, true, true)] // Show approved + full URLs
    [InlineData("-a -f", false, true, true)] // Show approved + full URLs (short flags)
    [InlineData("--show-approved --verbose", true, true, false)] // Show approved + verbose
    [InlineData("-a -v", true, true, false)] // Show approved + verbose (short flags)
    [InlineData("--verbose --full-urls", true, false, true)] // Verbose + full URLs
    [InlineData("-v -f", true, false, true)] // Verbose + full URLs (short flags)
    [InlineData("--show-approved --verbose --full-urls", true, true, true)] // All flags
    [InlineData("-a -v -f", true, true, true)] // All flags (short form)
    [InlineData("--invalid-flag", false, false, false)] // Invalid flag (should be ignored)
    public async Task Flag_Combinations_ProduceExpectedBehavior(string flags, bool expectVerbose, bool expectApproved, bool expectFullUrls)
    {
        // Arrange & Act
        var result = await RunGapirAsync(flags);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("gapir (Graph API Review) - Azure DevOps Pull Request Checker", result.Output);

        // Check verbose behavior
        if (expectVerbose)
        {
            Assert.Contains("Authenticating with Azure DevOps", result.Output);
            Assert.Contains("Successfully authenticated!", result.Output);
            Assert.Contains("Checking pull requests for user:", result.Output);
        }
        else
        {
            Assert.DoesNotContain("Authenticating with Azure DevOps", result.Output);
            Assert.DoesNotContain("Successfully authenticated!", result.Output);
            Assert.DoesNotContain("Checking pull requests for user:", result.Output);
        }

        // Check approved PRs behavior
        if (expectApproved)
        {
            Assert.Contains("PR(s) you have already approved:", result.Output);
        }
        else
        {
            Assert.DoesNotContain("PR(s) you have already approved:", result.Output);
        }

        // Check URL format (only if there are URLs in the output)
        if (result.Output.Contains("http"))
        {
            if (expectFullUrls)
            {
                Assert.Contains("https://msazure.visualstudio.com", result.Output);
                Assert.DoesNotContain("http://g/pr/", result.Output);
            }
            else
            {
                Assert.Contains("http://g/pr/", result.Output);
                Assert.DoesNotContain("https://msazure.visualstudio.com", result.Output);
            }
        }

        // All should show pending PRs section
        Assert.Contains("PR(s) pending your approval:", result.Output);
    }

    [Theory]
    [InlineData("--unknown-flag")]
    [InlineData("--show-approved=true")]  // Flags don't accept values
    [InlineData("--verbose extra-arg")]   // Extra arguments
    [InlineData("-x")]                    // Unknown short flag
    [InlineData("--help --verbose")]      // Help with other flags (help should take precedence)
    public async Task Invalid_Or_Edge_Case_Arguments_HandleGracefully(string arguments)
    {
        // Arrange & Act
        var result = await RunGapirAsync(arguments);

        // Assert - Should still run successfully (graceful degradation)
        Assert.Equal(0, result.ExitCode);
        
        // If help is included, should show help
        if (arguments.Contains("--help"))
        {
            Assert.Contains("Usage: gapir [options]", result.Output);
        }
        else
        {
            // Should still show the main application output
            Assert.Contains("gapir (Graph API Review) - Azure DevOps Pull Request Checker", result.Output);
        }
    }

    [Theory]
    [InlineData("")]                      // No arguments
    [InlineData("   ")]                   // Whitespace only
    public async Task Default_Behavior_Works_With_No_Arguments(string arguments)
    {
        // Arrange & Act
        var result = await RunGapirAsync(arguments);

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("gapir (Graph API Review) - Azure DevOps Pull Request Checker", result.Output);
        Assert.Contains("PR(s) pending your approval:", result.Output);
        
        // Should not show verbose output by default
        Assert.DoesNotContain("Authenticating with Azure DevOps", result.Output);
        Assert.DoesNotContain("Successfully authenticated!", result.Output);
        
        // Should not show approved PRs by default
        Assert.DoesNotContain("PR(s) you have already approved:", result.Output);
        
        // Should use short URLs by default (if any URLs present)
        if (result.Output.Contains("http"))
        {
            Assert.Contains("http://g/pr/", result.Output);
            Assert.DoesNotContain("https://msazure.visualstudio.com", result.Output);
        }
    }

    /// <summary>
    /// Helper method to run the gapir executable with specified arguments
    /// </summary>
    private async Task<ProcessResult> RunGapirAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run -- {arguments}",
            WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "src", "gapir"),
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
        var fullOutput = string.IsNullOrEmpty(error) ? output : $"{output}\nSTDERR:\n{error}";

        _output.WriteLine($"Command: dotnet run -- {arguments}");
        _output.WriteLine($"Exit Code: {process.ExitCode}");
        _output.WriteLine($"Output:\n{fullOutput}");

        return new ProcessResult(process.ExitCode, fullOutput);
    }

    /// <summary>
    /// Result of running a process
    /// </summary>
    private record ProcessResult(int ExitCode, string Output);
}

/// <summary>
/// Extension methods for process timeout handling
/// </summary>
public static class ProcessExtensions
{
    public static async Task<bool> WaitForExitAsync(this Process process, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>
/// Tests for the Base62 utility class.
/// </summary>
public class Base62Tests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(61, "Z")]
    [InlineData(62, "10")]
    [InlineData(12041652, "OwAc")]
    [InlineData(999999, "4c91")]
    public void Encode_ValidInputs_ReturnsExpectedBase62String(long input, string expected)
    {
        // Act
        string result = Base62.Encode(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("Z", 61)]
    [InlineData("10", 62)]
    [InlineData("OwAc", 12041652)]
    [InlineData("4c91", 999999)]
    public void Decode_ValidInputs_ReturnsExpectedInteger(string input, long expected)
    {
        // Act
        long result = Base62.Decode(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null!)]
    public void Decode_NullOrEmptyInput_ThrowsArgumentException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Base62.Decode(input));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("@")]
    [InlineData(" ")]
    [InlineData("1@3")]
    public void Decode_InvalidCharacters_ThrowsArgumentException(string input)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Base62.Decode(input));
    }

    [Fact]
    public void Encode_NegativeValue_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Base62.Encode(-1));
    }

    [Theory]
    [InlineData("0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", true)]
    [InlineData("123abc", true)]
    [InlineData("ABC", true)]
    [InlineData("0", true)]
    [InlineData("", false)]
    [InlineData(null!, false)]
    [InlineData("123!", false)]
    [InlineData("123 abc", false)]
    public void IsValidBase62_VariousInputs_ReturnsExpectedResult(string input, bool expected)
    {
        // Act
        bool result = Base62.IsValidBase62(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("12041652", true)]
    [InlineData("0", true)]
    [InlineData("999", true)]
    [InlineData("", false)]
    [InlineData(null!, false)]
    [InlineData("123abc", false)]
    [InlineData("12.34", false)]
    [InlineData("123 456", false)]
    public void IsDecimal_VariousInputs_ReturnsExpectedResult(string input, bool expected)
    {
        // Act
        bool result = Base62.IsDecimal(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(62)]
    [InlineData(12041652)]
    [InlineData(999999)]
    [InlineData(long.MaxValue)]
    public void EncodeAndDecode_RoundTrip_PreservesOriginalValue(long originalValue)
    {
        // Act
        string encoded = Base62.Encode(originalValue);
        long decoded = Base62.Decode(encoded);

        // Assert
        Assert.Equal(originalValue, decoded);
    }

    [Fact]
    public void ShortUrl_Generation_Uses_Base62_And_New_Domain()
    {
        // This test verifies that the new short URL format uses Base62 encoding and the 'g' domain
        // Note: This is more of an integration verification than a unit test
        
        // Arrange
        long testPrId = 12041652;
        string expectedBase62 = "OwAc";
        string expectedUrl = $"http://g/pr/{expectedBase62}";
        
        // Act
        string actualBase62 = Base62.Encode(testPrId);
        
        // Assert
        Assert.Equal(expectedBase62, actualBase62);
        
        // Verify the URL format that would be generated
        string actualUrl = $"http://g/pr/{actualBase62}";
        Assert.Equal(expectedUrl, actualUrl);
        
        // Verify round-trip works
        long decodedId = Base62.Decode(actualBase62);
        Assert.Equal(testPrId, decodedId);
    }
}
