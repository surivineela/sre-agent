// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Tests.E2E.Skill;

/// <summary>
/// In-process tests for 'srectl skill create' command.
/// These tests are fast, debuggable, and don't require spawning processes.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class CreateCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public CreateCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Create")]
    public async Task SkillCreate_MinimalParameters_CreatesSkillDirectory()
    {
        // Arrange
        var skillName = "test-skill-minimal";
        var description = "Test skill for E2E testing";

        // Act
        var result = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", description
        );

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command should succeed. Exit code: {result.ExitCode}, Error: {result.StandardError}");

        // Verify the metadata.yaml file was created
        var expectedMetadataPath = $"skills/{skillName}/metadata.yaml";
        Assert.True(Runner.FileExists(expectedMetadataPath), $"metadata.yaml should exist at {expectedMetadataPath}");

        // Verify the SKILL.md file was created
        var expectedSkillMdPath = $"skills/{skillName}/SKILL.md";
        Assert.True(Runner.FileExists(expectedSkillMdPath), $"SKILL.md should exist at {expectedSkillMdPath}");

        // Verify the YAML content
        var yamlContent = Runner.ReadFile(expectedMetadataPath);
        _output.WriteLine("=== metadata.yaml Content ===");
        _output.WriteLine(yamlContent);
        _output.WriteLine("=============================");

        // Parse and validate YAML structure
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

        // Validate required fields
        Assert.True(yamlDict.ContainsKey("api_version"), "YAML should contain api_version");
        Assert.True(yamlDict.ContainsKey("kind"), "YAML should contain kind");
        Assert.Equal("Skill", yamlDict["kind"].ToString());

        // Validate metadata
        Assert.True(yamlDict.ContainsKey("metadata"), "YAML should contain metadata");
        var metadata = yamlDict["metadata"] as Dictionary<object, object>;
        Assert.NotNull(metadata);
        Assert.Equal(skillName, metadata["name"].ToString());

        // Validate spec
        Assert.True(yamlDict.ContainsKey("spec"), "YAML should contain spec");
        var spec = yamlDict["spec"] as Dictionary<object, object>;
        Assert.NotNull(spec);
        Assert.True(spec.ContainsKey("description"), "Spec should contain description");
        Assert.Contains(description, spec["description"].ToString());

        // Verify SKILL.md content
        var skillMdContent = Runner.ReadFile(expectedSkillMdPath);
        _output.WriteLine("=== SKILL.md Content ===");
        _output.WriteLine(skillMdContent);
        _output.WriteLine("========================");

        Assert.Contains(skillName, skillMdContent);
        Assert.Contains("# ", skillMdContent); // Should have markdown header

        // Verify success message in output
        Assert.Contains("created", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(skillName, result.Output);
    }

    // Note: skill create command doesn't support --owner and --tags options
    // These would need to be added manually to metadata.yaml after creation

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Create")]
    public async Task SkillCreate_DuplicateName_ReturnsError()
    {
        // Arrange
        var skillName = "duplicate-skill";
        var description = "First skill";

        // Create the skill first time
        var firstResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", description
        );

        Assert.True(firstResult.Success, "First skill creation should succeed");

        // Act: Try to create the same skill again
        var secondResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", "Second skill"
        );

        // Assert
        _output.WriteLine("=== Second Command Output ===");
        _output.WriteLine(secondResult.Output);
        _output.WriteLine("=============================");

        Assert.False(secondResult.Success, "Second skill creation should fail");
        Assert.Contains("already exists", secondResult.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Create")]
    public async Task SkillCreate_InvalidName_ReturnsError()
    {
        // Arrange - only test names that are actually invalid
        var invalidNames = new[] { "Invalid Name", "invalid@name" };

        foreach (var invalidName in invalidNames)
        {
            _output.WriteLine($"Testing invalid name: {invalidName}");

            // Act
            var result = await Runner.RunAsync(
                "skill", "create",
                "--name", invalidName,
                "--description", "Test description"
            );

            // Assert
            _output.WriteLine($"=== Output for '{invalidName}' ===");
            _output.WriteLine(result.Output);
            _output.WriteLine("===================================");

            Assert.False(result.Success, $"Command should fail for invalid name '{invalidName}'");
            Assert.Contains("invalid", result.Output, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Note: skill create command doesn't support --output-path option
    // Skills are always created in the 'skills' directory

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Create")]
    public async Task SkillCreate_MissingRequiredName_ReturnsError()
    {
        // Act: Try to create without --name
        var resultNoName = await Runner.RunAsync(
            "skill", "create",
            "--description", "Test description"
        );

        // Assert
        _output.WriteLine("=== Output without --name ===");
        _output.WriteLine(resultNoName.Output);
        _output.WriteLine("==============================");

        Assert.False(resultNoName.Success, "Command should fail without --name");
        Assert.Contains("required", resultNoName.Output, StringComparison.OrdinalIgnoreCase);
    }
}
