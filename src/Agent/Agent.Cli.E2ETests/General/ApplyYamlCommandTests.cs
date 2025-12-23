// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;

namespace Agent.Cli.Tests.E2E.General;

/// <summary>
/// E2E tests for 'srectl apply-yaml' command with mock backend
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ApplyYamlCommandTests : AgentCommandTestBase
{
    public ApplyYamlCommandTests(MockWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ApplyYaml_SingleToolDocument_AppliesSuccessfully()
    {
        // Arrange: Create a single tool YAML file
        var toolName = "single-tool-test";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(toolName);

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/tool.yaml", toolYaml);

        // Act: Apply the YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/tool.yaml");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);

        // Verify tool was created using list command
        var listResult = await Runner.RunAsync("tool", "list", "--name", toolName);
        Assert.True(listResult.Success);
        Assert.Contains(toolName, listResult.StandardOutput);
    }

    [Fact]
    public async Task ApplyYaml_SingleAgentDocument_AppliesSuccessfully()
    {
        // Arrange: Create a single agent YAML file
        var agentName = "single-agent-test";
        var agentYaml = TestYamlHelper.GetMinimalAgentV2(agentName);

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/agent.yaml", agentYaml);

        // Act: Apply the YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/agent.yaml");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);

        // Verify agent was created using list command
        var listResult = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.True(listResult.Success);
        Assert.Contains(agentName, listResult.StandardOutput);
    }

    [Fact]
    public async Task ApplyYaml_SingleCommonPromptDocument_AppliesSuccessfully()
    {
        // Arrange: Create a single common prompt YAML file
        var promptName = "single-prompt-test";
        var promptYaml = TestYamlHelper.GetMinimalCommonPromptV2(promptName);

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/prompt.yaml", promptYaml);

        // Act: Apply the YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/prompt.yaml");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);

        // Verify common prompt was created using get command
        var getResult = await Runner.RunAsync("common-prompt", "get", "--name", promptName);
        Assert.True(getResult.Success, $"Get command failed: {getResult.Output}");
        Assert.Contains(promptName, getResult.StandardOutput);
    }

    [Fact]
    public async Task ApplyYaml_MultiDocument_AppliesAllResources()
    {
        // Arrange: Create a multi-document YAML file with tool, agent, and common prompt
        var tool1Name = "multi-tool-1";
        var tool2Name = "multi-tool-2";
        var agentName = "multi-agent";

        var multiDocYaml = $@"{TestYamlHelper.GetMinimalKustoToolV2(tool1Name)}
---
{TestYamlHelper.GetMinimalKustoToolV2(tool2Name)}
---
{TestYamlHelper.GetMinimalAgentV2(agentName, tools: new List<string> { tool1Name, tool2Name })}";

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/multi.yaml", multiDocYaml);

        // Act: Apply the multi-document YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/multi.yaml");

        // Assert: Command should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Document 1/3", result.StandardOutput);
        Assert.Contains("Document 2/3", result.StandardOutput);
        Assert.Contains("Document 3/3", result.StandardOutput);
        Assert.Contains("Summary: 3 succeeded, 0 failed", result.StandardOutput);

        // Verify all resources were created
        var tool1List = await Runner.RunAsync("tool", "list", "--name", tool1Name);
        Assert.True(tool1List.Success);
        Assert.Contains(tool1Name, tool1List.StandardOutput);

        var tool2List = await Runner.RunAsync("tool", "list", "--name", tool2Name);
        Assert.True(tool2List.Success);
        Assert.Contains(tool2Name, tool2List.StandardOutput);

        var agentList = await Runner.RunAsync("agent", "list", "--name", agentName);
        Assert.True(agentList.Success);
        Assert.Contains(agentName, agentList.StandardOutput);
    }

    [Fact]
    public async Task ApplyYaml_SingleDocument_NoDocumentHeaderInOutput()
    {
        // Arrange: Create a single tool YAML file
        var toolName = "single-doc-test";
        var toolYaml = TestYamlHelper.GetMinimalKustoToolV2(toolName);

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/tool.yaml", toolYaml);

        // Act: Apply the YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/tool.yaml");

        // Assert: Command should succeed without document header or summary
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.DoesNotContain("Document 1/1", result.StandardOutput);
        Assert.DoesNotContain("Summary:", result.StandardOutput);
        Assert.Contains("applied successfully", result.StandardOutput);
    }

    [Fact]
    public async Task ApplyYaml_FileNotFound_ReturnsError()
    {
        // Act: Try to apply a non-existent file
        var result = await Runner.RunAsync("apply-yaml", "--file", "non-existent.yaml");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("File not found", result.Output);
    }

    [Fact]
    public async Task ApplyYaml_InvalidYamlFormat_ReturnsError()
    {
        // Arrange: Create an invalid YAML file
        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/invalid.yaml", "this is not valid yaml: [[[");

        // Act: Apply the invalid YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/invalid.yaml");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("Failed to", result.Output);
    }

    [Fact]
    public async Task ApplyYaml_UnsupportedResourceKind_ReturnsError()
    {
        // Arrange: Create a YAML file with unsupported kind
        var unsupportedYaml = @"api_version: azuresre.ai/v2
kind: UnsupportedKind
metadata:
  name: test-resource
spec:
  something: value";

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/unsupported.yaml", unsupportedYaml);

        // Act: Apply the YAML file with unsupported kind
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/unsupported.yaml");

        // Assert: Command should fail with appropriate error
        Assert.False(result.Success);
        Assert.Contains("Failed to apply YAML content", result.Output);
    }

    [Fact]
    public async Task ApplyYaml_UnsupportedApiVersion_ReturnsError()
    {
        // Arrange: Create a tool with unsupported API version
        var unsupportedYaml = @"api_version: unsupported.ai/v99
kind: ExtendedAgentTool
metadata:
  name: test-tool
spec:
  type: KustoTool
  connector: test
  database: test
  query: SELECT 1";

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/unsupported-version.yaml", unsupportedYaml);

        // Act: Apply the YAML file with unsupported version
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/unsupported-version.yaml");

        // Assert: Command should fail with version error
        Assert.False(result.Success);
        Assert.Contains("Failed to apply YAML content", result.Output);
    }

    [Fact]
    public async Task ApplyYaml_MultiDocument_PartialFailure_ReturnsCorrectSummary()
    {
        // Arrange: Create multi-document with valid and invalid resources
        var validTool = TestYamlHelper.GetMinimalKustoToolV2("valid-tool");
        var invalidYaml = @"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: invalid-tool
spec:
  type: InvalidToolType
  description: test";

        var multiDocYaml = $@"{validTool}
---
{invalidYaml}";

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/partial.yaml", multiDocYaml);

        // Act: Apply the multi-document YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/partial.yaml");

        // Assert: Command should fail overall but show partial success
        Assert.False(result.Success);
        Assert.Contains("Summary:", result.StandardOutput);
        Assert.Contains("1 succeeded, 1 failed", result.StandardOutput);

        // Verify valid tool was created
        var listResult = await Runner.RunAsync("tool", "list", "--name", "valid-tool");
        Assert.True(listResult.Success);
        Assert.Contains("valid-tool", listResult.StandardOutput);
    }

    [Fact]
    public async Task ApplyYaml_EmptyFile_ReturnsError()
    {
        // Arrange: Create an empty YAML file
        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/empty.yaml", "");

        // Act: Apply the empty YAML file
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/empty.yaml");

        // Assert: Command should fail
        Assert.False(result.Success);
        Assert.Contains("No valid YAML documents found", result.Output);
    }

    [Fact]
    public async Task ApplyYaml_UpdateExistingResource_Succeeds()
    {
        // Arrange: Create and apply initial tool
        var toolName = "updateable-tool";
        var initialYaml = TestYamlHelper.GetKustoToolV2(toolName, "Initial description");

        Runner.CreateDirectory("manifests");
        Runner.CreateFile("manifests/tool.yaml", initialYaml);
        await Runner.RunAsync("apply-yaml", "--file", "manifests/tool.yaml");

        // Update the YAML
        var updatedYaml = TestYamlHelper.GetKustoToolV2(toolName, "Updated description");
        Runner.CreateFile("manifests/tool.yaml", updatedYaml);

        // Act: Apply the updated YAML
        var result = await Runner.RunAsync("apply-yaml", "--file", "manifests/tool.yaml");

        // Assert: Update should succeed
        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);
    }
}
