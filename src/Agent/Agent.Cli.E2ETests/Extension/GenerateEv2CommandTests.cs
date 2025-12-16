// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Extension;

/// <summary>
/// E2E tests for 'srectl extension generate-ev2' command.
/// Tests both Bicep-only and full EV2 artifact generation scenarios.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class GenerateEv2CommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public GenerateEv2CommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Extension")]
    [Trait("Command", "GenerateEv2")]
    public async Task GenerateEv2_BicepOnly_CreatesTemplates()
    {
        // Arrange - Create test tools and agents folders
        Runner.CreateDirectory("tools");
        Runner.CreateFile("tools/test-tool.yaml", TestYamlHelper.GetMinimalKustoToolV2("test-tool"));

        Runner.CreateDirectory("agents");
        Runner.CreateFile("agents/test-agent.yaml", TestYamlHelper.GetMinimalAgentV2("test-agent"));

        // Act - Generate Bicep templates only (no EV2 options)
        var result = await Runner.RunAsync(
            "extension", "generate-ev2",
            "--tools-folder", "tools",
            "--agent-folder", "agents",
            "--output", "output-bicep"
        );

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify BicepTemplates folder was created
        Assert.True(Runner.DirectoryExists("output-bicep/BicepTemplates"), "BicepTemplates folder should exist");
        Assert.True(Runner.FileExists("output-bicep/BicepTemplates/modules/sreagentExtensionFile.bicep"),
            "sreagentExtensionFile.bicep should exist");

        // Verify EV2 artifacts were NOT created
        Assert.False(Runner.FileExists("output-bicep/serviceModel.json"), "serviceModel.json should NOT exist without EV2 options");
        Assert.False(Runner.FileExists("output-bicep/configurationSettings.jsonc"), "configurationSettings.jsonc should NOT exist without EV2 options");
        Assert.False(Runner.FileExists("output-bicep/Deploy-Extension.ps1"), "Deploy-Extension.ps1 should NOT exist without EV2 options");

        // Verify ARM templates were generated
        Assert.True(Runner.DirectoryExists("output-bicep/ArmTemplates"), "ArmTemplates folder should exist");
    }

    [Fact]
    [Trait("Category", "Extension")]
    [Trait("Command", "GenerateEv2")]
    public async Task GenerateEv2_WithAllOptions_CreatesFullEv2Artifacts()
    {
        // Arrange - Create test tools and agents folders
        Runner.CreateDirectory("tools");
        Runner.CreateFile("tools/test-tool.yaml", TestYamlHelper.GetMinimalKustoToolV2("test-tool"));

        Runner.CreateDirectory("agents");
        Runner.CreateFile("agents/test-agent.yaml", TestYamlHelper.GetMinimalAgentV2("test-agent"));

        // Act - Generate full EV2 artifacts with all options
        var result = await Runner.RunAsync(
            "extension", "generate-ev2",
            "--tools-folder", "tools",
            "--agent-folder", "agents",
            "--output", "output-ev2",
            "--service-identifier", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            "--service-group", "TestServiceGroup",
            "--environment", "Test",
            "--tenant-id", "11111111-2222-3333-4444-555555555555",
            "--subscription-key", "TestSubscriptionKey",
            "--subscription-id", "66666666-7777-8888-9999-000000000000",
            "--resource-group", "TestResourceGroup",
            "--agent-name", "TestAgent"
        );

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify BicepTemplates folder was created
        Assert.True(Runner.DirectoryExists("output-ev2/BicepTemplates"), "BicepTemplates folder should exist");
        Assert.True(Runner.FileExists("output-ev2/BicepTemplates/modules/sreagentExtensionFile.bicep"),
            "sreagentExtensionFile.bicep should exist");

        // Verify EV2 artifacts were created
        Assert.True(Runner.FileExists("output-ev2/serviceModel.json"), "serviceModel.json should exist");
        Assert.True(Runner.FileExists("output-ev2/serviceGroupSpecification.json"), "serviceGroupSpecification.json should exist");
        Assert.True(Runner.FileExists("output-ev2/configurationSettings.jsonc"), "configurationSettings.jsonc should exist");
        Assert.True(Runner.FileExists("output-ev2/Deploy-Extension.ps1"), "Deploy-Extension.ps1 should exist");
        Assert.True(Runner.FileExists("output-ev2/RolloutSpec.json"), "RolloutSpec.json should exist");
        Assert.True(Runner.FileExists("output-ev2/ScopeBindings.json"), "ScopeBindings.json should exist");

        // Verify placeholders were replaced in configurationSettings.jsonc
        var configContent = Runner.ReadFile("output-ev2/configurationSettings.jsonc");
        _output.WriteLine("=== Configuration Settings ===");
        _output.WriteLine(configContent);
        _output.WriteLine("==============================");

        Assert.Contains("\"environment\": \"Test\"", configContent);
        Assert.Contains("\"subscriptionKey\": \"TestSubscriptionKey\"", configContent);
        Assert.Contains("\"subscriptionId\": \"66666666-7777-8888-9999-000000000000\"", configContent);
        Assert.Contains("\"resourceGroup\": \"TestResourceGroup\"", configContent);
        Assert.Contains("\"agentName\": \"TestAgent\"", configContent);
        Assert.DoesNotContain("{{ENVIRONMENT}}", configContent);
        Assert.DoesNotContain("{{AGENT_NAME}}", configContent);

        // Verify placeholders were replaced in serviceModel.json
        var serviceModelContent = Runner.ReadFile("output-ev2/serviceModel.json");
        Assert.Contains("\"serviceIdentifier\": \"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", serviceModelContent);
        Assert.Contains("\"serviceGroup\": \"TestServiceGroup\"", serviceModelContent);
        Assert.DoesNotContain("{{SERVICE_IDENTIFIER}}", serviceModelContent);
        Assert.DoesNotContain("{{SERVICE_GROUP}}", serviceModelContent);

        // Verify placeholders were replaced in serviceGroupSpecification.json
        var serviceGroupSpecContent = Runner.ReadFile("output-ev2/serviceGroupSpecification.json");
        Assert.Contains("\"name\": \"TestServiceGroup\"", serviceGroupSpecContent);
        Assert.Contains("\"identifier\": \"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", serviceGroupSpecContent);
        Assert.DoesNotContain("{{SERVICE_IDENTIFIER}}", serviceGroupSpecContent);
        Assert.DoesNotContain("{{SERVICE_GROUP}}", serviceGroupSpecContent);

        // Verify ARM templates were generated
        Assert.True(Runner.DirectoryExists("output-ev2/ArmTemplates"), "ArmTemplates folder should exist");
    }

    [Fact]
    [Trait("Category", "Extension")]
    [Trait("Command", "GenerateEv2")]
    public async Task GenerateEv2_WithEmptyFolders_Succeeds()
    {
        // Arrange - Create empty tools and agents folders
        Runner.CreateDirectory("tools");
        Runner.CreateDirectory("agents");

        // Act
        var result = await Runner.RunAsync(
            "extension", "generate-ev2",
            "--tools-folder", "tools",
            "--agent-folder", "agents",
            "--output", "output-empty"
        );

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed even with empty folders. Exit code: {result.ExitCode}");
        Assert.True(Runner.DirectoryExists("output-empty/BicepTemplates"), "BicepTemplates folder should exist");
    }

    [Fact]
    [Trait("Category", "Extension")]
    [Trait("Command", "GenerateEv2")]
    public async Task GenerateEv2_NonExistentToolsFolder_Fails()
    {
        // Arrange - Create only agents folder
        Runner.CreateDirectory("agents");

        // Act
        var result = await Runner.RunAsync(
            "extension", "generate-ev2",
            "--tools-folder", "nonexistent-tools",
            "--agent-folder", "agents",
            "--output", "output"
        );

        // Assert
        Assert.False(result.Success, "Command should fail when tools folder doesn't exist");
        Assert.Contains("does not exist", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Extension")]
    [Trait("Command", "GenerateEv2")]
    public async Task GenerateEv2_NonExistentAgentFolder_Fails()
    {
        // Arrange - Create only tools folder
        Runner.CreateDirectory("tools");

        // Act
        var result = await Runner.RunAsync(
            "extension", "generate-ev2",
            "--tools-folder", "tools",
            "--agent-folder", "nonexistent-agents",
            "--output", "output"
        );

        // Assert
        Assert.False(result.Success, "Command should fail when agent folder doesn't exist");
        Assert.Contains("does not exist", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
