// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class ExtendedAgentV1Tests
{
    #region ParseYaml Tests

    [Fact]
    public void ParseYaml_WithValidMinimalYaml_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: agent.platform.ai/v1
kind: AgentConfiguration
metadata:
  name: test-agent
  owner: test-owner
spec:
  name: TestAgent
  system_prompt: |
    You are a helpful assistant.
";

        // Act
        var agent = ExtendedAgentV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("agent.platform.ai/v1", agent.ApiVersion);
        Assert.Equal("AgentConfiguration", agent.Kind);
        Assert.NotNull(agent.Metadata);
        Assert.Equal("test-agent", agent.Metadata.Name);
        Assert.Equal("test-owner", agent.Metadata.Owner);
        Assert.NotNull(agent.Spec);
        Assert.Equal("TestAgent", agent.Spec.Name);
        Assert.Contains("You are a helpful assistant.", agent.Spec.Instructions);
    }

    [Fact]
    public void ParseYaml_WithAllProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: agent.platform.ai/v1
kind: AgentConfiguration
metadata:
  name: test-agent
  owner: test-owner
  tags:
    - tag1
    - tag2
spec:
  name: TestAgent
  system_prompt: |
    You are a helpful assistant.
    You can help with various tasks.
  handoff_description: Hand off to expert
  handoffs:
    - ExpertAgent
    - SpecialistAgent
  tools:
    - tool1
    - tool2
  allow_parallel_tool_calls: true
  max_reflection_count: 3
  critic_prompt_path: /path/to/critic
  critic_on_handoff: true
  custom_reflection_note: Custom note here
  common_prompts:
    - prompt1
    - prompt2
  temperature: 0.7
";

        // Act
        var agent = ExtendedAgentV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("agent.platform.ai/v1", agent.ApiVersion);
        Assert.Equal("AgentConfiguration", agent.Kind);
        Assert.NotNull(agent.Metadata);
        Assert.Equal("test-agent", agent.Metadata.Name);
        Assert.Equal("test-owner", agent.Metadata.Owner);
        Assert.NotNull(agent.Metadata.Tags);
        Assert.Equal(2, agent.Metadata.Tags.Count);
        Assert.Contains("tag1", agent.Metadata.Tags);
        Assert.Contains("tag2", agent.Metadata.Tags);

        Assert.NotNull(agent.Spec);
        Assert.Equal("TestAgent", agent.Spec.Name);
        Assert.Contains("You are a helpful assistant.", agent.Spec.Instructions);
        Assert.Equal("Hand off to expert", agent.Spec.HandoffDescription);
        Assert.NotNull(agent.Spec.Handoffs);
        Assert.Equal(2, agent.Spec.Handoffs.Count);
        Assert.NotNull(agent.Spec.Tools);
        Assert.Equal(2, agent.Spec.Tools.Count);
        Assert.True(agent.Spec.AllowParallelToolCalls);
        Assert.Equal(3, agent.Spec.MaxReflectionCount);
        Assert.Equal("/path/to/critic", agent.Spec.CriticPromptPath);
        Assert.True(agent.Spec.CriticOnHandoff);
        Assert.Equal("Custom note here", agent.Spec.CustomReflectionNote);
        Assert.NotNull(agent.Spec.CommonPrompts);
        Assert.Equal(2, agent.Spec.CommonPrompts.Count);
        Assert.Equal(0.7f, agent.Spec.Temperature);
    }

    [Fact]
    public void ParseYaml_WithOptionalFieldsOmitted_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: agent.platform.ai/v1
kind: AgentConfiguration
metadata:
  name: simple-agent
spec:
  name: SimpleAgent
