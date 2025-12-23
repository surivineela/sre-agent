// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E.Tool;

/// <summary>
/// E2E tests for 'srectl tool apply' command with mock backend
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ApplyCommandTests : AgentCommandTestBase
{
    public ApplyCommandTests(MockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ToolApply_CreatesToolOnServer()
    {
        // Arrange: Create a tool YAML file
        var toolName = "test-tool";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(toolName);

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", toolYaml);

        // Act: Apply the tool
        var result = await Runner.RunAsync("tool", "apply", "--name", toolName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);

        // Verify tool was created on server using E2E command
        var listResult = await Runner.RunAsync("tool", "list", "--name", toolName);
        Assert.True(listResult.Success);
        Assert.Contains(toolName, listResult.StandardOutput);
    }

    [Fact]
    public async Task ToolApply_DryRun_DoesNotCreateTool()
    {
        // Arrange: Create a tool YAML file
        var toolName = "dry-run-tool";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(toolName);

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", toolYaml);

        // Act: Apply the tool with --dry-run flag
        var result = await Runner.RunAsync("tool", "apply", "--name", toolName, "--dry-run");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("validated successfully", result.StandardOutput);

        // Verify tool was NOT created on server (dry run should not persist)
        var listResult = await Runner.RunAsync("tool", "list", "--name", toolName);
        Assert.False(listResult.Success);
        Assert.DoesNotContain(toolName, listResult.StandardOutput);
    }

    [Fact]
    public async Task ToolApply_ToolNotFound_ReturnsError()
    {
        // Arrange: Don't create any tool file

        // Act: Try to apply a non-existent tool
        var result = await Runner.RunAsync("tool", "apply", "--name", "non-existent-tool");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("Tool file not found", result.Output);
    }

    [Fact]
    public async Task ToolApply_UpdatesExistingTool()
    {
        // Arrange: Create and apply initial tool
        var toolName = "updateable-tool";
        var initialYaml = TestYamlHelper.GetKustoToolV2(
            toolName,
            "Initial version",
            query: "SELECT * FROM Table1");

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", initialYaml);
        await Runner.RunAsync("tool", "apply", "--name", toolName);

        // Update the YAML with new description
        var updatedYaml = TestYamlHelper.GetKustoToolV2(
            toolName,
            "Updated version",
            query: "SELECT * FROM Table2");
        Runner.CreateFile($"tools/{toolName}.yaml", updatedYaml);

        // Act: Apply the updated tool
        var result = await Runner.RunAsync("tool", "apply", "--name", toolName);

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);

        // Verify tool exists on server (updated successfully)
        var listResult = await Runner.RunAsync("tool", "list", "--name", toolName);
        Assert.True(listResult.Success);
        Assert.Contains(toolName, listResult.StandardOutput);
    }

    [Fact]
    public async Task ToolApply_InvalidYaml_ReturnsError()
    {
        // Arrange: Create a malformed YAML file
        var toolName = "invalid-yaml-tool";
        var invalidYaml = @"
apiVersion: v2
kind: ExtendedTool
metadata:
  name: invalid-yaml-tool
spec:
  # Invalid YAML - unbalanced brackets and malformed structure
  type: KustoTool
  connector: [unclosed
  properties:
    database: test
";

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", invalidYaml);

        // Act
        var result = await Runner.RunAsync("tool", "apply", "--name", toolName);

        // Assert: Command should fail when YAML is malformed
        Assert.False(result.Success);
        Assert.True(
            result.Output.Contains("Failed to parse", StringComparison.OrdinalIgnoreCase) ||
            result.Output.Contains("YAML", StringComparison.OrdinalIgnoreCase) ||
            result.Output.Contains("Failed to apply", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ToolApply_V1ToolFormat_ReturnsErrorRequiringMigration()
    {
        // Arrange: Create a V1 format tool (without apiVersion or with v1)
        var toolName = "v1-tool";
        var v1Yaml = @"
kind: ExtendedTool
metadata:
  name: v1-tool
spec:
  type: KustoTool
  connector: test-connector
  properties:
    database: testdb
    query: SELECT 1
";

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", v1Yaml);

        // Act
        var result = await Runner.RunAsync("tool", "apply", "--name", toolName);

        // Assert: This format (kind: ExtendedTool) is not recognized as valid V1 or V2
        Assert.False(result.Success);
        Assert.Contains("Unable to detect tool format", result.Output);
    }

    [Fact]
    public async Task ToolApply_DryRun_WithNonExistentTool_ReturnsError()
    {
        // Act: Try dry run with non-existent tool
        var result = await Runner.RunAsync("tool", "apply", "--name", "non-existent-tool", "--dry-run");

        // Assert: Command should fail even in dry-run when tool doesn't exist
        Assert.False(result.Success);
        Assert.Contains("Tool file not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToolApply_ToolWithInvalidSpec_ReturnsError()
    {
        // Arrange: Create a V2 tool but with incomplete/invalid spec
        var toolName = "invalid-spec-tool";
        var invalidSpecYaml = @"
apiVersion: v2
kind: ExtendedTool
metadata:
  name: invalid-spec-tool
spec:
  type: KustoTool
  connector: test-connector
  # Missing required properties - this creates an incomplete/invalid tool spec
";

        Runner.CreateDirectory("tools");
        Runner.CreateFile($"tools/{toolName}.yaml", invalidSpecYaml);

        // Act
        var result = await Runner.RunAsync("tool", "apply", "--name", toolName);

        // Assert: Command should fail when spec is invalid/incomplete
        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unable to detect tool format", result.Output);
    }

    [Fact]
    public async Task ToolApply_EmptyName_ReturnsError()
    {
        // Act: Try to apply with empty name
        var result = await Runner.RunAsync("tool", "apply", "--name", "");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.True(
            result.Output.Contains("Tool file not found", StringComparison.OrdinalIgnoreCase) ||
            result.Output.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ToolApply_NameMismatchWithYaml_ShowsWarningAndUsesYamlName()
    {
        // Arrange: Create a tool with a different name in YAML than what we'll use in --name
        var yamlName = "actual-tool-name";
        var cliName = "different-name";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(yamlName);

        Runner.CreateDirectory("tools");
        // Save the file with the CLI name, but the YAML content has a different name
        Runner.CreateFile($"tools/{cliName}.yaml", toolYaml);

        // Act: Apply the tool using the CLI name (which differs from YAML name)
        var result = await Runner.RunAsync("tool", "apply", "--name", cliName);

        // Assert: Command should succeed and show warning about name mismatch
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Warning", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cliName, result.Output);
        Assert.Contains(yamlName, result.Output);
        Assert.Contains("differs from YAML metadata.name", result.Output);
        Assert.Contains("Using name from YAML", result.Output);
        Assert.Contains("applied successfully", result.Output);

        // Verify the tool was created on server using the YAML name
        var listResult = await Runner.RunAsync("tool", "list", "--name", yamlName);
        Assert.True(listResult.Success, $"List command failed: {listResult.Output}");
        Assert.Contains(yamlName, listResult.Output);
    }
}
