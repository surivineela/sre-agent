// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// E2E tests for 'srectl tool delete' command with mock backend
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class DeleteCommandTests : AgentCommandTestBase
{
    public DeleteCommandTests(MockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ToolDelete_DeletesToolFromServer()
    {
        // Arrange: Create and apply a tool first
        var toolName = "tool-to-delete";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(toolName);

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", toolYaml);

        var applyResult = await Runner.RunAsync("tool", "apply", "--name", toolName);
        Assert.True(applyResult.Success, $"Failed to create tool: {applyResult.Output}");

        // Verify local file exists before deletion
        var localFilePath = Path.Combine(Runner.WorkingDirectory, "tools", $"{toolName}.yaml");
        Assert.True(File.Exists(localFilePath), "Local file should exist before deletion");

        // Configure ReadLine to respond 'yes' to cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "y";

        // Act: Delete the tool
        var result = await Runner.RunAsync("tool", "delete", "--name", toolName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("deleted successfully", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Local tool file deleted", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify tool was deleted from server
        var listResult = await Runner.RunAsync("tool", "list", "--name", toolName);
        Assert.False(listResult.Success);
        Assert.DoesNotContain(toolName, listResult.Output);

        // Verify local file was deleted
        Assert.False(File.Exists(localFilePath), "Local file should be deleted after confirming cleanup");
    }

    [Fact]
    public async Task ToolDelete_DryRun_DoesNotDeleteTool()
    {
        // Arrange: Create and apply a tool first
        var toolName = "tool-dry-run-delete";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(toolName);

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", toolYaml);

        var applyResult = await Runner.RunAsync("tool", "apply", "--name", toolName);
        Assert.True(applyResult.Success, $"Failed to create tool: {applyResult.Output}");

        // Act: Delete with dry-run flag
        var result = await Runner.RunAsync("tool", "delete", "--name", toolName, "--dry-run");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("dry run", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify tool still exists on server (dry run should not delete)
        var listResult = await Runner.RunAsync("tool", "list", "--name", toolName);
        Assert.True(listResult.Success);
        Assert.Contains(toolName, listResult.Output);
    }

    [Fact]
    public async Task ToolDelete_NonExistentTool_ReturnsSuccess()
    {
        // Act: Try to delete a non-existent tool
        var result = await Runner.RunAsync("tool", "delete", "--name", "non-existent-tool");

        // Assert: Command should succeed (deleting non-existent item is idempotent)
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("does not exist", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToolDelete_WithLocalFiles_ConfirmNo_PreservesLocalFiles()
    {
        // Arrange: Create and apply a tool first
        var toolName = "tool-preserve-local";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(toolName);

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", toolYaml);

        var applyResult = await Runner.RunAsync("tool", "apply", "--name", toolName);
        Assert.True(applyResult.Success, $"Failed to create tool: {applyResult.Output}");

        // Verify local file exists before deletion
        var localFilePath = Path.Combine(Runner.WorkingDirectory, "tools", $"{toolName}.yaml");
        Assert.True(File.Exists(localFilePath), "Local file should exist before deletion");

        // Configure ReadLine to respond 'no' to cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "n";

        // Act: Delete the tool
        var result = await Runner.RunAsync("tool", "delete", "--name", toolName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("deleted successfully", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Local configuration files preserved", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify local file still exists
        Assert.True(File.Exists(localFilePath), "Local file should be preserved after declining cleanup");
    }
}