";

        // Act
        var agent = ExtendedAgentV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("agent.platform.ai/v1", agent.ApiVersion);
        Assert.Equal("AgentConfiguration", agent.Kind);
        Assert.NotNull(agent.Metadata);
        Assert.Equal("simple-agent", agent.Metadata.Name);
        Assert.Null(agent.Metadata.Owner);
        Assert.Null(agent.Metadata.Tags);
        Assert.NotNull(agent.Spec);
        Assert.Equal("SimpleAgent", agent.Spec.Name);
        Assert.Null(agent.Spec.Instructions);
        Assert.Null(agent.Spec.HandoffDescription);
        Assert.Null(agent.Spec.Handoffs);
        Assert.Null(agent.Spec.Tools);
    }

    #endregion

    #region ToYaml Tests

    [Fact]
    public void ToYaml_WithMinimalProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var agent = new ExtendedAgentV1
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-agent",
                Owner = "test-owner"
            },
            Spec = new ExtendedAgentSpecV1
            {
                Name = "TestAgent",
                Instructions = "You are a helpful assistant."
            }
        };

        // Act
        var yaml = agent.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: agent.platform.ai/v1", yaml);
        Assert.Contains("kind: AgentConfiguration", yaml);
        Assert.Contains("name: test-agent", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("name: TestAgent", yaml);
        Assert.Contains("You are a helpful assistant.", yaml);
    }

    [Fact]
    public void ToYaml_WithAllProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var agent = new ExtendedAgentV1
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-agent",
                Owner = "test-owner",
                Tags = new List<string> { "tag1", "tag2" }
            },
            Spec = new ExtendedAgentSpecV1
            {
                Name = "TestAgent",
                Instructions = "You are a helpful assistant.\nYou can help with various tasks.",
                HandoffDescription = "Hand off to expert",
                Handoffs = new List<string> { "ExpertAgent", "SpecialistAgent" },
                Tools = new List<string> { "tool1", "tool2" },
                AllowParallelToolCalls = true,
                MaxReflectionCount = 3,
                CriticPromptPath = "/path/to/critic",
                CriticOnHandoff = true,
                CustomReflectionNote = "Custom note here",
                CommonPrompts = new List<string> { "prompt1", "prompt2" },
                Temperature = 0.7f
            }
        };

        // Act
        var yaml = agent.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: agent.platform.ai/v1", yaml);
        Assert.Contains("kind: AgentConfiguration", yaml);
        Assert.Contains("name: test-agent", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("- tag1", yaml);
        Assert.Contains("- tag2", yaml);
        Assert.Contains("name: TestAgent", yaml);
        Assert.Contains("You are a helpful assistant.", yaml);
        Assert.Contains("handoff_description: Hand off to expert", yaml);
        Assert.Contains("- ExpertAgent", yaml);
        Assert.Contains("- SpecialistAgent", yaml);
        Assert.Contains("- tool1", yaml);
        Assert.Contains("- tool2", yaml);
        Assert.Contains("allow_parallel_tool_calls: true", yaml);
        Assert.Contains("max_reflection_count: 3", yaml);
        Assert.Contains("critic_prompt_path: /path/to/critic", yaml);
        Assert.Contains("critic_on_handoff: true", yaml);
        Assert.Contains("custom_reflection_note: Custom note here", yaml);
        Assert.Contains("- prompt1", yaml);
        Assert.Contains("- prompt2", yaml);
        Assert.Contains("temperature: 0.7", yaml);
    }

    [Fact]
    public void ToYaml_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalAgent = new ExtendedAgentV1
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-agent",
                Owner = "test-owner",
                Tags = new List<string> { "production", "critical" }
            },
            Spec = new ExtendedAgentSpecV1
            {
                Name = "TestAgent",
                Instructions = "You are a helpful assistant.",
                HandoffDescription = "Hand off when needed",
                Handoffs = new List<string> { "ExpertAgent" },
                Tools = new List<string> { "tool1" },
                AllowParallelToolCalls = true,
                MaxReflectionCount = 5,
                Temperature = 0.5f
            }
        };

        // Act
        var yaml = originalAgent.ToYaml();
        var deserializedAgent = ExtendedAgentV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedAgent);
        Assert.Equal(originalAgent.ApiVersion, deserializedAgent.ApiVersion);
        Assert.Equal(originalAgent.Kind, deserializedAgent.Kind);
        Assert.Equal(originalAgent.Metadata.Name, deserializedAgent.Metadata.Name);
        Assert.Equal(originalAgent.Metadata.Owner, deserializedAgent.Metadata.Owner);
        Assert.Equal(originalAgent.Metadata.Tags?.Count, deserializedAgent.Metadata.Tags?.Count);
        Assert.Equal(originalAgent.Spec.Name, deserializedAgent.Spec.Name);
        Assert.Equal(originalAgent.Spec.Instructions, deserializedAgent.Spec.Instructions);
        Assert.Equal(originalAgent.Spec.HandoffDescription, deserializedAgent.Spec.HandoffDescription);
        Assert.Equal(originalAgent.Spec.AllowParallelToolCalls, deserializedAgent.Spec.AllowParallelToolCalls);
        Assert.Equal(originalAgent.Spec.MaxReflectionCount, deserializedAgent.Spec.MaxReflectionCount);
        Assert.Equal(originalAgent.Spec.Temperature, deserializedAgent.Spec.Temperature);
    }

    #endregion
}
