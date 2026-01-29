// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Tests.E2E.Helpers;
using Agent.Cli.Tests.E2E.MockBackend;
using Xunit;
using Xunit.Abstractions;

namespace Agent.Cli.Tests.E2E.Skill;

/// <summary>
/// E2E tests for 'srectl skill migrate' command.
/// Tests the migration of skills from metadata.yaml format to SKILL.md frontmatter format.
/// </summary>
[Collection(AgentCommandTestCollection.Name)]
public class MigrateCommandTests : AgentCommandTestBase
{
    private readonly ITestOutputHelper _output;

    public MigrateCommandTests(MockWebApplicationFactory factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
        _output.WriteLine($"Test working directory: {Runner.WorkingDirectory}");
    }

    #region Single Skill Migration Tests

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_SingleSkill_MigratesToFrontmatterFormat()
    {
        // Arrange: Create a skill using the old metadata.yaml format
        var skillName = "migrate-single-skill";
        var metadataYaml = TestYamlHelper.GetSkillMetadataV2(skillName, "A skill to migrate");
        var skillMd = $"# {skillName}\n\nThis is the skill content.";

        Runner.CreateDirectory($"skills/{skillName}");
        Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);
        Runner.CreateFile($"skills/{skillName}/SKILL.md", skillMd);

        // Verify old format files exist
        Assert.True(Runner.FileExists($"skills/{skillName}/metadata.yaml"));
        Assert.True(Runner.FileExists($"skills/{skillName}/SKILL.md"));

        // Act: Run migrate command
        var result = await Runner.RunAsync("skill", "migrate", "--name", skillName);

