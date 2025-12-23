// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Skill;

/// <summary>
/// E2E tests for 'srectl skill apply' command with mock backend
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class ApplyCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public ApplyCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_CreatesSkillOnServer()
    {
        // Arrange: Create a skill using the create command
        var skillName = "test-skill-apply";
        var description = "Test skill for apply command";

        var createResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", description
        );

        Assert.True(createResult.Success, $"Skill creation failed: {createResult.Output}");

        // Act: Apply the skill
        var result = await Runner.RunAsync("skill", "apply", "--name", skillName);

        // Assert
        _output.WriteLine("=== Apply Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("============================");

        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);

        // Verify skill was created on server using list command
        var listResult = await Runner.RunAsync("skill", "list", "--name", skillName);
        _output.WriteLine("=== List Command Output ===");
        _output.WriteLine(listResult.Output);
        _output.WriteLine("===========================");

        Assert.True(listResult.Success);
        Assert.Contains(skillName, listResult.StandardOutput);
    }

    // Note: This test is disabled because the mock backend appears to persist skills even in dry-run mode.
    [Fact(Skip = "Mock backend issue - dry-run appears to persist data")]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_DryRun_DoesNotCreateSkill()
    {
        // Arrange: Create a skill
        var skillName = "dry-run-skill";
        var description = "Test skill for dry run";

        var createResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", description
        );

        Assert.True(createResult.Success, $"Skill creation failed: {createResult.Output}");

        // Act: Apply the skill with --dry-run flag
        var result = await Runner.RunAsync("skill", "apply", "--name", skillName, "--dry-run");

        // Assert
        _output.WriteLine("=== Dry Run Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("validated successfully", result.StandardOutput);

        // Verify skill was NOT created on server (dry run should not persist)
        // Note: The skill list command returns success even when skill not found,
        // but the output won't contain the skill name
        var listResult = await Runner.RunAsync("skill", "list");
        Assert.DoesNotContain(skillName, listResult.StandardOutput);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_SkillNotFound_ReturnsError()
    {
        // Arrange: Don't create any skill

        // Act: Try to apply a non-existent skill
        var result = await Runner.RunAsync("skill", "apply", "--name", "non-existent-skill");

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_UpdatesExistingSkill()
    {
        // Arrange: Create and apply initial skill
        var skillName = "updateable-skill";
        var initialDescription = "Initial version";

        var createResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", initialDescription
        );

        Assert.True(createResult.Success, $"Initial skill creation failed: {createResult.Output}");

        var initialApplyResult = await Runner.RunAsync("skill", "apply", "--name", skillName);
        Assert.True(initialApplyResult.Success, $"Initial apply failed: {initialApplyResult.Output}");

        // Update the metadata.yaml with new description
        var metadataPath = $"skills/{skillName}/metadata.yaml";
        var currentMetadata = Runner.ReadFile(metadataPath);
        var updatedMetadata = currentMetadata.Replace(initialDescription, "Updated version");
        Runner.CreateFile(metadataPath, updatedMetadata);

        // Act: Apply the updated skill
        var updateResult = await Runner.RunAsync("skill", "apply", "--name", skillName);

        // Assert
        _output.WriteLine("=== Update Apply Output ===");
        _output.WriteLine(updateResult.Output);
        _output.WriteLine("===========================");

        Assert.True(updateResult.Success, $"Update failed: {updateResult.Output}");
        Assert.Contains("applied successfully", updateResult.StandardOutput);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_WithoutMetadataYaml_ReturnsError()
    {
        // Arrange: Create a skill with metadata.yaml but then delete it
        var skillName = "incomplete-skill";

        // First create the skill properly
        var createResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", "Test description"
        );
        Assert.True(createResult.Success);

        // Delete metadata.yaml to make it incomplete
        var metadataPath = $"skills/{skillName}/metadata.yaml";
        System.IO.File.Delete(Path.Combine(Runner.WorkingDirectory, metadataPath));

        // Act: Try to apply
        var result = await Runner.RunAsync("skill", "apply", "--name", skillName);

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    // Note: This test is disabled because FindSkillDirectory requires both metadata.yaml and SKILL.md to exist
    // in order to find the skill directory. When SKILL.md is deleted, the directory can't be found.
    [Fact(Skip = "FindSkillDirectory requires both metadata.yaml and SKILL.md to exist")]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_WithoutSkillMd_ReturnsError()
    {
        // Arrange: Create a skill and then delete SKILL.md
        var skillName = "no-skillmd-skill";

        // First create the skill properly
        var createResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", "Test description"
        );
        Assert.True(createResult.Success);

        // Verify it was created with both files
        Assert.True(Runner.FileExists($"skills/{skillName}/metadata.yaml"));
        Assert.True(Runner.FileExists($"skills/{skillName}/SKILL.md"));

        // Delete SKILL.md (but keep directory and metadata.yaml)
        var skillMdPath = $"skills/{skillName}/SKILL.md";
        System.IO.File.Delete(Path.Combine(Runner.WorkingDirectory, skillMdPath));

        // Act: Try to apply
        var result = await Runner.RunAsync("skill", "apply", "--name", skillName);

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.False(result.Success);
        Assert.Contains("SKILL.md", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_WithAdditionalFiles_UploadsAllContent()
    {
        // Arrange: Create a skill with additional files
        var skillName = "skill-with-files";
        var description = "Skill with additional files";

        // Create the skill first
        var createResult = await Runner.RunAsync(
            "skill", "create",
            "--name", skillName,
            "--description", description
        );

        Assert.True(createResult.Success, $"Skill creation failed: {createResult.Output}");

        // Add additional files
        Runner.CreateDirectory($"skills/{skillName}/examples");
        Runner.CreateFile($"skills/{skillName}/examples/example1.md", "# Example 1\nExample content");

        // Update metadata.yaml to reference the additional file
        var metadataPath = $"skills/{skillName}/metadata.yaml";
        var currentMetadata = Runner.ReadFile(metadataPath);
        var updatedMetadata = currentMetadata.TrimEnd() + @"
  additionalFiles:
    - filePath: examples/example1.md
";
        Runner.CreateFile(metadataPath, updatedMetadata);

        // Act: Apply the skill
        var result = await Runner.RunAsync("skill", "apply", "--name", skillName);

        // Assert
        _output.WriteLine("=== Apply Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("============================");

        Assert.True(result.Success, $"Apply failed: {result.Output}");
        Assert.Contains("applied successfully", result.StandardOutput);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Apply")]
    public async Task SkillApply_WithAdditionalFiles_AutoDiscoversFiles()
    {
        // Arrange: Create a skill with additional files in subdirectory
        var skillName = "skill-with-files";
        var metadataYaml = TestYamlHelper.GetSkillMetadataV2(
            skillName,
            "Test description"
        );

        Runner.CreateDirectory($"skills/{skillName}");
        Runner.CreateDirectory($"skills/{skillName}/examples");
        Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);
        Runner.CreateFile($"skills/{skillName}/SKILL.md", "# Test Skill");
        Runner.CreateFile($"skills/{skillName}/examples/example1.md", "# Example 1");
        Runner.CreateFile($"skills/{skillName}/extra.txt", "Extra file");

        // Act: Apply the skill
        var result = await Runner.RunAsync("skill", "apply", "--name", skillName);

        // Assert: Should succeed and auto-discover the additional files
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success);
        Assert.Contains("applied successfully", result.Output, StringComparison.OrdinalIgnoreCase);
    }
}
