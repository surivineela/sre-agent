// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E.AgentCommands;

/// <summary>
/// E2E tests for 'srectl agent create' command with mock backend
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class CreateCommandTests : AgentCommandTestBase
{
    public CreateCommandTests(MockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AgentCreate_MinimalAgent_CreatesAgentSuccessfully()
    {
        // Arrange
        var agentName = "test-minimal-agent";

        // Act: Create a minimal agent
        var result = await Runner.RunAsync("agent", "create", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("created successfully", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify agent file was created locally
        var agentFilePath = Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml");
        Assert.True(File.Exists(agentFilePath), "Agent YAML file should be created");

        // Verify file contains correct structure
        var yamlContent = await File.ReadAllTextAsync(agentFilePath);
        Assert.Contains("api_version: azuresre.ai/v2", yamlContent);
        Assert.Contains($"name: {agentName}", yamlContent);
        Assert.Contains("kind: ExtendedAgent", yamlContent);
    }

    [Fact]
    public async Task AgentCreate_WithInstructions_IncludesInstructions()
    {
        // Arrange
        var agentName = "test-agent-with-instructions";
        var instructions = "This agent helps with testing and validation tasks";

        // Act: Create agent with custom instructions
        var result = await Runner.RunAsync("agent", "create", "--name", agentName, "--instructions", instructions);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);

        // Verify agent file contains instructions
        var agentFilePath = Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml");
        var yamlContent = await File.ReadAllTextAsync(agentFilePath);
        Assert.Contains(instructions, yamlContent);
    }

    [Fact]
    public async Task AgentCreate_WithTools_IncludesTools()
    {
        // Arrange: Create tools first
        var agentName = "test-agent-with-tools";
        var toolName1 = "test-tool-1";
        var toolName2 = "test-tool-2";

        // Create tools
        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName1}.yaml", TestYamlHelper.GetMinimalKustoToolV2(toolName1));
        Runner.CreateFile($"tools/{toolName2}.yaml", TestYamlHelper.GetMinimalKustoToolV2(toolName2));

        // Apply tools to server
        await Runner.RunAsync("tool", "apply", "--name", toolName1);
        await Runner.RunAsync("tool", "apply", "--name", toolName2);

        // Act: Create agent with tools
        var result = await Runner.RunAsync("agent", "create", "--name", agentName, "--tools", toolName1, "--tools", toolName2);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);

        // Verify agent file contains tools
        var agentFilePath = Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml");
        var yamlContent = await File.ReadAllTextAsync(agentFilePath);
        Assert.Contains(toolName1, yamlContent);
        Assert.Contains(toolName2, yamlContent);
        Assert.Contains("tools:", yamlContent);
    }

    [Fact]
    public async Task AgentCreate_WithMissingTool_ReturnsError()
    {
        // Arrange
        var agentName = "test-agent-missing-tool";
        var nonExistentTool = "non-existent-tool";

        // Act: Try to create agent with non-existent tool
        var result = await Runner.RunAsync("agent", "create", "--name", agentName, "--tools", nonExistentTool);

        // Assert: Command should fail
        Assert.False(result.Success, "Command should fail when tool doesn't exist");
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not available", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentCreate_WithHandoffs_IncludesHandoffs()
    {
        // Arrange
        var agentName = "test-agent-with-handoffs";
        var handoffAgent1 = "handoff-agent-1";
        var handoffAgent2 = "handoff-agent-2";

        // Act: Create agent with handoffs
        var result = await Runner.RunAsync(
            "agent", "create",
            "--name", agentName,
            "--handoffs", handoffAgent1,
            "--handoffs", handoffAgent2);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);

        // Verify agent file contains handoffs
        var agentFilePath = Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml");
        var yamlContent = await File.ReadAllTextAsync(agentFilePath);
        Assert.Contains(handoffAgent1, yamlContent);
        Assert.Contains(handoffAgent2, yamlContent);
        Assert.Contains("handoffs:", yamlContent);
    }

    [Fact]
    public async Task AgentCreate_WithTemperature_IncludesTemperature()
    {
        // Arrange
        var agentName = "test-agent-with-temperature";
        var temperature = 0.7;

        // Act: Create agent with temperature setting
        var result = await Runner.RunAsync("agent", "create", "--name", agentName, "--temperature", temperature.ToString());

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);

        // Verify agent file contains temperature
        var agentFilePath = Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml");
        var yamlContent = await File.ReadAllTextAsync(agentFilePath);
        Assert.Contains($"temperature: {temperature}", yamlContent);
    }

    [Fact]
    public async Task AgentCreate_WithVanillaMode_IncludesVanillaMode()
    {
        // Arrange
        var agentName = "test-agent-vanilla";

        // Act: Create agent with vanilla mode enabled
        var result = await Runner.RunAsync("agent", "create", "--name", agentName, "--vanilla-mode");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Equal(0, result.ExitCode);

        // Verify agent file contains vanilla mode setting
        var agentFilePath = Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml");
        var yamlContent = await File.ReadAllTextAsync(agentFilePath);
        Assert.Contains("enableVanillaMode: true", yamlContent);
    }

    [Fact]
    public async Task AgentCreate_DuplicateNames_OverwritesExisting()
    {
        // Arrange: Create an agent first
        var agentName = "test-duplicate-agent";
        var firstInstructions = "First version instructions";
        var secondInstructions = "Second version instructions";

        // Create first agent
        var firstResult = await Runner.RunAsync("agent", "create", "--name", agentName, "--instructions", firstInstructions);
        Assert.True(firstResult.Success, "First agent creation should succeed");

        // Act: Create agent with same name but different instructions
        var secondResult = await Runner.RunAsync("agent", "create", "--name", agentName, "--instructions", secondInstructions);

        // Assert: Second creation should succeed (overwrite)
        Assert.True(secondResult.Success, $"Second creation failed: {secondResult.Output}");
        Assert.Equal(0, secondResult.ExitCode);

        // Verify file was overwritten with new instructions
        var agentFilePath = Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml");
        var yamlContent = await File.ReadAllTextAsync(agentFilePath);
        Assert.Contains(secondInstructions, yamlContent);
        Assert.DoesNotContain(firstInstructions, yamlContent);
    }

    [Fact]
    public async Task AgentCreate_CreatesCorrectDirectoryStructure()
    {
        // Arrange
        var agentName = "test-directory-structure";

        // Act: Create agent
        var result = await Runner.RunAsync("agent", "create", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");

        // Verify directory structure
        var agentDir = Path.Combine(Runner.WorkingDirectory, "agents", agentName);
        var agentFilePath = Path.Combine(agentDir, $"{agentName}.yaml");

        Assert.True(Directory.Exists(agentDir), "Agent directory should be created");
        Assert.True(File.Exists(agentFilePath), "Agent YAML file should be in agent directory");
    }

    [Fact]
    public async Task AgentCreate_ServerValidationSuccess_DisplaysSuccessMessage()
    {
        // Arrange
        var agentName = "test-validation-success";

        // Act: Create agent (server validation is done via dry-run)
        var result = await Runner.RunAsync("agent", "create", "--name", agentName);

        // Assert: Command should succeed and show validation success
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("created successfully", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Next Steps", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
