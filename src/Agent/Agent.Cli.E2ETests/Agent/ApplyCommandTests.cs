// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E.AgentCommands;

/// <summary>
/// E2E tests for 'srectl agent apply' command with mock backend
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ApplyCommandTests : AgentCommandTestBase
{
    public ApplyCommandTests(MockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AgentApply_CreatesAgentOnServer()
    {
        // Arrange: Create an agent YAML file
        var agentName = "test-apply-agent";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        // Act: Apply the agent
        var result = await Runner.RunAsync("agent", "apply", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify agent was created on server using E2E command
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.True(listResult.Success);
        Assert.Contains(agentName, listResult.StandardOutput);
    }

    [Fact]
    public async Task AgentApply_DryRun_DoesNotCreateAgent()
    {
        // Arrange: Create an agent YAML file
        var agentName = "dry-run-agent";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        // Act: Apply the agent with --dry-run flag
        var result = await Runner.RunAsync("agent", "apply", "--name", agentName, "--dry-run");

        // Assert: Command should succeed with validation message
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("validated successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify agent was NOT created on server (dry run should not persist)
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.False(listResult.Success);
        Assert.DoesNotContain(agentName, listResult.StandardOutput);
    }

    [Fact]
    public async Task AgentApply_AgentNotFound_ReturnsError()
    {
        // Act: Try to apply a non-existent agent
        var result = await Runner.RunAsync("agent", "apply", "--name", "non-existent-agent");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentApply_UpdatesExistingAgent()
    {
        // Arrange: Create and apply initial agent
        var agentName = "updateable-agent";
        var initialYaml = TestYamlHelper.GetMinimalAgentV2(
            agentName,
            "Initial version");

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", initialYaml);
        await Runner.RunAsync("agent", "apply", "--name", agentName);

        // Update the YAML with new instructions
        var updatedYaml = TestYamlHelper.GetMinimalAgentV2(
            agentName,
            "Updated version");
        Runner.CreateFile($"agents/{agentName}.yaml", updatedYaml);

        // Act: Apply the updated agent
        var result = await Runner.RunAsync("agent", "apply", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify agent was updated on server
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName, "--detail");
        Assert.True(listResult.Success);
        Assert.Contains("Updated version", listResult.StandardOutput);
    }

    [Fact]
    public async Task AgentApply_WithTools_CreatesAgentWithTools()
    {
        // Arrange: Create agent YAML with tools
        var agentName = "agent-with-tools";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(
            agentName,
            "Agent with tools",
            tools: new List<string> { "tool1", "tool2" });

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        // Act: Apply the agent
        var result = await Runner.RunAsync("agent", "apply", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify agent was created on server with tools
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName, "--detail");
        Assert.True(listResult.Success);
        Assert.Contains("tool1", listResult.StandardOutput);
        Assert.Contains("tool2", listResult.StandardOutput);
    }

    [Fact]
    public async Task AgentApply_WithHandoffs_CreatesAgentWithHandoffs()
    {
        // Arrange: Create agent YAML with handoffs
        var agentName = "agent-with-handoffs";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(
            agentName,
            "Agent with handoffs",
            handoffs: new List<string> { "agent1", "agent2" });

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", agentYaml);

        // Act: Apply the agent
        var result = await Runner.RunAsync("agent", "apply", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        // Verify agent was created on server with handoffs
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName, "--detail");
        Assert.True(listResult.Success);
        Assert.Contains("agent1", listResult.StandardOutput);
        Assert.Contains("agent2", listResult.StandardOutput);
    }
}
