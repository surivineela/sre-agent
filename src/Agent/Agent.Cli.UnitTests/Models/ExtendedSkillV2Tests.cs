// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class ExtendedSkillV2Tests
{
    #region ParseYaml Tests

    [Fact]
    public void ParseYaml_WithValidMinimalYaml_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: Skill
metadata:
  name: test-skill
  owner: test-owner
spec:
  description: A test skill
";

        // Act
        var skill = ExtendedSkillV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("azuresre.ai/v2", skill.ApiVersion);
        Assert.Equal("Skill", skill.Kind);
        Assert.NotNull(skill.Metadata);
        Assert.Equal("test-skill", skill.Metadata.Name);
        Assert.Equal("test-owner", skill.Metadata.Owner);
        Assert.NotNull(skill.Spec);
        Assert.Equal("A test skill", skill.Spec.Description);
        Assert.Null(skill.Spec.SkillContent); // SkillContent is not serialized in YAML
    }

    [Fact]
    public void ParseYaml_WithAllProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: Skill
metadata:
  name: test-skill
  owner: test-owner
  tags:
    - tag1
    - tag2
spec:
  description: A comprehensive test skill
";

        // Act
        var skill = ExtendedSkillV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("azuresre.ai/v2", skill.ApiVersion);
        Assert.Equal("Skill", skill.Kind);
        Assert.NotNull(skill.Metadata);
        Assert.Equal("test-skill", skill.Metadata.Name);
        Assert.Equal("test-owner", skill.Metadata.Owner);
        Assert.NotNull(skill.Metadata.Tags);
        Assert.Equal(2, skill.Metadata.Tags.Count);
        Assert.Contains("tag1", skill.Metadata.Tags);
        Assert.Contains("tag2", skill.Metadata.Tags);

        Assert.NotNull(skill.Spec);
        Assert.Equal("A comprehensive test skill", skill.Spec.Description);
        Assert.Null(skill.Spec.SkillContent); // SkillContent is loaded from SKILL.md, not from YAML

        // AdditionalFiles are not in YAML anymore - they are auto-discovered from directory by LoadYamlAsync
        Assert.NotNull(skill.Spec.AdditionalFiles);
        Assert.Empty(skill.Spec.AdditionalFiles);
    }

    [Fact]
    public void ParseYaml_WithOptionalFieldsOmitted_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: Skill
metadata:
  name: simple-skill
spec:
  description: Simple skill
