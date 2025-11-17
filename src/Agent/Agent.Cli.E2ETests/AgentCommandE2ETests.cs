// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E;

/// <summary>
/// E2E tests for agent commands
/// </summary>
public class AgentCommandE2ETests : CliTestBase
{
    public AgentCommandE2ETests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Create")]
    public async Task AgentCreate_WithValidName_CreatesSuccessfully()
    {
        Output.WriteLine("=== TEST: AgentCreate_WithValidName_CreatesSuccessfully ===");

        var agentName = GenerateTestName("TestAgent");
        Output.WriteLine($"Creating agent: {agentName}");

        var result = await RunCliCommand(
            "agent", "create",
            "--name", agentName,
            "--instructions", "This is a test agent created for end-to-end testing purposes to validate CLI functionality"
        );

        Assert.True(result.Success, $"Command failed with: {result.Error}");
        Assert.Contains("created successfully", result.Output, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("[SUCCESS] Agent created successfully");
        Output.WriteLine($"=== TEST PASSED: AgentCreate_WithValidName_CreatesSuccessfully ===\n");

        await DeleteTestAgentAsync(agentName);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Delete")]
    public async Task AgentDelete_ExistingAgent_RemovesFromServer()
    {
        Output.WriteLine("=== TEST: AgentDelete_ExistingAgent_RemovesFromServer ===");

        var agentName = await CreateTestAgentAsync();
        Output.WriteLine($"Agent created: {agentName}");

        var result = await RunCliCommand("agent", "delete", "--name", agentName);

        Assert.True(result.Success, $"Delete failed with: {result.Error}");
        Assert.Contains("deleted", result.Output, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("[SUCCESS] Agent deleted successfully");
        Output.WriteLine($"=== TEST PASSED: AgentDelete_ExistingAgent_RemovesFromServer ===\n");
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Validate")]
    public async Task AgentValidate_ExistingAgent_Succeeds()
    {
        Output.WriteLine("=== TEST: AgentValidate_ExistingAgent_Succeeds ===");

        var agentName = await CreateTestAgentAsync();
        Output.WriteLine($"Agent created: {agentName}");

        var result = await RunCliCommand("agent", "validate", "--name", agentName);

        Assert.True(result.Success, $"Validate failed with: {result.Error}");
        Assert.Contains("valid", result.Output, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("[SUCCESS] Agent validated successfully");
        Output.WriteLine($"=== TEST PASSED: AgentValidate_ExistingAgent_Succeeds ===\n");

        await DeleteTestAgentAsync(agentName);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Apply")]
    public async Task AgentApply_LocalAgent_DeploysToServer()
    {
        Output.WriteLine("=== TEST: AgentApply_LocalAgent_DeploysToServer ===");

        var agentName = GenerateTestName("TestAgent");
        Output.WriteLine($"Creating local agent: {agentName}");

        var createResult = await RunCliCommand(
            "agent", "create",
            "--name", agentName,
            "--instructions", "This is a test agent created for end-to-end testing purposes to validate CLI functionality"
        );
        Assert.True(createResult.Success, $"Create failed with: {createResult.Error}");

        var result = await RunCliCommand("agent", "apply", "--name", agentName);

        Assert.True(result.Success, $"Apply failed with: {result.Error}");
        Assert.Contains("apply successful", result.Output, StringComparison.OrdinalIgnoreCase);

        Output.WriteLine("[SUCCESS] Agent applied to server successfully");
        Output.WriteLine($"=== TEST PASSED: AgentApply_LocalAgent_DeploysToServer ===\n");

        await DeleteTestAgentAsync(agentName);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Test")]
    public async Task AgentTest_WithMessage_CreatesThread()
    {
        Output.WriteLine("=== TEST: AgentTest_WithMessage_CreatesThread ===");

        var agentName = await CreateTestAgentAsync();
        Output.WriteLine($"Agent created: {agentName}");

        var applyResult = await RunCliCommand("agent", "apply", "--name", agentName);
        Assert.True(applyResult.Success, $"Apply failed with: {applyResult.Error}");

        var result = await RunCliCommand(
            "agent", "test",
            "--name", agentName,
            "--message", "Hi"
        );

        Assert.True(result.Success, $"Test failed with: {result.Error}");
        Assert.NotEmpty(result.Output);

        Output.WriteLine("[SUCCESS] Agent test executed successfully");
        Output.WriteLine($"=== TEST PASSED: AgentTest_WithMessage_CreatesThread ===\n");

        await DeleteTestAgentAsync(agentName);
    }

    [Fact]
    [Trait("Category", "Agent")]
    [Trait("Command", "Diff")]
    public async Task AgentDiff_ModifiedAgent_ShowsDifferences()
    {
        Output.WriteLine("=== TEST: AgentDiff_ModifiedAgent_ShowsDifferences ===");

        var agentName = await CreateTestAgentAsync();
        Output.WriteLine($"Agent created: {agentName}");

        var applyResult = await RunCliCommand("agent", "apply", "--name", agentName);
        Assert.True(applyResult.Success, $"Apply failed with: {applyResult.Error}");

        var modifyResult = await RunCliCommand(
            "agent", "create",
            "--name", agentName,
            "--instructions", "This is modified instructions for testing the diff command functionality in E2E tests"
        );
        Assert.True(modifyResult.Success, $"Modify failed with: {modifyResult.Error}");

        var result = await RunCliCommand("agent", "diff", "--name", agentName);

        Assert.True(result.Success, $"Diff failed with: {result.Error}");
        Assert.NotEmpty(result.Output);

        Output.WriteLine("[SUCCESS] Agent diff executed successfully");
        Output.WriteLine($"=== TEST PASSED: AgentDiff_ModifiedAgent_ShowsDifferences ===\n");

        await DeleteTestAgentAsync(agentName);
    }
}
