// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Skills;

namespace Agent.Tests.Unit.Framework;

public class SkillFrontmatterTests
{
    #region Parse Tests

    [Fact]
    public void Parse_ValidFrontmatter_ExtractsMetadataAndContent()
    {
        // Arrange
        var skillMdContent = """
            ---
            name: test_skill
            description: This is a test skill
            tools:
              - Tool1
              - Tool2
            ---

            # Test Skill

            This is the markdown content.
            """;

        // Act
        var result = SkillFrontmatter.Parse(skillMdContent);

        // Assert
        Assert.True(result.HasFrontmatter);
        Assert.NotNull(result.Metadata);
        Assert.Equal("test_skill", result.Metadata.Name);
        Assert.Equal("This is a test skill", result.Metadata.Description);
        Assert.Equal(2, result.Metadata.Tools.Count);
        Assert.Contains("Tool1", result.Metadata.Tools);
        Assert.Contains("Tool2", result.Metadata.Tools);
        Assert.Contains("# Test Skill", result.MarkdownContent);
        Assert.Contains("This is the markdown content.", result.MarkdownContent);
    }

    [Fact]
    public void Parse_MultilineDescription_ParsesCorrectly()
    {
        // Arrange
        var skillMdContent = """
            ---
            name: test_skill
            description: |
              This is a multiline
              description for the skill.
            tools: []
            ---

            # Content
            """;

        // Act
        var result = SkillFrontmatter.Parse(skillMdContent);

        // Assert
        Assert.True(result.HasFrontmatter);
        Assert.NotNull(result.Metadata);
        Assert.Contains("multiline", result.Metadata.Description);
        Assert.Contains("description for the skill", result.Metadata.Description);
    }

    [Fact]
    public void Parse_NoFrontmatter_ReturnsFullContentAsMarkdown()
    {
        // Arrange
        var skillMdContent = """
            # Test Skill

            This is a skill without frontmatter.
            """;

        // Act
        var result = SkillFrontmatter.Parse(skillMdContent);

        // Assert
        Assert.False(result.HasFrontmatter);
        Assert.Null(result.Metadata);
        Assert.Contains("# Test Skill", result.MarkdownContent);
        Assert.Contains("without frontmatter", result.MarkdownContent);
    }

    [Fact]
    public void Parse_OpeningDelimiterOnly_TreatsAsNoFrontmatter()
    {
        // Arrange - Has opening --- but no closing ---
        var skillMdContent = """
            ---
            name: incomplete
            
            # Content without closing delimiter
            """;

        // Act
        var result = SkillFrontmatter.Parse(skillMdContent);

        // Assert
        Assert.False(result.HasFrontmatter);
        Assert.Equal(skillMdContent, result.MarkdownContent);
    }

