// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Framework.Skills;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Framework;

/// <summary>
/// Unit tests for the AllowedSkills filtering functionality in SkillRegistry.
/// Tests cover GetSkillsMetadataForPrompt, GetSkillByName, and ReadSkillFile methods.
/// </summary>
public class SkillRegistryAllowedSkillsTests
{
    private readonly Mock<ILogger<SkillRegistry>> _mockLogger;
    private readonly Mock<IExtensibilityLoader> _mockExtensibilityLoader;
    private readonly string _testSkillsDirectory;

    public SkillRegistryAllowedSkillsTests()
    {
        _mockLogger = new Mock<ILogger<SkillRegistry>>();
        _mockExtensibilityLoader = new Mock<IExtensibilityLoader>();
        _mockExtensibilityLoader
            .Setup(x => x.LoadExtendedSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkillSpec>());

        _testSkillsDirectory = Path.Combine(AppContext.BaseDirectory, "Framework", "TestSkillsFirstParty");
    }

    private SkillRegistry CreateRegistry()
    {
        return new SkillRegistry(
            _mockLogger.Object,
            _testSkillsDirectory,
            _mockExtensibilityLoader.Object,
            isFirstPartyTenantCheck: () => true); // First-party so all skills are accessible
    }

    #region GetSkillsMetadataForPrompt Tests

    [Fact]
    public async Task GetSkillsMetadataForPrompt_WithNullAllowedSkills_ReturnsAllSkills()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(includeSystemSkills: true, allowedSkills: null);

        // Assert - All test skills should be present
        Assert.Contains("regular_skill", metadata);
        Assert.Contains("skill_no_flag", metadata);
        Assert.Contains("first_party_skill", metadata);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_WithEmptyAllowedSkills_ReturnsNoSkills()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(includeSystemSkills: true, allowedSkills: []);

