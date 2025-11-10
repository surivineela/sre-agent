using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Runtime.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.UnitTests.Validations;

public class ServerSideYamlValidationTests
{
    private static List<string> CallServerValidateYamlStructure(Dictionary<string, object> rootDocument)
    {
        // Create ExtendedAgentService instance for testing
        var mockLogger = new Mock<ILogger<ExtendedAgentService>>();
        var mockAgentFactory = new Mock<IAgentFactory<AgentContext>>();
        var mockToolFactory = new Mock<IToolFactory<AgentContext>>();
        var mockRepository = new Mock<IExtendedAgentRepository>();

        var service = new ExtendedAgentService(
            mockLogger.Object,
            mockAgentFactory.Object,
            mockToolFactory.Object,
            mockRepository.Object,
            new EmptySkillRegistry()
        );

        return service.ValidateYamlStructure(rootDocument);
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
    public void ServerValidateYamlStructure_WithCorrectStructure_ShouldReturnNoErrors()
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

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region Server-side Indentation Error Tests

    [Fact]
    public void ServerValidateYamlStructure_WithSystemPromptAtRootLevel_ShouldReturnError()
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

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        Assert.Contains(errors, e => e.Contains("Property 'system_prompt' should be under 'spec' section, not at root level. Check indentation."));
    }

    [Fact]
    public void ServerValidateYamlStructure_WithMultiplePropertiesAtRootLevel_ShouldReturnMultipleErrors()
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

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        Assert.Contains(errors, e => e.Contains("'system_prompt' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'tools' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'handoffs' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'temperature' should be under 'spec' section"));
        Assert.True(errors.Count >= 4);
    }

    #endregion

    #region Integration Tests with Real Problematic YAML

    [Fact]
    public void ServerValidateYamlStructure_WithRealBadYamlExample_ShouldCatchIndentationErrors()
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

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);


        // Assert
        Assert.True(errors.Count > 0, "Should have detected indentation errors");
        Assert.Contains(errors, e => e.Contains("'system_prompt' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'tools' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'handoffs' should be under 'spec' section"));
        Assert.Contains(errors, e => e.Contains("'max_reflection_count' should be under 'spec' section"));
    }

    [Fact]
    public void ServerValidateYamlStructure_WithCorrectlyIndentedYaml_ShouldReturnNoErrors()
    {
        // Arrange - corrected version of the problematic YAML
        var goodYaml = @"
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

        var rawDocument = deserializer.Deserialize<object>(goodYaml);
        var rootDocument = ConvertToDictionary(rawDocument);

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region Missing Required Properties Tests

    [Fact]
    public void ServerValidateYamlStructure_WithMissingName_ShouldReturnError()
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

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        Assert.Contains(errors, e => e.Contains("Required property 'name' is missing from 'spec' section"));
    }

    [Fact]
    public void ServerValidateYamlStructure_WithMissingSystemPrompt_ShouldReturnError()
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

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        Assert.Contains(errors, e => e.Contains("Required property 'system_prompt' is missing from 'spec' section"));
    }

    #endregion

    #region Instructions vs System Prompt Tests

    [Fact]
    public void ServerValidateYamlStructure_WithInstructionsInsteadOfSystemPrompt_ShouldReturnError()
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

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        Assert.Contains(errors, e => e.Contains("Use 'system_prompt' instead of 'instructions' in the 'spec' section"));
    }

    #endregion

    #region No Spec Section Tests

    [Fact]
    public void ServerValidateYamlStructure_WithNoSpecSection_ShouldReturnNoErrors()
    {
        // Arrange - no spec section (will be caught by other validation)
        var rootDocument = new Dictionary<string, object>
        {
            ["api_version"] = "azuresre.ai/v1",
            ["kind"] = "AgentConfiguration",
            ["metadata"] = new Dictionary<string, object>()
            // Missing spec section
        };

        // Act
        var errors = CallServerValidateYamlStructure(rootDocument);

        // Assert
        // Should return no errors since this will be caught by other validation layers
        Assert.Empty(errors);
    }

    #endregion
}