    [Fact]
    public void Parse_InvalidYaml_TreatsAsNoFrontmatter()
    {
        // Arrange - Invalid YAML in frontmatter
        var skillMdContent = """
            ---
            name: test
            invalid yaml [
            ---

            # Content
            """;

        // Act
        var result = SkillFrontmatter.Parse(skillMdContent);

        // Assert
        Assert.False(result.HasFrontmatter);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyResult()
    {
        // Act
        var result = SkillFrontmatter.Parse(string.Empty);

        // Assert
        Assert.False(result.HasFrontmatter);
        Assert.Equal(string.Empty, result.MarkdownContent);
    }

    [Fact]
    public void Parse_FirstPartyOnlyFlag_ParsesCorrectly()
    {
        // Arrange
        var skillMdContent = """
            ---
            name: internal_skill
            description: Internal only skill
            tools: []
            first_party_only: true
            ---

            # Internal Skill
            """;

        // Act
        var result = SkillFrontmatter.Parse(skillMdContent);

        // Assert
        Assert.True(result.HasFrontmatter);
        Assert.NotNull(result.Metadata);
        Assert.True(result.Metadata.FirstPartyOnly);
    }

    #endregion

    #region HasValidFrontmatter Tests

    [Fact]
    public void HasValidFrontmatter_WithValidFrontmatter_ReturnsTrue()
    {
        // Arrange
        var skillMdContent = """
            ---
            name: test_skill
            description: Test
            tools: []
            ---

            # Content
            """;

        // Act
        var result = SkillFrontmatter.HasValidFrontmatter(skillMdContent);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasValidFrontmatter_WithoutFrontmatter_ReturnsFalse()
    {
        // Arrange
        var skillMdContent = "# Just markdown content";

        // Act
        var result = SkillFrontmatter.HasValidFrontmatter(skillMdContent);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Generate Tests

    [Fact]
    public void Generate_BasicMetadata_CreatesValidFrontmatter()
    {
        // Arrange
        var metadata = new YamlSkillDescriptor
        {
            Name = "test_skill",
            Description = "A test skill",
            Tools = ["Tool1", "Tool2"]
        };
        var markdownContent = "# Test Skill\n\nContent here.";

        // Act
        var result = SkillFrontmatter.Generate(metadata, markdownContent);

        // Assert
        Assert.StartsWith("---", result);
        Assert.Contains("name: test_skill", result);
        Assert.Contains("description: A test skill", result);
        Assert.Contains("tools:", result);
        Assert.Contains("  - Tool1", result);
        Assert.Contains("  - Tool2", result);
        Assert.Contains("# Test Skill", result);
        Assert.Contains("Content here.", result);
    }

    [Fact]
    public void Generate_MultilineDescription_UsesLiteralBlockStyle()
    {
        // Arrange
        var metadata = new YamlSkillDescriptor
        {
            Name = "test_skill",
            Description = "Line one\nLine two\nLine three",
            Tools = []
        };

        // Act
        var result = SkillFrontmatter.Generate(metadata, "# Content");

        // Assert
        Assert.Contains("description: |", result);
        Assert.Contains("  Line one", result);
        Assert.Contains("  Line two", result);
    }

    [Fact]
    public void Generate_FirstPartyOnly_IncludedWhenTrue()
    {
        // Arrange
        var metadata = new YamlSkillDescriptor
        {
            Name = "internal_skill",
            Description = "Internal",
            Tools = [],
            FirstPartyOnly = true
        };

        // Act
        var result = SkillFrontmatter.Generate(metadata, "# Content", includeFirstPartyOnly: true);

        // Assert
        Assert.Contains("first_party_only: true", result);
    }

    [Fact]
    public void Generate_FirstPartyOnlyFalse_NotIncluded()
    {
        // Arrange
        var metadata = new YamlSkillDescriptor
        {
            Name = "public_skill",
            Description = "Public",
            Tools = [],
            FirstPartyOnly = false
        };

        // Act
        var result = SkillFrontmatter.Generate(metadata, "# Content", includeFirstPartyOnly: true);

        // Assert
        Assert.DoesNotContain("first_party_only", result);
    }

    [Fact]
    public void Generate_EmptyTools_OmitsToolsSection()
    {
        // Arrange
        var metadata = new YamlSkillDescriptor
        {
            Name = "no_tools_skill",
            Description = "No tools",
            Tools = []
        };

        // Act
        var result = SkillFrontmatter.Generate(metadata, "# Content");

        // Assert
        Assert.DoesNotContain("tools:", result);
    }

    [Fact]
    public void Generate_Roundtrip_PreservesData()
    {
        // Arrange
        var originalMetadata = new YamlSkillDescriptor
        {
            Name = "roundtrip_skill",
            Description = "Testing roundtrip\nwith multiple lines",
            Tools = ["ToolA", "ToolB", "ToolC"],
            FirstPartyOnly = true
        };
        var originalContent = "# Roundtrip Test\n\nOriginal content.";

        // Act
        var generated = SkillFrontmatter.Generate(originalMetadata, originalContent, includeFirstPartyOnly: true);
        var parsed = SkillFrontmatter.Parse(generated);

        // Assert
        Assert.True(parsed.HasFrontmatter);
        Assert.NotNull(parsed.Metadata);
        Assert.Equal("roundtrip_skill", parsed.Metadata.Name);
        Assert.Contains("Testing roundtrip", parsed.Metadata.Description);
        Assert.Contains("with multiple lines", parsed.Metadata.Description);
        Assert.Equal(3, parsed.Metadata.Tools.Count);
        Assert.True(parsed.Metadata.FirstPartyOnly);
        Assert.Contains("# Roundtrip Test", parsed.MarkdownContent);
        Assert.Contains("Original content.", parsed.MarkdownContent);
    }

    #endregion
}