        // Assert
        Assert.Equal("No skills available.", metadata);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_WithSpecificAllowedSkills_ReturnsOnlyAllowedSkills()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: true,
            allowedSkills: ["regular_skill"]);

        // Assert
        Assert.Contains("regular_skill", metadata);
        Assert.DoesNotContain("skill_no_flag", metadata);
        Assert.DoesNotContain("first_party_skill", metadata);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_WithMultipleAllowedSkills_ReturnsOnlyThoseSkills()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: true,
            allowedSkills: ["regular_skill", "skill_no_flag"]);

        // Assert
        Assert.Contains("regular_skill", metadata);
        Assert.Contains("skill_no_flag", metadata);
        Assert.DoesNotContain("first_party_skill", metadata);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_WithNonExistentAllowedSkill_ReturnsNoSkillsMessage()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: true,
            allowedSkills: ["nonexistent_skill"]);

        // Assert
        Assert.Equal("No skills available.", metadata);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_WithMixedValidAndInvalidSkills_ReturnsOnlyValidSkills()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: true,
            allowedSkills: ["regular_skill", "nonexistent_skill"]);

        // Assert
        Assert.Contains("regular_skill", metadata);
        Assert.DoesNotContain("nonexistent_skill", metadata);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_AllowedSkillsIsCaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: true,
            allowedSkills: ["REGULAR_SKILL", "Skill_No_Flag"]);

        // Assert
        Assert.Contains("regular_skill", metadata);
        Assert.Contains("skill_no_flag", metadata);
    }

    #endregion

    #region GetSkillByName Tests

    [Fact]
    public async Task GetSkillByName_WithNullAllowedSkills_ReturnsSkill()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName("regular_skill", includeSystemSkills: true, allowedSkills: null);

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("regular_skill", skill.Name);
    }

    [Fact]
    public async Task GetSkillByName_WithEmptyAllowedSkills_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName("regular_skill", includeSystemSkills: true, allowedSkills: []);

        // Assert
        Assert.Null(skill);
    }

    [Fact]
    public async Task GetSkillByName_WithSkillInAllowedList_ReturnsSkill()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName(
            "regular_skill",
            includeSystemSkills: true,
            allowedSkills: ["regular_skill", "skill_no_flag"]);

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("regular_skill", skill.Name);
    }

    [Fact]
    public async Task GetSkillByName_WithSkillNotInAllowedList_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName(
            "regular_skill",
            includeSystemSkills: true,
            allowedSkills: ["skill_no_flag"]); // regular_skill is not in allowed list

        // Assert
        Assert.Null(skill);
    }

    [Fact]
    public async Task GetSkillByName_AllowedSkillsIsCaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName(
            "regular_skill",
            includeSystemSkills: true,
            allowedSkills: ["REGULAR_SKILL"]);

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("regular_skill", skill.Name);
    }

    #endregion

    #region ReadSkillFile Tests

    [Fact]
    public async Task ReadSkillFile_WithNullAllowedSkills_ReturnsContent()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var content = registry.ReadSkillFile("regular_skill", "SKILL.md", includeSystemSkills: true, allowedSkills: null);

        // Assert
        Assert.DoesNotContain("Error:", content);
        Assert.Contains("Regular Skill", content);
    }

    [Fact]
    public async Task ReadSkillFile_WithEmptyAllowedSkills_ReturnsError()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var content = registry.ReadSkillFile("regular_skill", "SKILL.md", includeSystemSkills: true, allowedSkills: []);

        // Assert
        Assert.Contains("Error:", content);
        Assert.Contains("allowed_skills is empty", content);
    }

    [Fact]
    public async Task ReadSkillFile_WithSkillInAllowedList_ReturnsContent()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var content = registry.ReadSkillFile(
            "regular_skill",
            "SKILL.md",
            includeSystemSkills: true,
            allowedSkills: ["regular_skill"]);

        // Assert
        Assert.DoesNotContain("Error:", content);
        Assert.Contains("Regular Skill", content);
    }

    [Fact]
    public async Task ReadSkillFile_WithSkillNotInAllowedList_ReturnsError()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var content = registry.ReadSkillFile(
            "regular_skill",
            "SKILL.md",
            includeSystemSkills: true,
            allowedSkills: ["skill_no_flag"]); // regular_skill is not in allowed list

        // Assert
        Assert.Contains("Error:", content);
        Assert.Contains("not in the allowed skills list", content);
    }

    [Fact]
    public async Task ReadSkillFile_AllowedSkillsIsCaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var content = registry.ReadSkillFile(
            "regular_skill",
            "SKILL.md",
            includeSystemSkills: true,
            allowedSkills: ["REGULAR_SKILL"]);

        // Assert
        Assert.DoesNotContain("Error:", content);
        Assert.Contains("Regular Skill", content);
    }

    #endregion

    #region Extended Skills with AllowedSkills Tests

    [Fact]
    public async Task GetSkillsMetadataForPrompt_WithExtendedSkillsAndAllowedSkills_FiltersCorrectly()
    {
        // Arrange - Add extended skills
        var extendedSkills = new List<SkillSpec>
        {
            new SkillSpec
            {
                Name = "extended_skill_1",
                Description = "Extended skill 1",
                Tools = [],
                SkillMdContent = "# Extended Skill 1",
                AdditionalFiles = []
            },
            new SkillSpec
            {
                Name = "extended_skill_2",
                Description = "Extended skill 2",
                Tools = [],
                SkillMdContent = "# Extended Skill 2",
                AdditionalFiles = []
            }
        };

        _mockExtensibilityLoader
            .Setup(x => x.LoadExtendedSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(extendedSkills);

        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act - Allow only one extended skill and one system skill
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: true,
            allowedSkills: ["extended_skill_1", "regular_skill"]);

        // Assert
        Assert.Contains("extended_skill_1", metadata);
        Assert.Contains("regular_skill", metadata);
        Assert.DoesNotContain("extended_skill_2", metadata);
        Assert.DoesNotContain("skill_no_flag", metadata);
    }

    [Fact]
    public async Task GetSkillByName_ExtendedSkillWithAllowedSkills_ReturnsSkillWhenAllowed()
    {
        // Arrange
        var extendedSkill = new SkillSpec
        {
            Name = "extended_skill",
            Description = "Extended skill",
            Tools = [],
            SkillMdContent = "# Extended Skill",
            AdditionalFiles = []
        };

        _mockExtensibilityLoader
            .Setup(x => x.LoadExtendedSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkillSpec> { extendedSkill });

        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName(
            "extended_skill",
            includeSystemSkills: true,
            allowedSkills: ["extended_skill"]);

        // Assert
        Assert.NotNull(skill);
        Assert.Equal("extended_skill", skill.Name);
    }

    [Fact]
    public async Task GetSkillByName_ExtendedSkillWithAllowedSkills_ReturnsNullWhenNotAllowed()
    {
        // Arrange
        var extendedSkill = new SkillSpec
        {
            Name = "extended_skill",
            Description = "Extended skill",
            Tools = [],
            SkillMdContent = "# Extended Skill",
            AdditionalFiles = []
        };

        _mockExtensibilityLoader
            .Setup(x => x.LoadExtendedSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkillSpec> { extendedSkill });

        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName(
            "extended_skill",
            includeSystemSkills: true,
            allowedSkills: ["regular_skill"]); // extended_skill not in list

        // Assert
        Assert.Null(skill);
    }

    #endregion

    #region Interaction with IncludeSystemSkills Tests

    [Fact]
    public async Task GetSkillByName_SystemSkillNotAllowedWhenIncludeSystemSkillsFalse()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act - Even if allowed_skills includes a system skill, it should be blocked by includeSystemSkills=false
        var skill = registry.GetSkillByName(
            "regular_skill",
            includeSystemSkills: false, // System skills excluded
            allowedSkills: ["regular_skill"]);

        // Assert - regular_skill is a system skill, so it should be null
        Assert.Null(skill);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_SystemSkillsExcludedEvenWhenInAllowedList()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act - System skills should be excluded even if they're in the allowed list
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: false,
            allowedSkills: ["regular_skill", "skill_no_flag"]);

        // Assert - No system skills should appear
        Assert.Equal("No skills available.", metadata);
    }

    [Fact]
    public async Task GetSkillsMetadataForPrompt_ExtendedSkillsVisibleWhenSystemSkillsExcluded()
    {
        // Arrange
        var extendedSkill = new SkillSpec
        {
            Name = "extended_skill",
            Description = "Extended skill",
            Tools = [],
            SkillMdContent = "# Extended Skill",
            AdditionalFiles = []
        };

        _mockExtensibilityLoader
            .Setup(x => x.LoadExtendedSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkillSpec> { extendedSkill });

        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act - Even with includeSystemSkills=false, extended skills should be visible
        var metadata = registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: false,
            allowedSkills: ["extended_skill", "regular_skill"]); // regular_skill is system

        // Assert - Only extended skill should appear
        Assert.Contains("extended_skill", metadata);
        Assert.DoesNotContain("regular_skill", metadata);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task GetSkillsMetadataForPrompt_LogsWarningForInvalidSkillNames()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.InitializeAsync();

        // Act
        registry.GetSkillsMetadataForPrompt(
            includeSystemSkills: true,
            allowedSkills: ["nonexistent_skill_1", "regular_skill", "nonexistent_skill_2"]);

        // Assert - Verify that warnings were logged for nonexistent skills
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("nonexistent_skill_1")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("nonexistent_skill_2")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
