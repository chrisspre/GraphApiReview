using System.CommandLine;
using System.CommandLine.Parsing;
using Xunit;

namespace gapir.Tests;

/// <summary>
/// Simple unit tests that verify the command structure without expensive operations.
/// Tests the real command structure from Program.cs.
/// </summary>
public class CommandStructureTests
{
    [Fact]
    public void CreateRootCommand_ReturnsValidCommand()
    {
        // Act
        var rootCommand = Program.CreateRootCommand();

        // Assert
        Assert.NotNull(rootCommand);
        Assert.NotNull(rootCommand.Description);
    }

    [Fact]
    public void RootCommand_HasExpectedSubcommands()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();

        // Act
        var subcommandNames = rootCommand.Subcommands.Select(c => c.Name).ToList();

        // Assert
        Assert.Contains("review", subcommandNames);
        Assert.Contains("collect", subcommandNames);
        Assert.Contains("diagnose", subcommandNames);
        Assert.Contains("approved", subcommandNames);
        Assert.Equal(4, subcommandNames.Count);
    }

    [Fact]
    public void RootCommand_HasExpectedGlobalOptions()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();

        // Act
        var optionNames = rootCommand.Options.Select(o => o.Name).ToList();

        // Assert
        Assert.Contains("verbose", optionNames);
        Assert.Contains("format", optionNames);
    }

    [Fact]
    public void ReviewCommand_HasExpectedOptions()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var reviewCommand = rootCommand.Subcommands.First(c => c.Name == "review");

        // Act
        var optionNames = reviewCommand.Options.Select(o => o.Name).ToList();

        // Assert
        Assert.Contains("full-urls", optionNames);
        Assert.Contains("detailed-timing", optionNames);
        Assert.Contains("show-detailed-info", optionNames);
    }

    [Fact]
    public void ApprovedCommand_HasExpectedOptions()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var approvedCommand = rootCommand.Subcommands.First(c => c.Name == "approved");

        // Act
        var optionNames = approvedCommand.Options.Select(o => o.Name).ToList();

        // Assert
        Assert.Contains("full-urls", optionNames);
        Assert.Contains("detailed-timing", optionNames);
        Assert.Contains("show-detailed-info", optionNames);
    }

    [Fact]
    public void CollectCommand_Exists()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();

        // Act
        var collectCommand = rootCommand.Subcommands.FirstOrDefault(c => c.Name == "collect");

        // Assert
        Assert.NotNull(collectCommand);
        Assert.Equal("collect", collectCommand.Name);
    }

    [Fact]
    public void DiagnoseCommand_HasExpectedArgument()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var diagnoseCommand = rootCommand.Subcommands.First(c => c.Name == "diagnose");

        // Act
        var argumentNames = diagnoseCommand.Arguments.Select(a => a.Name).ToList();

        // Assert
        Assert.Contains("id", argumentNames);
        Assert.Single(argumentNames);
    }

    [Theory]
    [InlineData("review")]
    [InlineData("collect")]
    [InlineData("diagnose", "123")]
    [InlineData("approved")]
    public void ValidCommands_ParseWithoutErrors(params string[] args)
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var parser = new Parser(rootCommand);

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.False(parseResult.Errors.Any(), 
            $"Command '{string.Join(" ", args)}' should parse without errors. Errors: {string.Join(", ", parseResult.Errors.Select(e => e.Message))}");
    }

    [Theory]
    [InlineData("invalid-command")]
    [InlineData("review", "--invalid-option")]
    [InlineData("diagnose")]  // Missing required argument
    [InlineData("diagnose", "invalid-id")]  // Invalid argument type
    public void InvalidCommands_ParseWithErrors(params string[] args)
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var parser = new Parser(rootCommand);

        // Act
        var parseResult = parser.Parse(args);

        // Assert
        Assert.True(parseResult.Errors.Any(), 
            $"Command '{string.Join(" ", args)}' should have parse errors");
    }

    [Fact]
    public void VerboseOption_HasCorrectAliases()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var verboseOption = rootCommand.Options.First(o => o.Name == "verbose");

        // Act & Assert
        Assert.True(verboseOption.HasAlias("--verbose"));
        Assert.True(verboseOption.HasAlias("-v"));
    }

    [Fact]
    public void FormatOption_HasCorrectAliases()
    {
        // Arrange
        var rootCommand = Program.CreateRootCommand();
        var formatOption = rootCommand.Options.First(o => o.Name == "format");

        // Act & Assert
        Assert.True(formatOption.HasAlias("--format"));
        Assert.True(formatOption.HasAlias("-f"));
    }
}
