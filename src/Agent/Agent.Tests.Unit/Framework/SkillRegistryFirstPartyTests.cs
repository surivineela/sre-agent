// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Framework.Skills;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit.Framework;

public class SkillRegistryFirstPartyTests
{
    private readonly Mock<ILogger<SkillRegistry>> _mockLogger;
    private readonly Mock<IExtensibilityLoader> _mockExtensibilityLoader;
    private readonly string _testSkillsDirectory;

    public SkillRegistryFirstPartyTests()
    {
        _mockLogger = new Mock<ILogger<SkillRegistry>>();
        _mockExtensibilityLoader = new Mock<IExtensibilityLoader>();
        _mockExtensibilityLoader
            .Setup(x => x.LoadExtendedSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkillSpec>());

        _testSkillsDirectory = Path.Combine(AppContext.BaseDirectory, "Framework", "TestSkillsFirstParty");
    }

    private SkillRegistry CreateRegistry(bool isFirstParty)
    {
        return new SkillRegistry(
            _mockLogger.Object,
            _testSkillsDirectory,
            _mockExtensibilityLoader.Object,
            isFirstPartyTenantCheck: () => isFirstParty);
    }

    [Fact]
    public async Task SystemSkill_FirstPartyOnly_HiddenForNonFirstParty()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: false);
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(includeSystemSkills: true);

        // Assert
        Assert.DoesNotContain("first_party_skill", metadata);
        Assert.Contains("regular_skill", metadata);
        Assert.Contains("skill_no_flag", metadata);
    }

    [Fact]
    public async Task SystemSkill_FirstPartyOnly_VisibleForFirstParty()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: true);
        await registry.InitializeAsync();

        // Act
        var metadata = registry.GetSkillsMetadataForPrompt(includeSystemSkills: true);

        // Assert
        Assert.Contains("first_party_skill", metadata);
        Assert.Contains("regular_skill", metadata);
        Assert.Contains("skill_no_flag", metadata);
    }

    [Fact]
    public async Task SystemSkill_GetSkillByName_ReturnsNullForNonFirstParty()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: false);
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName("first_party_skill", includeSystemSkills: true);

        // Assert
        Assert.Null(skill);
    }

    [Fact]
    public async Task SystemSkill_GetSkillByName_ReturnsSkillForFirstParty()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: true);
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName("first_party_skill", includeSystemSkills: true);

        // Assert
        Assert.NotNull(skill);
        Assert.True(skill.FirstPartyOnly);
    }

    [Fact]
    public async Task SystemSkill_ReadSkillFile_ReturnsErrorForNonFirstParty()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: false);
        await registry.InitializeAsync();

        // Act
        var result = registry.ReadSkillFile("first_party_skill", "SKILL.md", includeSystemSkills: true);

        // Assert
        Assert.Contains("Error:", result);
        Assert.Contains("not found", result);
    }

    [Fact]
    public async Task SystemSkill_ReadSkillFile_ReturnsContentForFirstParty()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: true);
        await registry.InitializeAsync();

        // Act
        var result = registry.ReadSkillFile("first_party_skill", "SKILL.md", includeSystemSkills: true);

        // Assert
        Assert.DoesNotContain("Error:", result);
        Assert.Contains("First Party Skill", result);
    }

    [Fact]
    public async Task SystemSkill_RegularSkill_AlwaysVisible()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: false);
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName("regular_skill", includeSystemSkills: true);

        // Assert
        Assert.NotNull(skill);
        Assert.False(skill.FirstPartyOnly);
    }

    [Fact]
    public async Task SystemSkill_FirstPartyOnly_DefaultsToFalse()
    {
        // Arrange
        var registry = CreateRegistry(isFirstParty: false);
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName("skill_no_flag", includeSystemSkills: true);

        // Assert
        Assert.NotNull(skill);
        Assert.False(skill.FirstPartyOnly);
    }

    [Fact]
    public async Task ExtendedSkill_FirstPartyOnlyIgnored()
    {
        // Arrange - Create an extended skill with first_party_only: true
        var extendedSkill = new SkillSpec
        {
            Name = "extended_first_party",
            Description = "An extended skill claiming first-party-only",
            Tools = [],
            FirstPartyOnly = true, // This should be ignored for extended skills
            SkillMdContent = "# Extended Skill",
            AdditionalFiles = []
        };

        _mockExtensibilityLoader
            .Setup(x => x.LoadExtendedSkillsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SkillSpec> { extendedSkill });

        var registry = CreateRegistry(isFirstParty: false); // Non-first-party context
        await registry.InitializeAsync();

        // Act
        var skill = registry.GetSkillByName("extended_first_party", includeSystemSkills: true);
        var metadata = registry.GetSkillsMetadataForPrompt(includeSystemSkills: true);

        // Assert - Extended skill should still be visible even with first_party_only: true
        Assert.NotNull(skill);
        Assert.Contains("extended_first_party", metadata);
    }
}
