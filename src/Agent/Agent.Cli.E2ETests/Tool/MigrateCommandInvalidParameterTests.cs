// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// Tests for invalid parameter validation in 'srectl tool migrate' command.
/// Validates error handling for mutually exclusive parameters.
/// </summary>
[Collection("ToolTests")]
public class MigrateCommandInvalidParameterTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly CliTestRunner _cli;

    public MigrateCommandInvalidParameterTests(ITestOutputHelper output)
    {
        _output = output;
        _cli = new CliTestRunner();
        _output.WriteLine($"Test working directory: {_cli.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task ToolMigrate_BothNameAndAll_ReturnsError()
    {
        // Act - both --name and --all provided (mutually exclusive)
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", "TestTool",
            "--all"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when both --name and --all are provided");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Invalid parameter: Cannot use both --name and --all together", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task ToolMigrate_NeitherNameNorAll_ReturnsError()
    {
        // Act - neither --name nor --all provided
        var result = await _cli.RunAsync(
            "tool", "migrate"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail when neither --name nor --all is provided");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Invalid parameter: Must specify either --name or --all", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Tool")]
    [Trait("Command", "Migrate")]
    [Trait("Type", "Validation")]
    public async Task ToolMigrate_UnrecognizedOption_ReturnsError()
    {
        // Act - unrecognized option
        var result = await _cli.RunAsync(
            "tool", "migrate",
            "--name", "TestTool",
            "--invalid-option", "some-value"
        );

        // Assert
        _output.WriteLine(result.Output);
        Assert.False(result.Success, "Command should fail with unrecognized option");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unrecognized command or arguments: '--invalid-option'", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _cli.Dispose();
    }
}
