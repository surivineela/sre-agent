// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Validations;
using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Framework.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Validations;

public class AgentDescriptorValidationTests
{
    #region Basic Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithNullDescriptor_ShouldAddError()
    {
        // Arrange
        IAgentDescriptor? agentDescriptor = null;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Single(errors);
        Assert.Contains("Agent descriptor is null", errors[0]);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyName_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Name = "";

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("does not have a name"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyInstructions_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Instructions = "";

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("does not have instructions"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithInstructionsTooShort_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Instructions = "Short instruction"; // Less than 50 characters

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("System prompt must be longer than 50 characters"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithInstructionsTooLong_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Instructions = new string('x', 60001); // More than 60000 characters

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("System prompt must be under 60000 characters"));
    }

    #endregion

    #region Temperature Validation Tests

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    [InlineData(-1)]
    [InlineData(3)]
    public void ValidateAgentDescriptor_WithInvalidTemperature_ShouldAddError(float temperature)
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Temperature = temperature;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Temperature must be between 0 and 2"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(0.5)]
    [InlineData(1.5)]
    public void ValidateAgentDescriptor_WithValidTemperature_ShouldNotAddError(float temperature)
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Temperature = temperature;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("Temperature must be between 0 and 2"));
    }

    #endregion

    #region Tools Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyToolName_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Tools = new List<string> { "validTool", "", "anotherValidTool" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Tool name cannot be empty"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithToolNameContainingWhitespace_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Tools = new List<string> { "valid Tool", "another tool" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("'valid Tool' must not contain whitespace"));
        Assert.Contains(errors, e => e.Contains("'another tool' must not contain whitespace"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithValidToolNames_ShouldNotAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Tools = new List<string> { "validTool", "anotherValidTool", "tool123" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("Tool name"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithNullTools_ShouldConvertToEmptyArray()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Tools = null!;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.NotNull(agentDescriptor.Tools);
        Assert.Empty(agentDescriptor.Tools);
        Assert.DoesNotContain(errors, e => e.Contains("Tools property"));
    }

    #endregion

    #region Handoffs Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyHandoffName_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Handoffs = new List<string> { "validAgent", "", "anotherValidAgent" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Handoff name cannot be empty"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithHandoffNameContainingWhitespace_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Handoffs = new List<string> { "valid Agent", "another agent" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("'valid Agent' must not contain whitespace"));
        Assert.Contains(errors, e => e.Contains("'another agent' must not contain whitespace"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithSelfReferenceHandoff_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Name = "testAgent";
        agentDescriptor.Handoffs = new List<string> { "otherAgent", "testAgent" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Agent 'testAgent' cannot have a handoff to itself"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithDuplicateHandoffs_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Handoffs = new List<string> { "agent1", "agent2", "agent1", "agent3" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Duplicate handoff target 'agent1' found"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithValidHandoffs_ShouldNotAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Handoffs = new List<string> { "agent1", "agent2", "agent3" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("handoff"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithNullHandoffs_ShouldConvertToEmptyArray()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Handoffs = null!;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.NotNull(agentDescriptor.Handoffs);
        Assert.Empty(agentDescriptor.Handoffs);
        Assert.DoesNotContain(errors, e => e.Contains("Handoffs property"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyHandoffs_ShouldNotAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Handoffs = new List<string>();

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("handoff"));
        Assert.DoesNotContain(errors, e => e.Contains("Handoff"));
    }

    #endregion

    #region MCP Tools Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyMcpToolName_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.McpTools = new List<string> { "validMcpTool", "", "anotherValidMcpTool" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("MCP tool name cannot be empty"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithMcpToolNameContainingWhitespace_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.McpTools = new List<string> { "valid McpTool", "another mcpTool" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("'valid McpTool' must not contain whitespace"));
        Assert.Contains(errors, e => e.Contains("'another mcpTool' must not contain whitespace"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithNullMcpTools_ShouldConvertToEmptyArray()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.McpTools = null!;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.NotNull(agentDescriptor.McpTools);
        Assert.Empty(agentDescriptor.McpTools);
        Assert.DoesNotContain(errors, e => e.Contains("MCP tools property"));
    }

    #endregion

    #region Agent Name Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithAgentNameContainingWhitespace_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Name = "agent with spaces";

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Agent name 'agent with spaces' must not contain whitespace"));
    }

    #endregion

    #region Max Reflection Count Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithNegativeMaxReflectionCount_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.MaxReflectionCount = -1;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Max reflection count cannot be negative"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ValidateAgentDescriptor_WithValidMaxReflectionCount_ShouldNotAddError(int count)
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.MaxReflectionCount = count;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("Max reflection count"));
    }

    #endregion

    #region Handoff Description Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithHandoffDescriptionTooLong_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.HandoffDescription = new string('x', 501); // More than 500 characters

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Handoff description must be under 500 characters"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithValidHandoffDescription_ShouldNotAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.HandoffDescription = new string('x', 500); // Exactly 500 characters

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("Handoff description"));
    }

    #endregion

    #region Common Prompts Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyCommonPrompt_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.CommonPrompts = new List<string> { "validPrompt", "", "anotherValidPrompt" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Common prompt name cannot be empty"));
    }

    #endregion

    #region Agents As Tools Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyAgentNameInAgentsAsTools_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AgentsAsTools = new List<AgentsAsTools>
        {
            new AgentsAsTools { AgentName = "", ToolName = "tool1", ToolDescription = "desc1" }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Agent name in agents_as_tools cannot be empty"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyToolNameInAgentsAsTools_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AgentsAsTools = new List<AgentsAsTools>
        {
            new AgentsAsTools { AgentName = "agent1", ToolName = "", ToolDescription = "desc1" }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Tool name in agents_as_tools cannot be empty"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyToolDescriptionInAgentsAsTools_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AgentsAsTools = new List<AgentsAsTools>
        {
            new AgentsAsTools { AgentName = "agent1", ToolName = "tool1", ToolDescription = "" }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Tool description in agents_as_tools cannot be empty"));
    }

    #endregion

    #region Allowed Skills Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithEmptySkillNameInAllowedSkills_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AllowedSkills = new List<string> { "validSkill", "", "anotherValidSkill" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Skill name in allowed_skills cannot be empty"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithSkillNameContainingWhitespace_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AllowedSkills = new List<string> { "valid skill", "another skill" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("'valid skill' in allowed_skills must not contain whitespace"));
        Assert.Contains(errors, e => e.Contains("'another skill' in allowed_skills must not contain whitespace"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithDuplicateSkillsInAllowedSkills_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AllowedSkills = new List<string> { "skill1", "skill2", "skill1", "skill3" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Contains(errors, e => e.Contains("Duplicate skill name 'skill1' found in allowed_skills"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithDuplicateSkillsCaseInsensitive_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AllowedSkills = new List<string> { "Skill1", "SKILL1" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        // Should detect duplicate even with different casing
        Assert.Contains(errors, e => e.Contains("Duplicate skill name") && e.Contains("allowed_skills"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithValidAllowedSkills_ShouldNotAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AllowedSkills = new List<string> { "kubernetes_skill", "postgresql_skill", "metrics_skill" };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("allowed_skills"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyAllowedSkillsList_ShouldNotAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AllowedSkills = new List<string>();

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("allowed_skills"));
    }

    [Fact]
    public void ValidateAgentDescriptor_WithNullAllowedSkills_ShouldNotAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.AllowedSkills = null;

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.DoesNotContain(errors, e => e.Contains("allowed_skills"));
    }

    #endregion

    #region Valid Agent Tests

    [Fact]
    public void ValidateAgentDescriptor_WithValidAgent_ShouldNotAddErrors()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region YAML Parsing Tests for AllowedSkills

    [Fact]
    public void YamlAgentDescriptor_FromYaml_WithAllowedSkills_ShouldParseCorrectly()
    {
        // Arrange
        var yaml = @"
name: test_agent
system_prompt: This is a valid instruction that is longer than 50 characters to meet the minimum requirement.
enable_skills: true
add_system_skills: true
allowed_skills:
  - kubernetes_skill
  - postgresql_skill
  - metrics_skill
";

        // Act
        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        // Assert
        Assert.NotNull(descriptor.AllowedSkills);
        Assert.Equal(3, descriptor.AllowedSkills.Count);
        Assert.Contains("kubernetes_skill", descriptor.AllowedSkills);
        Assert.Contains("postgresql_skill", descriptor.AllowedSkills);
        Assert.Contains("metrics_skill", descriptor.AllowedSkills);
    }

    [Fact]
    public void YamlAgentDescriptor_FromYaml_WithEmptyAllowedSkills_ShouldParseAsEmptyList()
    {
        // Arrange
        var yaml = @"
name: test_agent
system_prompt: This is a valid instruction that is longer than 50 characters to meet the minimum requirement.
enable_skills: true
allowed_skills: []
";

        // Act
        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        // Assert
        Assert.NotNull(descriptor.AllowedSkills);
        Assert.Empty(descriptor.AllowedSkills);
    }

    [Fact]
    public void YamlAgentDescriptor_FromYaml_WithoutAllowedSkills_ShouldBeNull()
    {
        // Arrange
        var yaml = @"
name: test_agent
system_prompt: This is a valid instruction that is longer than 50 characters to meet the minimum requirement.
enable_skills: true
add_system_skills: true
";

        // Act
        var descriptor = YamlAgentDescriptor.FromYaml(yaml);

        // Assert
        Assert.Null(descriptor.AllowedSkills);
    }

    #endregion

    #region Hook Validation Tests

    [Fact]
    public void ValidateAgentDescriptor_WithValidPromptHook_ShouldNotAddErrors()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = "Review the conversation and determine if the agent should stop.",
                    Timeout = 30
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithValidCommandHook_ShouldNotAddErrors()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["PostToolUse"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Command,
                    Command = "echo '{\"ok\": true}'",
                    Matcher = "Edit|Write",
                    Timeout = 30
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithPromptHookMissingPrompt_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = null, // Missing prompt
                    Timeout = 30
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Single(errors);
        Assert.Contains("Prompt hook", errors[0]);
        Assert.Contains("must have a prompt defined", errors[0]);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithCommandHookMissingCommand_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["PostToolUse"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Command,
                    Command = null, // Missing command
                    Timeout = 30
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Single(errors);
        Assert.Contains("Command hook", errors[0]);
        Assert.Contains("must have a command defined", errors[0]);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithInvalidHookEventType_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["InvalidEvent"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = "Some prompt",
                    Timeout = 30
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Single(errors);
        Assert.Contains("Invalid hook event type", errors[0]);
        Assert.Contains("InvalidEvent", errors[0]);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithEmptyHookDefinitions_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>() // Empty list
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Single(errors);
        Assert.Contains("has no hook definitions", errors[0]);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithHookTimeoutTooLow_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = "Some prompt",
                    Timeout = 0 // Invalid: must be positive
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Single(errors);
        Assert.Contains("invalid timeout", errors[0]);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithHookTimeoutTooHigh_ShouldAddError()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = "Some prompt",
                    Timeout = 500 // Invalid: max is 300
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Single(errors);
        Assert.Contains("excessive timeout", errors[0]);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithMultipleHookEvents_ShouldValidateAll()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = "Stop hook prompt",
                    Timeout = 30
                }
            },
            ["PostToolUse"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Command,
                    Command = "validate-tool.sh",
                    Matcher = "*",
                    Timeout = 60
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithMultipleHooksInSameEvent_ShouldValidateAll()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["PostToolUse"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Command,
                    Command = "lint-check.sh",
                    Matcher = "Edit",
                    Timeout = 30
                },
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = "Review the tool output for security issues.",
                    Timeout = 60
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateAgentDescriptor_WithMultipleHookErrors_ShouldReportAll()
    {
        // Arrange
        var agentDescriptor = CreateValidAgentDescriptor();
        agentDescriptor.Hooks = new Dictionary<string, List<HookDefinition>>
        {
            ["Stop"] = new List<HookDefinition>
            {
                new HookDefinition
                {
                    Type = HookType.Prompt,
                    Prompt = null, // Error: missing prompt
                    Timeout = 30
                },
                new HookDefinition
                {
                    Type = HookType.Command,
                    Command = null, // Error: missing command
                    Timeout = 30
                }
            }
        };

        // Act
        AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out var errors);

        // Assert
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.Contains("Prompt hook") && e.Contains("must have a prompt"));
        Assert.Contains(errors, e => e.Contains("Command hook") && e.Contains("must have a command"));
    }

    #endregion

    #region Helper Methods

    private static YamlAgentDescriptor CreateValidAgentDescriptor()
    {
        return new YamlAgentDescriptor
        {
            Name = "validAgent",
            Instructions = "This is a valid instruction that is longer than 50 characters to meet the minimum requirement.",
            HandoffDescription = "Valid handoff description",
            Handoffs = new List<string>(),
            Tools = new List<string>(),
            McpTools = new List<string>(),
            AllowParallelToolCalls = false,
            AgentsAsTools = new List<AgentsAsTools>(),
            MaxReflectionCount = 0,
            CustomReflectionNote = "",
            CriticPromptPath = "",
            CriticOnHandOff = false,
            CommonPrompts = new List<string>(),
            CommonTools = new List<string>(),
            Temperature = 1.0f,
            LlmModelName = null,
            LlmScenarioType = null,
            OutputType = null,
            UserPromptOverride = null,
            DisableDocumentRetrieval = false,
            EnableHandoffPromptOverride = false,

            AgentType = AgentType.Autonomous,
            ParameterExtractionAgent = null,
            OrchestrationStartAgents = new List<string>(),
            ResultSummarizationPrompt = null,
            NextAgentMappings = new List<NextAgentMapping>()
        };
    }

    #endregion
}