        // Assert
        _output.WriteLine("=== Migrate Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("==============================");

        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Migrated", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify metadata.yaml was deleted
        Assert.False(Runner.FileExists($"skills/{skillName}/metadata.yaml"),
            "metadata.yaml should be deleted after migration");

        // Verify SKILL.md now has frontmatter
        var migratedContent = Runner.ReadFile($"skills/{skillName}/SKILL.md");
        _output.WriteLine("=== Migrated SKILL.md ===");
        _output.WriteLine(migratedContent);
        _output.WriteLine("=========================");

        Assert.StartsWith("---", migratedContent);
        Assert.Contains($"name: {skillName}", migratedContent);
        Assert.Contains("description:", migratedContent);
        Assert.Contains("# " + skillName, migratedContent); // Original markdown content preserved
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_AlreadyMigratedSkill_SkipsAndReportsCorrectly()
    {
        // Arrange: Create a skill already in frontmatter format
        var skillName = "already-migrated-skill";
        var skillMdWithFrontmatter = TestYamlHelper.GetSkillMdWithFrontmatter(
            skillName,
            "Already in new format",
            new[] { "Tool1", "Tool2" });

        Runner.CreateDirectory($"skills/{skillName}");
        Runner.CreateFile($"skills/{skillName}/SKILL.md", skillMdWithFrontmatter);

        // Act: Run migrate command
        var result = await Runner.RunAsync("skill", "migrate", "--name", skillName);

        // Assert
        _output.WriteLine("=== Migrate Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("==============================");

        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Already using frontmatter format", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Skipped", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_NonExistentSkill_ReturnsError()
    {
        // Arrange: Create skills directory but not the specific skill
        Runner.CreateDirectory("skills");

        // Act: Try to migrate a skill that doesn't exist
        var result = await Runner.RunAsync("skill", "migrate", "--name", "nonexistent-skill");

        // Assert
        _output.WriteLine("=== Command Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Dry Run Tests

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_DryRun_DoesNotModifyFiles()
    {
        // Arrange: Create a skill using the old format
        var skillName = "dryrun-skill";
        var metadataYaml = TestYamlHelper.GetSkillMetadataV2(skillName, "Dry run test skill");
        var originalSkillMd = $"# {skillName}\n\nOriginal content.";

        Runner.CreateDirectory($"skills/{skillName}");
        Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);
        Runner.CreateFile($"skills/{skillName}/SKILL.md", originalSkillMd);

        // Act: Run migrate with --dry-run
        var result = await Runner.RunAsync("skill", "migrate", "--name", skillName, "--dry-run");

        // Assert
        _output.WriteLine("=== Dry Run Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("======================");

        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Would migrate", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify files were NOT modified
        Assert.True(Runner.FileExists($"skills/{skillName}/metadata.yaml"),
            "metadata.yaml should still exist after dry run");

        var skillMdContent = Runner.ReadFile($"skills/{skillName}/SKILL.md");
        Assert.DoesNotContain("---", skillMdContent.TrimStart().Substring(0, Math.Min(10, skillMdContent.Length)));
        Assert.Equal(originalSkillMd, skillMdContent);
    }

    #endregion

    #region Migrate All Tests

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_All_MigratesMultipleSkills()
    {
        // Arrange: Create multiple skills in old format
        var skills = new[] { "migrate-all-skill1", "migrate-all-skill2", "migrate-all-skill3" };

        foreach (var skillName in skills)
        {
            var metadataYaml = TestYamlHelper.GetSkillMetadataV2(skillName, $"Description for {skillName}");
            var skillMd = $"# {skillName}\n\nContent for {skillName}.";

            Runner.CreateDirectory($"skills/{skillName}");
            Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);
            Runner.CreateFile($"skills/{skillName}/SKILL.md", skillMd);
        }

        // Act: Run migrate with --all
        var result = await Runner.RunAsync("skill", "migrate", "--all");

        // Assert
        _output.WriteLine("=== Migrate All Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("==========================");

        Assert.True(result.Success, $"Command failed: {result.Output}");

        // Verify all skills were migrated
        foreach (var skillName in skills)
        {
            Assert.Contains(skillName, result.Output);
            Assert.False(Runner.FileExists($"skills/{skillName}/metadata.yaml"),
                $"metadata.yaml should be deleted for {skillName}");

            var migratedContent = Runner.ReadFile($"skills/{skillName}/SKILL.md");
            Assert.StartsWith("---", migratedContent);
        }

        // Check summary shows correct count
        Assert.Contains("Migrated", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_All_DryRun_ShowsAllSkillsWithoutModifying()
    {
        // Arrange: Create multiple skills
        var skills = new[] { "dryrun-all-skill1", "dryrun-all-skill2" };

        foreach (var skillName in skills)
        {
            var metadataYaml = TestYamlHelper.GetSkillMetadataV2(skillName, $"Description for {skillName}");
            var skillMd = $"# {skillName}\n\nContent.";

            Runner.CreateDirectory($"skills/{skillName}");
            Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);
            Runner.CreateFile($"skills/{skillName}/SKILL.md", skillMd);
        }

        // Act: Run migrate with --all --dry-run
        var result = await Runner.RunAsync("skill", "migrate", "--all", "--dry-run");

        // Assert
        _output.WriteLine("=== Dry Run All Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("==========================");

        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("DRY RUN", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify files were NOT modified
        foreach (var skillName in skills)
        {
            Assert.True(Runner.FileExists($"skills/{skillName}/metadata.yaml"),
                $"metadata.yaml should still exist for {skillName}");
        }
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_All_MixedFormats_SkipsAlreadyMigrated()
    {
        // Arrange: Create skills in both old and new formats
        var oldFormatSkill = "mixed-old-format";
        var newFormatSkill = "mixed-new-format";

        // Old format skill
        Runner.CreateDirectory($"skills/{oldFormatSkill}");
        Runner.CreateFile($"skills/{oldFormatSkill}/metadata.yaml",
            TestYamlHelper.GetSkillMetadataV2(oldFormatSkill, "Old format"));
        Runner.CreateFile($"skills/{oldFormatSkill}/SKILL.md", $"# {oldFormatSkill}\n\nContent.");

        // New format skill (already has frontmatter)
        Runner.CreateDirectory($"skills/{newFormatSkill}");
        Runner.CreateFile($"skills/{newFormatSkill}/SKILL.md",
            TestYamlHelper.GetSkillMdWithFrontmatter(newFormatSkill, "New format"));

        // Act
        var result = await Runner.RunAsync("skill", "migrate", "--all");

        // Assert
        _output.WriteLine("=== Mixed Format Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("===========================");

        Assert.True(result.Success, $"Command failed: {result.Output}");

        // Old format should be migrated
        Assert.False(Runner.FileExists($"skills/{oldFormatSkill}/metadata.yaml"));

        // Summary should show both migrated and skipped
        Assert.Contains("Migrated", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Skipped", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Validation Tests

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_NoSkillsDirectory_ReturnsError()
    {
        // Arrange: Ensure no skills directory exists (clean workspace)
        // The test runner creates a clean workspace, so skills/ won't exist initially
        // But we need to make sure it's really not there
        if (Directory.Exists(Path.Combine(Runner.WorkingDirectory, "skills")))
        {
            Directory.Delete(Path.Combine(Runner.WorkingDirectory, "skills"), true);
        }

        // Act
        var result = await Runner.RunAsync("skill", "migrate", "--all");

        // Assert
        _output.WriteLine("=== No Skills Dir Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("============================");

        Assert.False(result.Success);
        Assert.Contains("No skills directory found", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Migrate")]
    public async Task SkillMigrate_ExtendedSkillFormat_PreservesAllMetadata()
    {
        // Arrange: Create an extended skill using the full CLI metadata.yaml format
        // This is the format used when creating skills via 'srectl skill create' or 'srectl skill sync'
        var skillName = "extended-format-skill";
        var metadataYaml = @"api_version: azuresre.ai/v2
kind: Skill
metadata:
  name: extended-format-skill
  owner: test-owner
  tags:
    - test
    - e2e
spec:
  description: |
    This is an extended skill with the full metadata.yaml format.
    It includes api_version, kind, metadata, and spec sections.
  tools:
    - ExtendedTool1
    - ExtendedTool2
    - ExtendedTool3
";
        var skillMd = @"# Extended Format Skill

## Overview
This skill was created using the extended metadata.yaml format.

## Capabilities
- Full CLI metadata support
- Multiple tools
";

        Runner.CreateDirectory($"skills/{skillName}");
        Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);
        Runner.CreateFile($"skills/{skillName}/SKILL.md", skillMd);

        // Act
        var result = await Runner.RunAsync("skill", "migrate", "--name", skillName);

        // Assert
        _output.WriteLine("=== Extended Format Migration Output ===");
        _output.WriteLine(result.Output);
        _output.WriteLine("========================================");

        Assert.True(result.Success, $"Command failed: {result.Output}");
        Assert.Contains("Migrated", result.Output, StringComparison.OrdinalIgnoreCase);

        // Verify metadata.yaml was deleted
        Assert.False(Runner.FileExists($"skills/{skillName}/metadata.yaml"),
            "metadata.yaml should be deleted after migration");

        // Verify SKILL.md now has frontmatter with all metadata preserved
        var migratedContent = Runner.ReadFile($"skills/{skillName}/SKILL.md");
        _output.WriteLine("=== Migrated SKILL.md Content ===");
        _output.WriteLine(migratedContent);
        _output.WriteLine("=================================");

        // Verify frontmatter structure - must start with ---
        Assert.StartsWith("---", migratedContent);

        // Extract frontmatter section for more precise validation
        var frontmatterEndIndex = migratedContent.IndexOf("---", 3);
        Assert.True(frontmatterEndIndex > 0, "Frontmatter should have closing ---");
        var frontmatter = migratedContent.Substring(0, frontmatterEndIndex + 3);

        // Verify nested metadata block with api_version and kind
        Assert.Contains("metadata:", frontmatter);
        Assert.Contains("  api_version: azuresre.ai/v2", frontmatter); // Indented under metadata
        Assert.Contains("  kind: Skill", frontmatter); // Indented under metadata

        // Verify top-level fields (not nested under metadata)
        Assert.Contains($"name: {skillName}", frontmatter);
        Assert.Contains("description:", frontmatter);

        // Tools preserved as list
        Assert.Contains("tools:", frontmatter);
        Assert.Contains("  - ExtendedTool1", frontmatter);
        Assert.Contains("  - ExtendedTool2", frontmatter);
        Assert.Contains("  - ExtendedTool3", frontmatter);

        // Original markdown content preserved after frontmatter
        var markdownContent = migratedContent.Substring(frontmatterEndIndex + 3).TrimStart();
        Assert.Contains("# Extended Format Skill", markdownContent);
        Assert.Contains("## Overview", markdownContent);
        Assert.Contains("## Capabilities", markdownContent);
    }

    #endregion

    #region Sync Warning Tests

    [Fact]
    [Trait("Category", "Skill")]
    [Trait("Command", "Sync")]
    public async Task SkillSync_WithExistingMetadataYaml_ShowsWarning()
    {
        // Arrange: Create a skill on the server first via apply
        var skillName = "sync-metadata-warning-skill";
        var metadataYaml = TestYamlHelper.GetSkillMetadataV2(skillName, "Skill to test sync warning");
        var skillMd = "# Sync Warning Test Skill\n\nThis skill tests the sync metadata.yaml warning.";

        // Create skill with old format and apply it to get it on the server
        Runner.CreateDirectory($"skills/{skillName}");
        Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);
        Runner.CreateFile($"skills/{skillName}/SKILL.md", skillMd);

        var applyResult = await Runner.RunAsync("skill", "apply", "--name", skillName);
        Assert.True(applyResult.Success, $"Initial apply failed: {applyResult.Output}");

        // Re-create the metadata.yaml file to simulate user still having old format
        // (sync will detect and delete it, but should warn first)
        Runner.CreateFile($"skills/{skillName}/metadata.yaml", metadataYaml);

        // Verify metadata.yaml exists before sync
        Assert.True(Runner.FileExists($"skills/{skillName}/metadata.yaml"));

        // Act: Sync the skill from server
        var syncResult = await Runner.RunAsync("skill", "sync", "--name", skillName);

        // Assert
        _output.WriteLine("=== Sync Command Output ===");
        _output.WriteLine(syncResult.Output);
        _output.WriteLine("===========================");

        Assert.True(syncResult.Success, $"Sync failed: {syncResult.Output}");
        Assert.Contains("synced", syncResult.Output, StringComparison.OrdinalIgnoreCase);

        // Verify warning about metadata.yaml deletion is shown
        Assert.Contains("metadata.yaml", syncResult.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deleted", syncResult.Output, StringComparison.OrdinalIgnoreCase);

        // Verify metadata is now in frontmatter
        Assert.Contains("frontmatter", syncResult.Output, StringComparison.OrdinalIgnoreCase);

        // Verify SKILL.md has frontmatter after sync
        var skillMdContent = Runner.ReadFile($"skills/{skillName}/SKILL.md");
        Assert.StartsWith("---", skillMdContent);
        Assert.Contains($"name: {skillName}", skillMdContent);

        // Verify metadata.yaml was deleted
        Assert.False(Runner.FileExists($"skills/{skillName}/metadata.yaml"),
            "metadata.yaml should be deleted after sync");
    }

    #endregion
}