";

        // Act
        var skill = ExtendedSkillV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("azuresre.ai/v2", skill.ApiVersion);
        Assert.Equal("Skill", skill.Kind);
        Assert.NotNull(skill.Metadata);
        Assert.Equal("simple-skill", skill.Metadata.Name);
        Assert.Null(skill.Metadata.Owner);
        Assert.Null(skill.Metadata.Tags);
        Assert.NotNull(skill.Spec);
        Assert.Equal("Simple skill", skill.Spec.Description);
        Assert.Null(skill.Spec.SkillContent); // SkillContent is loaded from SKILL.md, not from YAML
        Assert.NotNull(skill.Spec.AdditionalFiles);
        Assert.Empty(skill.Spec.AdditionalFiles);
    }

    #endregion

    #region ToYaml Tests

    [Fact]
    public void ToYaml_WithMinimalProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var skill = new ExtendedSkillV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-skill",
                Owner = "test-owner"
            },
            Spec = new SkillSpecV2
            {
                Description = "A test skill",
                SkillContent = "# Test Skill\nThis is the skill content." // Not serialized to YAML
            }
        };

        // Act
        var yaml = skill.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: azuresre.ai/v2", yaml);
        Assert.Contains("kind: Skill", yaml);
        Assert.Contains("name: test-skill", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("A test skill", yaml); // Description is in YAML (using literal block style)
        Assert.DoesNotContain("# Test Skill", yaml); // SkillContent is not serialized
    }

    [Fact]
    public void ToYaml_WithAllProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var skill = new ExtendedSkillV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-skill",
                Owner = "test-owner",
                Tags = new List<string> { "tag1", "tag2" }
            },
            Spec = new SkillSpecV2
            {
                Description = "A comprehensive test skill",
                SkillContent = "# Test Skill\nContent here",
                AdditionalFiles = new List<SkillAdditionalFileV2>
                {
                    new SkillAdditionalFileV2
                    {
                        FilePath = "examples/example1.md",
                        Content = "# Example 1\nContent"
                    },
                    new SkillAdditionalFileV2
                    {
                        FilePath = "examples/example2.yaml",
                        Content = "key: value"
                    }
                }
            }
        };

        // Act
        var yaml = skill.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: azuresre.ai/v2", yaml);
        Assert.Contains("kind: Skill", yaml);
        Assert.Contains("name: test-skill", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("- tag1", yaml);
        Assert.Contains("- tag2", yaml);
        Assert.Contains("A comprehensive test skill", yaml); // Description is in YAML (using literal block style)
        // AdditionalFiles are not in YAML anymore - they are auto-discovered from directory
        Assert.DoesNotContain("examples/example1.md", yaml);
        Assert.DoesNotContain("examples/example2.yaml", yaml);
        Assert.DoesNotContain("additionalFiles", yaml);
        Assert.DoesNotContain("# Test Skill", yaml); // SkillContent is not serialized
        Assert.DoesNotContain("# Example 1", yaml); // Additional file content is not serialized
        Assert.DoesNotContain("key: value", yaml); // Additional file content is not serialized
    }

    [Fact]
    public void ToYaml_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalSkill = new ExtendedSkillV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-skill",
                Owner = "test-owner",
                Tags = new List<string> { "production", "critical" }
            },
            Spec = new SkillSpecV2
            {
                Description = "A test skill",
                SkillContent = "# Test Skill\nSkill content here", // Not serialized
                AdditionalFiles = new List<SkillAdditionalFileV2>
                {
                    new SkillAdditionalFileV2
                    {
                        FilePath = "example.md",
                        Content = "Example content" // Not serialized
                    }
                }
            }
        };

        // Act
        var yaml = originalSkill.ToYaml();
        var deserializedSkill = ExtendedSkillV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedSkill);
        Assert.Equal(originalSkill.ApiVersion, deserializedSkill.ApiVersion);
        Assert.Equal(originalSkill.Kind, deserializedSkill.Kind);
        Assert.Equal(originalSkill.Metadata.Name, deserializedSkill.Metadata.Name);
        Assert.Equal(originalSkill.Metadata.Owner, deserializedSkill.Metadata.Owner);
        Assert.Equal(originalSkill.Metadata.Tags?.Count, deserializedSkill.Metadata.Tags?.Count);
        Assert.Equal(originalSkill.Spec.Description, deserializedSkill.Spec.Description);
        Assert.Null(deserializedSkill.Spec.SkillContent); // SkillContent is not serialized/deserialized
        // AdditionalFiles are not in YAML anymore - they would be empty after ParseYaml
        Assert.NotNull(deserializedSkill.Spec.AdditionalFiles);
        Assert.Empty(deserializedSkill.Spec.AdditionalFiles);
    }

    [Fact]
    public void ToYaml_WithMultilineContent_ShouldNormalizeCorrectly()
    {
        // Arrange
        var skill = new ExtendedSkillV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-skill"
            },
            Spec = new SkillSpecV2
            {
                Description = "Test skill",
                SkillContent = "Line 1\nLine 2\nLine 3" // Not serialized, but included for normalization test
            }
        };

        // Act
        skill.Normalize();

        // Assert - SkillContent should be normalized (trailing whitespace removed)
        Assert.NotNull(skill.Spec.SkillContent);
        Assert.Contains("Line 1", skill.Spec.SkillContent);
        Assert.Contains("Line 2", skill.Spec.SkillContent);
        Assert.Contains("Line 3", skill.Spec.SkillContent);
    }

    #endregion

    #region AdditionalFiles Tests

    [Fact]
    public void AdditionalFiles_Add_ShouldWorkCorrectly()
    {
        // Arrange
        var spec = new SkillSpecV2();

        // Act - Add items to the initially empty list
        spec.AdditionalFiles!.Add(new SkillAdditionalFileV2
        {
            FilePath = "file1.md",
            Content = "Content 1"
        });
        spec.AdditionalFiles!.Add(new SkillAdditionalFileV2
        {
            FilePath = "file2.md",
            Content = "Content 2"
        });

        // Assert - Should contain added items
        Assert.Equal(2, spec.AdditionalFiles.Count);
        Assert.Equal("file1.md", spec.AdditionalFiles[0].FilePath);
        Assert.Equal("file2.md", spec.AdditionalFiles[1].FilePath);
    }

    [Fact]
    public void AdditionalFiles_DefaultInitialization_ShouldBeEmptyList()
    {
        // Arrange
        var spec = new SkillSpecV2();

        // Act & Assert - Default initialization should provide empty list
        Assert.NotNull(spec.AdditionalFiles);
        Assert.Empty(spec.AdditionalFiles);
    }

    #endregion

    #region Normalize Tests

    [Fact]
    public void Normalize_ShouldTrimTrailingWhitespace()
    {
        // Arrange
        var skill = new ExtendedSkillV2
        {
            Metadata = new ResourceMetadataModel { Name = "test" },
            Spec = new SkillSpecV2
            {
                Description = "Description with trailing spaces   ",
                SkillContent = "Content with trailing spaces   \n  Line 2  ",
                AdditionalFiles = new List<SkillAdditionalFileV2>
                {
                    new SkillAdditionalFileV2
                    {
                        FilePath = "test.md",
                        Content = "Content with trailing   \nspaces  "
                    }
                }
            }
        };

        // Act
        skill.Normalize();

        // Assert - Trailing whitespace should be removed
        Assert.Equal("Description with trailing spaces", skill.Spec.Description);
        Assert.Equal("Content with trailing spaces\n  Line 2", skill.Spec.SkillContent);
        Assert.Equal("Content with trailing\nspaces", skill.Spec.AdditionalFiles[0].Content);
    }

    [Fact]
    public void Normalize_WithNullContent_ShouldNotThrow()
    {
        // Arrange
        var skill = new ExtendedSkillV2
        {
            Metadata = new ResourceMetadataModel { Name = "test" },
            Spec = new SkillSpecV2
            {
                Description = null,
                SkillContent = null,
                AdditionalFiles = new List<SkillAdditionalFileV2>
                {
                    new SkillAdditionalFileV2
                    {
                        FilePath = "test.md",
                        Content = null
                    }
                }
            }
        };

        // Act & Assert - Should not throw
        var exception = Record.Exception(() => skill.Normalize());
        Assert.Null(exception);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldInitializeDefaults()
    {
        // Act
        var skill = new ExtendedSkillV2();

        // Assert
        Assert.Equal("azuresre.ai/v2", skill.ApiVersion);
        Assert.Equal("Skill", skill.Kind);
        Assert.NotNull(skill.Metadata);
        Assert.NotNull(skill.Spec);
    }

    #endregion
}
