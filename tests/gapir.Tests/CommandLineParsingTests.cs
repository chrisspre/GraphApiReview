using System.CommandLine;
using System.CommandLine.Parsing;
using Xunit;

namespace gapir.Tests;

/// <summary>
/// Unit tests for command line parsing logic without executing expensive operations.
/// Tests the transformation from command line arguments to PullRequestCheckerOptions.
/// </summary>
public class CommandLineParsingTests
{
    /// <summary>
    /// Creates a parser for testing without executing the actual commands
    /// </summary>
    private static Parser CreateTestParser()
    {
        // Create the root command structure similar to Program.cs but without handlers
        var rootCommand = new RootCommand("gapir (Graph API Review) - Azure DevOps Pull Request Checker");

        // Global options
        var verboseOption = new Option<bool>(
            aliases: ["--verbose", "-v"],
            description: "Show diagnostic messages during execution");

        var formatOption = new Option<Format>(
            aliases: ["--format", "-f"],
            getDefaultValue: () => Format.Text,
            description: "Output format: text or json");

        rootCommand.AddGlobalOption(verboseOption);
        rootCommand.AddGlobalOption(formatOption);

        // Review command
        var reviewCommand = new Command("review", "Show pull requests assigned to you for review");
        var fullUrlsOption = new Option<bool>(
            aliases: ["--full-urls", "-f"],
            description: "Use full Azure DevOps URLs instead of short g URLs");
        var detailedTimingOption = new Option<bool>(
            aliases: ["--detailed-timing", "-t"],
            description: "Show detailed age column - slower due to API calls");
        var showDetailedInfoOption = new Option<bool>(
            aliases: ["--show-detailed-info", "-d"],
            description: "Show detailed information section for each pending PR");

        reviewCommand.AddOption(fullUrlsOption);
        reviewCommand.AddOption(detailedTimingOption);
        reviewCommand.AddOption(showDetailedInfoOption);

        // Collect command
        var collectCommand = new Command("collect", "Collect required reviewers from recent PRs");

        // Diagnose command
        var diagnoseCommand = new Command("diagnose", "Diagnose a specific PR ID");
        var prIdArgument = new Argument<int>("id", "The pull request ID to diagnose");
        diagnoseCommand.AddArgument(prIdArgument);

        // Approved command
        var approvedCommand = new Command("approved", "Show table of already approved PRs");
        approvedCommand.AddOption(fullUrlsOption);
        approvedCommand.AddOption(detailedTimingOption);
        approvedCommand.AddOption(showDetailedInfoOption);

        rootCommand.AddCommand(reviewCommand);
        rootCommand.AddCommand(collectCommand);
        rootCommand.AddCommand(diagnoseCommand);
        rootCommand.AddCommand(approvedCommand);

        return new Parser(rootCommand);
    }

    /// <summary>
    /// Helper method to extract options for review and approved commands
    /// </summary>
    private static PullRequestCheckerOptions ExtractPullRequestOptions(ParseResult parseResult, bool showApproved = false)
    {
        return new PullRequestCheckerOptions
        {
            ShowApproved = showApproved,
            UseShortUrls = !parseResult.GetValueForOption<bool>("--full-urls"),
            ShowDetailedTiming = parseResult.GetValueForOption<bool>("--detailed-timing"),
            ShowDetailedInfo = parseResult.GetValueForOption<bool>("--show-detailed-info"),
            Format = parseResult.GetValueForOption<Format>("--format")
        };
    }

