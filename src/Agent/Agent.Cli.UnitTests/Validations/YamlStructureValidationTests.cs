using Agent.Cli.Services;
using System.Reflection;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.UnitTests.Validations;

public class YamlStructureValidationTests
{
    private static List<string> CallValidateYamlStructure(Dictionary<string, object> rootDocument, Dictionary<string, object> specSection)
    {
        // Use reflection to call the private method
        var method = typeof(ApiService).GetMethod("ValidateYamlStructure",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method == null)
        {
            throw new InvalidOperationException("ValidateYamlStructure method not found");
        }

        return (List<string>)method.Invoke(null, new object[] { rootDocument, specSection })!;
    }

    private static Dictionary<string, object> ConvertToDictionary(object obj)
    {
        if (obj is Dictionary<string, object> stringDict)
            return stringDict;

        if (obj is Dictionary<object, object> objectDict)
        {
            return objectDict.ToDictionary(
                kvp => kvp.Key.ToString()!,
                kvp => kvp.Value
            );
        }

        throw new InvalidOperationException($"Cannot convert {obj?.GetType()} to Dictionary<string, object>");
    }

    #region Valid YAML Structure Tests

    [Fact]
    public void ValidateYamlStructure_WithCorrectStructure_ShouldReturnNoErrors()
    {
        // Arrange
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent",
                ["system_prompt"] = "Test instructions",
                ["tools"] = new List<string>(),
                ["handoffs"] = new List<string>()
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region Indentation Error Tests

    [Fact]
    public void ValidateYamlStructure_WithSystemPromptAtRootLevel_ShouldReturnError()
    {
        // Arrange - system_prompt at root level instead of under spec
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["system_prompt"] = "Test instructions at wrong level", // ❌ Wrong indentation
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent"
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Property 'system_prompt' should be under 'spec' section, not at root level. Check indentation."));
    }

    [Fact]
    public void ValidateYamlStructure_WithToolsAtRootLevel_ShouldReturnError()
    {
        // Arrange - tools at root level instead of under spec
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["tools"] = new List<string> { "tool1", "tool2" }, // ❌ Wrong indentation
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent",
                ["system_prompt"] = "Test instructions"
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Property 'tools' should be under 'spec' section, not at root level. Check indentation."));
    }

    [Fact]
    public void ValidateYamlStructure_WithHandoffsAtRootLevel_ShouldReturnError()
    {
        // Arrange - handoffs at root level instead of under spec
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["handoffs"] = new List<string> { "agent1", "agent2" }, // ❌ Wrong indentation
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent",
                ["system_prompt"] = "Test instructions"
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Property 'handoffs' should be under 'spec' section, not at root level. Check indentation."));
    }

    [Fact]
    public void ValidateYamlStructure_WithMultiplePropertiesAtRootLevel_ShouldReturnMultipleErrors()
    {
        // Arrange - multiple properties at wrong level
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["system_prompt"] = "Test instructions", // ❌ Wrong indentation
            ["tools"] = new List<string>(), // ❌ Wrong indentation
            ["handoffs"] = new List<string>(), // ❌ Wrong indentation
            ["temperature"] = 0.7, // ❌ Wrong indentation
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent"
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("'system_prompt' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'tools' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'handoffs' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'temperature' should be under 'spec' section"));
        Assert.True(errors.Count >= 4);
    }

    #endregion

    #region Missing Required Properties Tests

    [Fact]
    public void ValidateYamlStructure_WithMissingName_ShouldReturnError()
    {
        // Arrange - missing name in spec
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["spec"] = new Dictionary<string, object>
            {
                ["system_prompt"] = "Test instructions"
                // Missing name
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Required property 'name' is missing from 'spec' section"));
    }

    [Fact]
    public void ValidateYamlStructure_WithMissingSystemPrompt_ShouldReturnError()
    {
        // Arrange - missing system_prompt in spec
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent"
                // Missing system_prompt
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Required property 'system_prompt' is missing from 'spec' section"));
    }

    [Fact]
    public void ValidateYamlStructure_WithSystemPromptAtRootAndMissingInSpec_ShouldReturnSpecificError()
    {
        // Arrange - system_prompt at root level, missing in spec
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["system_prompt"] = "Test instructions", // ❌ At root level
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent"
                // Missing system_prompt in spec
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Property 'system_prompt' found at root level - should be under 'spec' section. Check indentation."));
    }

    #endregion

    #region Instructions vs System Prompt Tests

    [Fact]
    public void ValidateYamlStructure_WithInstructionsInsteadOfSystemPrompt_ShouldReturnError()
    {
        // Arrange - using 'instructions' instead of 'system_prompt'
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent",
                ["instructions"] = "Test instructions" // ❌ Should be 'system_prompt'
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Use 'system_prompt' instead of 'instructions' in the 'spec' section"));
    }

    [Fact]
    public void ValidateYamlStructure_WithInstructionsAtRootLevel_ShouldReturnSpecificError()
    {
        // Arrange - instructions at root level
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["instructions"] = "Test instructions", // ❌ At root level and wrong name
            ["spec"] = new Dictionary<string, object>
            {
                ["name"] = "TestAgent"
            }
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("Property 'instructions' found at root level - should be under 'spec' section and renamed to 'system_prompt'. Check indentation."));
    }

    #endregion

    #region Empty Spec Tests

    [Fact]
    public void ValidateYamlStructure_WithEmptySpec_ShouldReturnError()
    {
        // Arrange - empty spec section
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>(),
            ["spec"] = new Dictionary<string, object>() // Empty spec
        };

        var specSection = (Dictionary<string, object>)rootDocument["spec"];

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.Contains(errors, e => e.Contains("'spec' section is empty - agent properties should be defined here"));
    }

    #endregion

    #region Integration Tests with Real YAML

    [Fact]
    public void ValidateYamlStructure_WithRealBadYamlExample_ShouldCatchIndentationErrors()
    {
        // Arrange - this is the actual problematic YAML from the user
        var badYaml = @"
api_version: azuresre.ai/v1
kind: AgentConfiguration
metadata: {}
spec:
  name: FunctionAppTelemetryRouterAgent
system_prompt: |
    You are SRE Agent specialized in detect any Azure Function App Application Insights telemetry issue.
tools: []
handoffs:
    - FunctionAppMissingLogsOTelAgent
    - FunctionAppMissingLogsAIAgent
max_reflection_count: 0";

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var rawDocument = deserializer.Deserialize<object>(badYaml);
        var rootDocument = ConvertToDictionary(rawDocument);
        var specSection = ConvertToDictionary(rootDocument["spec"]);

        // Act
        var errors = CallValidateYamlStructure(rootDocument, specSection);

        // Assert
        Assert.True(errors.Count > 0, "Should have detected indentation errors");
        Assert.Contains(errors, e => e.Contains("'system_prompt' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'tools' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'handoffs' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'max_reflection_count' should be under 'spec' section"));
    }

    #endregion
}