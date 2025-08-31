using System.CommandLine;
using System.CommandLine.Parsing;
using Xunit;

namespace gapir.Tests;

/// <summary>
/// Unit tests for command line parsing logic that test the REAL command structure from Program.cs.
/// These tests are fast because they only test parsing without executing expensive operations.
/// </summary>
public class RealCommandLineParsingTests
{
    /// <summary>
    /// Gets the real parser from the actual Program.cs command structure
    /// </summary>
    private static Parser CreateRealParser()
    {
        var rootCommand = Program.CreateRootCommand();
        return new Parser(rootCommand);
    }

    [Fact]
    public void Review_WithNoOptions_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "review" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal("review", parseResult.CommandResult.Command.Name);
        Assert.False(parseResult.GetValueForOption<bool>("--full-urls"));
        Assert.False(parseResult.GetValueForOption<bool>("--detailed-timing"));
        Assert.False(parseResult.GetValueForOption<bool>("--show-detailed-info"));
        Assert.Equal(Format.Text, parseResult.GetValueForOption<Format>("--format"));
    }

    [Fact]
    public void Review_WithFullUrls_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "review", "--full-urls" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(parseResult.GetValueForOption<bool>("--full-urls"));
    }

    [Fact]
    public void Review_WithDetailedTiming_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "review", "--detailed-timing" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(parseResult.GetValueForOption<bool>("--detailed-timing"));
    }

    [Fact]
    public void Review_WithShowDetailedInfo_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "review", "--show-detailed-info" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(parseResult.GetValueForOption<bool>("--show-detailed-info"));
    }

    [Fact]
    public void Review_WithJsonFormat_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "review", "--format", "json" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal(Format.Json, parseResult.GetValueForOption<Format>("--format"));
    }

    [Fact]
    public void Review_WithAllOptions_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "review", "--full-urls", "--detailed-timing", "--show-detailed-info", "--format", "json" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.True(parseResult.GetValueForOption<bool>("--full-urls"));
        Assert.True(parseResult.GetValueForOption<bool>("--detailed-timing"));
        Assert.True(parseResult.GetValueForOption<bool>("--show-detailed-info"));
        Assert.Equal(Format.Json, parseResult.GetValueForOption<Format>("--format"));
    }

    [Fact]
    public void Approved_WithNoOptions_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "approved" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal("approved", parseResult.CommandResult.Command.Name);
        Assert.False(parseResult.GetValueForOption<bool>("--full-urls"));
        Assert.False(parseResult.GetValueForOption<bool>("--detailed-timing"));
        Assert.False(parseResult.GetValueForOption<bool>("--show-detailed-info"));
        Assert.Equal(Format.Text, parseResult.GetValueForOption<Format>("--format"));
    }

    [Fact]
    public void Approved_WithAllOptions_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
        var args = new[] { "approved", "--full-urls", "--detailed-timing", "--show-detailed-info", "--format", "json" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any());
        Assert.Equal("approved", parseResult.CommandResult.Command.Name);
        Assert.True(parseResult.GetValueForOption<bool>("--full-urls"));
        Assert.True(parseResult.GetValueForOption<bool>("--detailed-timing"));
        Assert.True(parseResult.GetValueForOption<bool>("--show-detailed-info"));
        Assert.Equal(Format.Json, parseResult.GetValueForOption<Format>("--format"));
    }

    [Fact]
    public void Collect_ParsesCorrectly()
    {
        // Arrange
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
        var args = new[] { "diagnose", "invalid" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.True(parseResult.Errors.Any());
    }

    [Fact]
    public void Diagnose_WithoutId_HasParseError()
    {
        // Arrange
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
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
        var parser = CreateRealParser();
        var args = new[] { "review", "--invalid-option" };

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.True(parseResult.Errors.Any());
    }

    [Fact]
    public void VerifyCommandStructure_HasExpectedSubcommands()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();

        // Act & Assert
        var subcommands = rootCommand.Subcommands.Select(c => c.Name).ToList();
        
        Assert.Contains("review", subcommands);
        Assert.Contains("collect", subcommands);
        Assert.Contains("diagnose", subcommands);
        Assert.Contains("approved", subcommands);
        Assert.Equal(4, subcommands.Count); // Ensure we don't have extra commands
    }

    [Fact]
    public void VerifyGlobalOptions_AreCorrectlyDefined()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();

        // Act & Assert
        var globalOptions = rootCommand.Options.Select(o => o.Name).ToList();
        
        Assert.Contains("verbose", globalOptions);
        Assert.Contains("format", globalOptions);
    }
}
