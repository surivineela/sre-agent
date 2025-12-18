// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E.AgentCommands;

/// <summary>
/// E2E tests for 'srectl agent delete' command with mock backend
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class DeleteCommandTests : AgentCommandTestBase
{
    public DeleteCommandTests(MockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AgentDelete_DeletesAgentFromServer()
    {
        // Arrange: Create and apply an agent first
        var agentName = "agent-to-delete";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        var applyResult = await Runner.RunAsync("agent", "apply", "--name", agentName);
        Assert.True(applyResult.Success, $"Failed to create agent: {applyResult.Output}");

        // Verify local file exists before deletion
        var localFilePath = Path.Combine(Runner.WorkingDirectory, "agents", $"{agentName}.yaml");
        Assert.True(File.Exists(localFilePath), "Local file should exist before deletion");

        // Configure ReadLine to respond 'yes' to cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "y";

        // Act: Delete the agent
        var result = await Runner.RunAsync("agent", "delete", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("deleted successfully", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Local", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify agent was deleted from server
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.False(listResult.Success);
        Assert.DoesNotContain(agentName, listResult.Output);

        // Verify local file was deleted
        Assert.False(File.Exists(localFilePath), "Local file should be deleted after confirming cleanup");
    }

    [Fact]
    public async Task AgentDelete_NonExistentAgent_ReturnsSuccess()
    {
        // Act: Try to delete a non-existent agent
        var result = await Runner.RunAsync("agent", "delete", "--name", "non-existent-agent");

        // Assert: Command should succeed (deleting non-existent item is idempotent)
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("does not exist", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentDelete_WithLocalFiles_ConfirmNo_PreservesLocalFiles()
    {
        // Arrange: Create and apply an agent first
        var agentName = "agent-preserve-local";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        var applyResult = await Runner.RunAsync("agent", "apply", "--name", agentName);
        Assert.True(applyResult.Success, $"Failed to create agent: {applyResult.Output}");

        var localFilePath = Path.Combine(Runner.WorkingDirectory, "agents", $"{agentName}.yaml");
        Assert.True(File.Exists(localFilePath), "Local file should exist");

        // Configure ReadLine to respond 'no' to cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "n";

        // Act: Delete the agent
        var result = await Runner.RunAsync("agent", "delete", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("deleted", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify agent was deleted from server
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.False(listResult.Success);

        // Verify local file still exists
        Assert.True(File.Exists(localFilePath), "Local file should still exist after declining cleanup");
    }

    [Fact]
    public async Task AgentDelete_WithoutLocalFiles_DeletesFromServerOnly()
    {
        // Arrange: Create and apply an agent
        var agentName = "agent-no-local";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        var applyResult = await Runner.RunAsync("agent", "apply", "--name", agentName);
        Assert.True(applyResult.Success, $"Failed to create agent: {applyResult.Output}");

        // Delete the local file before running delete command
        var localFilePath = Path.Combine(Runner.WorkingDirectory, "agents", $"{agentName}.yaml");
        File.Delete(localFilePath);

        // Act: Delete the agent
        var result = await Runner.RunAsync("agent", "delete", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("deleted successfully", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify agent was deleted from server
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.False(listResult.Success);
    }

    [Fact]
    public async Task AgentDelete_SubdirectoryStructure_DeletesDirectory()
    {
        // Arrange: Create agent in subdirectory structure
        var agentName = "agent-subdir";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        var agentDir = Path.Combine(Runner.WorkingDirectory, "agents", agentName);
        Directory.CreateDirectory(agentDir);
        Runner.CreateFile($"agents/{agentName}/{agentName}.yaml", agentYaml);

        var applyResult = await Runner.RunAsync("agent", "apply", "--name", agentName);
        Assert.True(applyResult.Success, $"Failed to create agent: {applyResult.Output}");

        // Configure ReadLine to respond 'yes' to cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "y";

        // Act: Delete the agent
        var result = await Runner.RunAsync("agent", "delete", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("deleted successfully", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify agent was deleted from server
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.False(listResult.Success);

        // Verify entire directory was deleted
        Assert.False(Directory.Exists(agentDir), "Agent directory should be deleted after confirming cleanup");
    }

    [Fact]
    public async Task AgentDelete_VerifyCleanupPromptAppears()
    {
        // Arrange: Create and apply an agent
        var agentName = "agent-cleanup-prompt";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        var applyResult = await Runner.RunAsync("agent", "apply", "--name", agentName);
        Assert.True(applyResult.Success, $"Failed to create agent: {applyResult.Output}");

        // Configure ReadLine to respond 'no' to cleanup prompt
        Agent.Cli.Helpers.ConsoleUI.ReadLineHandler = () => "n";

        // Act: Delete the agent
        var result = await Runner.RunAsync("agent", "delete", "--name", agentName);

        // Assert: Output should contain cleanup prompt
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Local", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delete", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
