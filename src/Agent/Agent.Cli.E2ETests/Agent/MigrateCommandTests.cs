// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E.AgentCommands;

/// <summary>
/// E2E tests for 'srectl agent migrate' command
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class MigrateCommandTests : AgentCommandTestBase
{
    public MigrateCommandTests(MockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AgentMigrate_SingleV1Agent_MigratesToV2()
    {
        // Arrange: Create a V1 agent YAML file
        var agentName = "test-migrate-single";
        var v1AgentYaml = GetMinimalAgentV1(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", v1AgentYaml);

        // Verify it's V1 before migration
        var beforeContent = File.ReadAllText(Path.Combine(Runner.WorkingDirectory, "agents", $"{agentName}.yaml"));
        Assert.Contains("agent.platform.ai/v1", beforeContent);

        // Act: Migrate the agent
        var result = await Runner.RunAsync("agent", "migrate", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Migrated to V2", result.Output);
        Assert.Contains("Migration Summary", result.Output);
        Assert.Matches(@"Migrated\s*:\s*1", result.Output); // Use regex to handle padding

        // Verify the file was actually migrated to V2
        var afterContent = File.ReadAllText(Path.Combine(Runner.WorkingDirectory, "agents", $"{agentName}.yaml"));
        Assert.Contains("azuresre.ai/v2", afterContent);
        Assert.DoesNotContain("agent.platform.ai/v1", afterContent);
        Assert.Contains("kind: ExtendedAgent", afterContent);
    }

    [Fact]
    public async Task AgentMigrate_DryRun_DoesNotModifyFile()
    {
        // Arrange: Create a V1 agent YAML file
        var agentName = "test-migrate-dryrun";
        var v1AgentYaml = GetMinimalAgentV1(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", v1AgentYaml);

        var originalContent = File.ReadAllText(Path.Combine(Runner.WorkingDirectory, "agents", $"{agentName}.yaml"));

        // Act: Migrate with --dry-run flag
        var result = await Runner.RunAsync("agent", "migrate", "--name", agentName, "--dry-run");

        // Assert: Command should succeed and show what would be migrated
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Migrated to V2", result.Output);
        Assert.Contains("DRY RUN", result.Output);
        Assert.Contains("This was a dry run. No files were modified.", result.Output);

        // Verify the file was NOT modified
        var afterContent = File.ReadAllText(Path.Combine(Runner.WorkingDirectory, "agents", $"{agentName}.yaml"));
        Assert.Equal(originalContent, afterContent);
        Assert.Contains("agent.platform.ai/v1", afterContent);
    }

    [Fact]
    public async Task AgentMigrate_AlreadyV2_Skips()
    {
        // Arrange: Create a V2 agent (already migrated)
        var agentName = "test-migrate-already-v2";
        var v2AgentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("agents");
        Runner.CreateFile($"agents/{agentName}.yaml", v2AgentYaml);

        // Act: Try to migrate an already-V2 agent
        var result = await Runner.RunAsync("agent", "migrate", "--name", agentName);

        // Assert: Command should succeed but skip the file
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Already V2 format", result.Output);
        Assert.Matches(@"Skipped\s*:\s*1", result.Output);
        Assert.Matches(@"Migrated\s*:\s*0", result.Output);
    }

    [Fact]
    public async Task AgentMigrate_All_MigratesMultipleFiles()
    {
        // Arrange: Create multiple V1 agents and one V2 agent
        Runner.CreateDirectory("agents");

        var v1Agent1 = "migrate-all-v1-1";
        var v1Agent2 = "migrate-all-v1-2";
        var v2Agent = "migrate-all-v2";

        Runner.CreateFile($"agents/{v1Agent1}.yaml", GetMinimalAgentV1(v1Agent1));
        Runner.CreateFile($"agents/{v1Agent2}.yaml", GetMinimalAgentV1(v1Agent2));
        Runner.CreateFile($"agents/{v2Agent}.yaml", TestYamlHelper.GetMinimalAgentV2(v2Agent));

        // Act: Migrate all agents
        var result = await Runner.RunAsync("agent", "migrate", "--all");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Migrating 3 agent(s)", result.Output);
        Assert.Matches(@"Migrated\s*:\s*2", result.Output);
        Assert.Matches(@"Skipped\s*:\s*1", result.Output);

        // Verify V1 agents were migrated
        var agent1Content = File.ReadAllText(Path.Combine(Runner.WorkingDirectory, "agents", $"{v1Agent1}.yaml"));
        var agent2Content = File.ReadAllText(Path.Combine(Runner.WorkingDirectory, "agents", $"{v1Agent2}.yaml"));

        Assert.Contains("azuresre.ai/v2", agent1Content);
        Assert.Contains("azuresre.ai/v2", agent2Content);
    }

    [Fact]
    public async Task AgentMigrate_SubdirectoryStructure_Migrates()
    {
        // Arrange: Create a V1 agent in subdirectory structure
        var agentName = "test-migrate-subdir";
        var v1AgentYaml = GetMinimalAgentV1(agentName);

        Runner.CreateDirectory($"agents/{agentName}");
        Runner.CreateFile($"agents/{agentName}/{agentName}.yaml", v1AgentYaml);

        // Act: Migrate the agent
        var result = await Runner.RunAsync("agent", "migrate", "--name", agentName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Migrated to V2", result.Output);

        // Verify the file was migrated
        var afterContent = File.ReadAllText(Path.Combine(Runner.WorkingDirectory, "agents", agentName, $"{agentName}.yaml"));
        Assert.Contains("azuresre.ai/v2", afterContent);
    }

    [Fact]
    public async Task AgentMigrate_NonExistentAgent_ReturnsError()
    {
        // Arrange: No agents directory or files
        Runner.CreateDirectory("agents");

        // Act: Try to migrate non-existent agent
        var result = await Runner.RunAsync("agent", "migrate", "--name", "non-existent-agent");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("Agent file not found", result.Output);
    }

    [Fact]
    public async Task AgentMigrate_NoAgentsDirectory_ReturnsError()
    {
        // Act: Try to migrate when agents directory doesn't exist
        var result = await Runner.RunAsync("agent", "migrate", "--name", "any-agent");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("No agents directory found", result.Output);
    }

    [Fact]
    public async Task AgentMigrate_BothNameAndAll_ReturnsError()
    {
        // Arrange: Create agents directory
        Runner.CreateDirectory("agents");

        // Act: Try to use both --name and --all
        var result = await Runner.RunAsync("agent", "migrate", "--name", "some-agent", "--all");

        // Assert: Command should fail with validation error
        Assert.False(result.Success);
        Assert.Contains("Cannot specify both --name and --all", result.Output);
    }

    [Fact]
    public async Task AgentMigrate_NeitherNameNorAll_ReturnsError()
    {
        // Arrange: Create agents directory
        Runner.CreateDirectory("agents");

        // Act: Try to migrate without specifying --name or --all
        var result = await Runner.RunAsync("agent", "migrate");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("Please specify --name or --all", result.Output);
    }

    /// <summary>
    /// Helper to generate a minimal V1 agent YAML
    /// </summary>
    private static string GetMinimalAgentV1(string name)
    {
        return $@"api_version: agent.platform.ai/v1
kind: AgentConfiguration
metadata:
  name: {name}
  owner: someone
spec:
  instructions: |
    This is a test V1 agent that needs migration.
  tools: []
  handoffs: []
";
    }
}