    [Fact]
    public void Review_WithNoOptions_SetsDefaults()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.False(options.ShowApproved);
        Assert.True(options.UseShortUrls); // default is short URLs
        Assert.False(options.ShowDetailedTiming);
        Assert.False(options.ShowDetailedInfo);
        Assert.Equal(Format.Text, options.Format);
    }

    [Fact]
    public void Review_WithFullUrls_SetsUseShortUrlsFalse()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", "--full-urls" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.False(options.UseShortUrls); // --full-urls should set this to false
    }

    [Fact]
    public void Review_WithDetailedTiming_SetsDetailedTimingTrue()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", "--detailed-timing" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(options.ShowDetailedTiming);
    }

    [Fact]
    public void Review_WithShowDetailedInfo_SetsShowDetailedInfoTrue()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", "--show-detailed-info" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(options.ShowDetailedInfo);
    }

    [Fact]
    public void Review_WithJsonFormat_SetsFormatJson()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", "--format", "json" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal(Format.Json, options.Format);
    }

    [Fact]
    public void Review_WithAllOptions_SetsAllOptionsCorrectly()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", "--full-urls", "--detailed-timing", "--show-detailed-info", "--format", "json" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.False(options.UseShortUrls);
        Assert.True(options.ShowDetailedTiming);
        Assert.True(options.ShowDetailedInfo);
        Assert.Equal(Format.Json, options.Format);
    }

    [Fact]
    public void Approved_WithNoOptions_SetsShowApprovedTrue()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "approved" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult, showApproved: true);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(options.ShowApproved);
        Assert.True(options.UseShortUrls);
        Assert.False(options.ShowDetailedTiming);
        Assert.False(options.ShowDetailedInfo);
        Assert.Equal(Format.Text, options.Format);
    }

    [Fact]
    public void Approved_WithAllOptions_SetsAllOptionsCorrectly()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "approved", "--full-urls", "--detailed-timing", "--show-detailed-info", "--format", "json" };

        // Act
        var parseResult = parser.Parse(args);
        var options = ExtractPullRequestOptions(parseResult, showApproved: true);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(options.ShowApproved);
        Assert.False(options.UseShortUrls);
        Assert.True(options.ShowDetailedTiming);
        Assert.True(options.ShowDetailedInfo);
        Assert.Equal(Format.Json, options.Format);
    }

    [Fact]
    public void Collect_ValidCommand_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "collect" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal("collect", parseResult.CommandResult.Command.Name);
    }

    [Fact]
    public void Collect_WithVerbose_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "collect", "--verbose" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(parseResult.GetValueForOption<bool>("--verbose"));
    }

    [Fact]
    public void Diagnose_WithValidId_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "diagnose", "12345" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal("diagnose", parseResult.CommandResult.Command.Name);
        Assert.Equal(12345, parseResult.GetValueForArgument<int>("id"));
    }

    [Fact]
    public void Diagnose_WithInvalidId_HasParseError()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "diagnose", "invalid" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.True(parseResult.Errors.Any());
        Assert.Contains(parseResult.Errors, e => e.Message.Contains("invalid"));
    }

    [Fact]
    public void Diagnose_WithoutId_HasParseError()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "diagnose" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.True(parseResult.Errors.Any());
    }

    [Fact]
    public void Diagnose_WithJsonFormat_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "diagnose", "12345", "--format", "json" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal(12345, parseResult.GetValueForArgument<int>("id"));
        Assert.Equal(Format.Json, parseResult.GetValueForOption<Format>("--format"));
    }

    [Theory]
    [InlineData("--verbose")]
    [InlineData("-v")]
    public void GlobalVerboseOption_BothAliases_ParseCorrectly(string verboseFlag)
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", verboseFlag };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(parseResult.GetValueForOption<bool>("--verbose"));
    }

    [Theory]
    [InlineData("--format", "json")]
    [InlineData("-f", "json")]
    [InlineData("--format", "text")]
    [InlineData("-f", "text")]
    public void GlobalFormatOption_BothAliasesAndValues_ParseCorrectly(string formatFlag, string formatValue)
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", formatFlag, formatValue };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        var expectedFormat = formatValue == "json" ? Format.Json : Format.Text;
        Assert.Equal(expectedFormat, parseResult.GetValueForOption<Format>("--format"));
    }

    [Fact]
    public void RootCommand_WithoutSubcommand_ShowsHelpWithError()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = Array.Empty<string>();

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        // Root command without subcommand should show help and have errors
        Assert.True(parseResult.Errors.Any() || parseResult.CommandResult.Command.Name == "gapir");
    }

    [Fact]
    public void InvalidSubcommand_HasParseError()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "invalid-command" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.True(parseResult.Errors.Any());
    }

    [Fact]
    public void InvalidOption_HasParseError()
    {
        // Arrange
        var parser = CreateTestParser();
        var args = new[] { "review", "--invalid-option" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.True(parseResult.Errors.Any());
    }
}
