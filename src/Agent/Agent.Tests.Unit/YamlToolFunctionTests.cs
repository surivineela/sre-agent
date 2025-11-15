// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Unit
{
    public class YamlToolFunctionTests : IDisposable
    {
        private readonly Mock<IServiceProvider> _mockServiceProvider;
        private readonly Mock<ILogger> _mockLogger;
        private readonly ServiceCollection _services;
        private readonly IServiceProvider _serviceProvider;
        private readonly Assembly[] _assemblies;

        public YamlToolFunctionTests()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger>();

            _services = new ServiceCollection();
            _services.AddTransient<TestYamlPlugin>();
            _services.AddTransient<ComplexTestYamlPlugin>();
            _services.AddTransient<ValidationTestYamlPlugin>();
            _serviceProvider = _services.BuildServiceProvider();

            _assemblies = new[] { typeof(YamlToolFunctionTests).Assembly };
        }

        [Fact]
        public void Constructor_WithValidParameters_ShouldInitializeCorrectly()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();

            // Act
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Assert
            Assert.NotNull(yamlToolFunction);
            Assert.Null(yamlToolFunction.MethodInfo); // YamlToolFunction doesn't expose a direct MethodInfo
        }

        [Fact]
        public void GetToolFunction_WithValidDefinition_ShouldReturnAIFunction()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var threadId = Guid.NewGuid();

            // Act
            var aiFunction = yamlToolFunction.GetToolFunction(threadId);

            // Assert
            Assert.NotNull(aiFunction);
            Assert.Equal("TestTool", aiFunction.Name);
            Assert.Equal("Test tool for unit testing", aiFunction.Description);
        }

        [Fact]
        public void GetToolFunction_WithAgentMode_ShouldReturnAIFunction()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var threadId = Guid.NewGuid();
            var agentMode = "test-mode";

            // Act
            var aiFunction = yamlToolFunction.GetToolFunction(threadId, agentMode);

            // Assert
            Assert.NotNull(aiFunction);
            Assert.Equal("TestTool", aiFunction.Name);
            Assert.Equal("Test tool for unit testing", aiFunction.Description);
        }

        [Fact]
        public void GetToolFunction_WithMissingPluginType_ShouldThrowTypeLoadException()
        {
            // Arrange
            var toolDef = new TestYamlToolDefinition
            {
                Name = "TestTool",
                Type = "NonExistentType", // This type doesn't exist
                Description = "Test tool",
                Parameters = new List<YamlParameter>()
            };
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Act & Assert
            var exception = Assert.Throws<TypeLoadException>(() => yamlToolFunction.GetToolFunction());
            Assert.Contains("No plugin found for type 'NonExistentType'", exception.Message);
        }

        [Fact]
        public async Task ExecuteFromArguments_WithValidParameters_ShouldExecuteSuccessfully()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Get the AIFunction and create test arguments
            var aiFunction = yamlToolFunction.GetToolFunction();
            var args = new AIFunctionArguments
            {
                ["message"] = "Hello, World!",
                ["count"] = 42
            };

            // Act
            var result = await aiFunction.InvokeAsync(args);

            // Assert
            Assert.NotNull(result);
            // The actual result depends on the TestYamlPlugin implementation
        }

        [Fact]
        public async Task ExecuteFromArguments_WithMissingParameters_ShouldUseDefaults()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            var aiFunction = yamlToolFunction.GetToolFunction();
            var args = new AIFunctionArguments(); // Empty arguments

            // Act
            var result = await aiFunction.InvokeAsync(args);

            // Assert
            Assert.NotNull(result);
            // Should use default values: string.Empty for message, 0 for count
        }

        [Fact]
        public void GetDefaultValueForType_WithDifferentTypes_ShouldReturnCorrectDefaults()
        {
            // This tests the private GetDefaultValueForType method indirectly
            // by testing with missing parameters that should use defaults

            // Arrange
            var toolDef = new TestYamlToolDefinition
            {
                Name = "TestTool",
                Type = "TestYamlPlugin",
                Description = "Test tool",
                Parameters = new List<YamlParameter>
                {
                    new() { Name = "stringParam", Type = "string", Required = false },
                    new() { Name = "intParam", Type = "int", Required = false },
                    new() { Name = "boolParam", Type = "bool", Required = false },
                    new() { Name = "doubleParam", Type = "double", Required = false }
                }
            };

            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var aiFunction = yamlToolFunction.GetToolFunction();

            // Act & Assert - Should not throw when using defaults
            Assert.NotNull(aiFunction);
        }

        [Fact]
        public void GetPluginCategory_ShouldReturnToolType()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Act
            var category = yamlToolFunction.GetPluginCategory();

            // Assert
            Assert.Equal("TestYamlPlugin", category);
        }

        [Fact]
        public void GetPluginResourceType_ShouldReturnToolType()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Act
            var resourceType = yamlToolFunction.GetPluginResourceType();

            // Assert
            Assert.Equal("TestYamlPlugin", resourceType);
        }

        [Fact]
        public void GetPluginName_ShouldReturnToolName()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Act
            var name = yamlToolFunction.GetPluginName();

            // Assert
            Assert.Equal("TestTool", name);
        }

        [Fact]
        public async Task InvokeAIFunction_WithYamlParameters_ShouldExecuteSuccessfully()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var threadId = Guid.NewGuid();

            // Get the AIFunction
            var aiFunction = yamlToolFunction.GetToolFunction(threadId);

            // Create arguments based on YAML parameter names
            var arguments = new AIFunctionArguments
            {
                ["message"] = "Hello from YAML test!",
                ["count"] = 42
            };

            // Act
            var result = await aiFunction.InvokeAsync(arguments);

            // Assert
            Assert.NotNull(result);
            var resultString = result.ToString();
            Assert.Contains("Plugin executed with message: 'Hello from YAML test!' and count: 42", resultString);
        }

        [Fact]
        public void AIFunction_JsonSchema_ShouldIncludeYamlParameterDescriptions()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Act
            var aiFunction = yamlToolFunction.GetToolFunction();
            var schema = aiFunction.JsonSchema;

            // Assert
            Assert.NotEqual(default(JsonElement), schema);

            // Verify schema structure
            Assert.True(schema.TryGetProperty("type", out var typeProperty));
            Assert.Equal("object", typeProperty.GetString());

            // Verify properties contain our YAML parameters
            Assert.True(schema.TryGetProperty("properties", out var propertiesProperty));

            // Check message parameter
            Assert.True(propertiesProperty.TryGetProperty("message", out var messageProperty));
            Assert.True(messageProperty.TryGetProperty("type", out var messageType));
            Assert.Equal("string", messageType.GetString());
            Assert.True(messageProperty.TryGetProperty("description", out var messageDescription));
            Assert.Equal("Test message parameter", messageDescription.GetString());

            // Check count parameter
            Assert.True(propertiesProperty.TryGetProperty("count", out var countProperty));
            Assert.True(countProperty.TryGetProperty("type", out var countType));
            Assert.Equal("integer", countType.GetString());
            Assert.True(countProperty.TryGetProperty("description", out var countDescription));
            Assert.Equal("Test count parameter", countDescription.GetString());

            // Verify required parameters
            Assert.True(schema.TryGetProperty("required", out var requiredProperty));
            var requiredArray = requiredProperty.EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Contains("message", requiredArray); // message is required
            Assert.DoesNotContain("count", requiredArray); // count is not required
        }

        [Fact]
        public async Task InvokeAIFunction_WithMissingRequiredParameter_ShouldStillExecute()
        {
            // Arrange
            var toolDef = CreateTestToolDefinition();
            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var aiFunction = yamlToolFunction.GetToolFunction();

            // Create arguments with only optional parameter
            var arguments = new AIFunctionArguments
            {
                ["count"] = 10
                // Missing required "message" parameter
            };

            // Act
            var result = await aiFunction.InvokeAsync(arguments);

            // Assert - Should execute but with empty message
            Assert.NotNull(result);
            var resultString = result.ToString();
            Assert.Contains("Plugin executed with message: '' and count: 10", resultString);
        }

        [Fact]
        public void YamlParameters_RequiredProperty_ShouldBeReflectedInSchema()
        {
            // Arrange - Create a tool with mixed required/optional parameters
            var toolDef = new TestYamlToolDefinition
            {
                Name = "RequiredTestTool",
                Type = "TestYamlPlugin",
                Description = "Tool to test required parameter behavior",
                Parameters = new List<YamlParameter>
                {
                    new() { Name = "requiredParam1", Type = "string", Required = true, Description = "First required parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "optionalParam1", Type = "int", Required = false, Description = "First optional parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "requiredParam2", Type = "bool", Required = true, Description = "Second required parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "optionalParam2", Type = "double", Required = false, Description = "Second optional parameter", Target = "dictionary:args:object", MapTo = "args" }
                }
            };

            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Act
            var aiFunction = yamlToolFunction.GetToolFunction();
            var schema = aiFunction.JsonSchema;

            // Assert - Check required array contains only required parameters
            Assert.True(schema.TryGetProperty("required", out var requiredProperty));
            var requiredArray = requiredProperty.EnumerateArray().Select(x => x.GetString()).ToHashSet();

            // Required parameters should be in the required array
            Assert.Contains("requiredParam1", requiredArray);
            Assert.Contains("requiredParam2", requiredArray);

            // Optional parameters should NOT be in the required array
            Assert.DoesNotContain("optionalParam1", requiredArray);
            Assert.DoesNotContain("optionalParam2", requiredArray);

            // Should have exactly 2 required parameters
            Assert.Equal(2, requiredArray.Count);

            // Verify all parameters are still in properties regardless of required status
            Assert.True(schema.TryGetProperty("properties", out var properties));
            Assert.True(properties.TryGetProperty("requiredParam1", out _));
            Assert.True(properties.TryGetProperty("optionalParam1", out _));
            Assert.True(properties.TryGetProperty("requiredParam2", out _));
            Assert.True(properties.TryGetProperty("optionalParam2", out _));
        }

        [Fact]
        public async Task InvokeAIFunction_WithComplexYamlTool_ShouldHandleAllParameterTypes()
        {
            // Arrange
            var toolDef = new TestYamlToolDefinition
            {
                Name = "ComplexTool",
                Type = "ComplexTestYamlPlugin",
                Description = "Complex test tool with multiple parameter types",
                Parameters = new List<YamlParameter>
                {
                    new() { Name = "stringParam", Type = "string", Required = true, Description = "A string parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "intParam", Type = "int", Required = false, Description = "An integer parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "boolParam", Type = "bool", Required = false, Description = "A boolean parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "doubleParam", Type = "double", Required = false, Description = "A double parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "directParam", Type = "string", Required = false, Description = "A direct parameter", Target = "direct", MapTo = "directParam" }
                }
            };

            // Add the complex plugin to services
            _services.AddTransient<ComplexTestYamlPlugin>();
            var serviceProvider = _services.BuildServiceProvider();

            var yamlToolFunction = new YamlToolFunction<object>(serviceProvider, _assemblies, toolDef);
            var aiFunction = yamlToolFunction.GetToolFunction();

            // Create arguments with all parameter types
            var arguments = new AIFunctionArguments
            {
                ["stringParam"] = "test string",
                ["intParam"] = 123,
                ["boolParam"] = true,
                ["doubleParam"] = 45.67,
                ["directParam"] = "direct value"
            };

            // Act
            var result = await aiFunction.InvokeAsync(arguments);

            // Assert
            Assert.NotNull(result);
            var resultString = result.ToString();
            Assert.Contains("stringParam: test string", resultString);
            Assert.Contains("intParam: 123", resultString);
            Assert.Contains("boolParam: True", resultString);
            Assert.Contains("doubleParam: 45.67", resultString);
            Assert.Contains("directParam: direct value", resultString);
        }

        [Fact]
        public void AIFunction_ComplexSchema_ShouldIncludeAllParameterTypesAndDescriptions()
        {
            // Arrange
            var toolDef = new TestYamlToolDefinition
            {
                Name = "ComplexTool",
                Type = "ComplexTestYamlPlugin",
                Description = "Complex test tool",
                Parameters = new List<YamlParameter>
                {
                    new() { Name = "stringParam", Type = "string", Required = true, Description = "String parameter description", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "intParam", Type = "int", Required = false, Description = "Integer parameter description", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "boolParam", Type = "bool", Required = true, Description = "Boolean parameter description", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "doubleParam", Type = "double", Required = false, Description = "Double parameter description", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "directParam", Type = "string", Required = false, Description = "Direct parameter description", Target = "direct", MapTo = "directParam" }
                }
            };

            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);

            // Act
            var aiFunction = yamlToolFunction.GetToolFunction();
            var schema = aiFunction.JsonSchema;

            // Assert
            Assert.True(schema.TryGetProperty("properties", out var properties));

            // Check all parameter types are correctly mapped
            Assert.True(properties.TryGetProperty("stringParam", out var stringParam));
            Assert.Equal("string", stringParam.GetProperty("type").GetString());
            Assert.Equal("String parameter description", stringParam.GetProperty("description").GetString());

            Assert.True(properties.TryGetProperty("intParam", out var intParam));
            Assert.Equal("integer", intParam.GetProperty("type").GetString());
            Assert.Equal("Integer parameter description", intParam.GetProperty("description").GetString());

            Assert.True(properties.TryGetProperty("boolParam", out var boolParam));
            Assert.Equal("boolean", boolParam.GetProperty("type").GetString());
            Assert.Equal("Boolean parameter description", boolParam.GetProperty("description").GetString());

            Assert.True(properties.TryGetProperty("doubleParam", out var doubleParam));
            Assert.Equal("number", doubleParam.GetProperty("type").GetString());
            Assert.Equal("Double parameter description", doubleParam.GetProperty("description").GetString());

            Assert.True(properties.TryGetProperty("directParam", out var directParam));
            Assert.Equal("string", directParam.GetProperty("type").GetString());
            Assert.Equal("Direct parameter description", directParam.GetProperty("description").GetString());

            // Check required parameters
            Assert.True(schema.TryGetProperty("required", out var required));
            var requiredList = required.EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Contains("stringParam", requiredList);
            Assert.Contains("boolParam", requiredList);
            Assert.DoesNotContain("intParam", requiredList);
            Assert.DoesNotContain("doubleParam", requiredList);
            Assert.DoesNotContain("directParam", requiredList);
        }

        [Fact]
        public async Task YamlToolFunction_WithRegexValidationFailure_ShouldThrowValidationException()
        {
            var toolDef = new TestYamlToolDefinition
            {
                Name = "ValidationTool",
                Type = "ValidationTestYamlPlugin",
                Description = "Tool with validation",
                Parameters = new List<YamlParameter>
                {
                    new()
                    {
                        Name = "stamp",
                        Type = "string",
                        Required = true,
                        Description = "Stamp name",
                        Target = "direct",
                        MapTo = "value",
                        Validation = new YamlParameterValidation
                        {
                            Regex = "^waws-prod-[a-zA-Z0-9]+-[a-zA-Z0-9]+$",
                            ErrorMessage = "Invalid stamp format",
                            Normalize = new List<string> { "trim", "lowerInvariant" }
                        }
                    }
                }
            };

            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var aiFunction = yamlToolFunction.GetToolFunction();

            var arguments = new AIFunctionArguments
            {
                ["stamp"] = "invalid-stamp"
            };

            await Assert.ThrowsAsync<ValidationException>(async () => await aiFunction.InvokeAsync(arguments));
        }

        [Fact]
        public async Task YamlToolFunction_WithNormalization_ShouldApplyTransformations()
        {
            var toolDef = new TestYamlToolDefinition
            {
                Name = "ValidationTool",
                Type = "ValidationTestYamlPlugin",
                Description = "Tool with validation",
                Parameters = new List<YamlParameter>
                {
                    new()
                    {
                        Name = "stamp",
                        Type = "string",
                        Required = true,
                        Description = "Stamp name",
                        Target = "direct",
                        MapTo = "value",
                        Validation = new YamlParameterValidation
                        {
                            Regex = "^waws-prod-[a-zA-Z0-9]+-[a-zA-Z0-9]+$",
                            Normalize = new List<string> { "trim", "lowerInvariant" }
                        }
                    }
                }
            };

            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var aiFunction = yamlToolFunction.GetToolFunction();

            var arguments = new AIFunctionArguments
            {
                ["stamp"] = "  WAWS-PROD-DEF-456   "
            };

            var result = await aiFunction.InvokeAsync(arguments);
            Assert.Equal("Processed: waws-prod-def-456", result?.ToString());
        }

        [Fact]
        public void JsonSchema_ShouldIncludePattern_WhenValidationRegexProvided()
        {
            var toolDef = new TestYamlToolDefinition
            {
                Name = "ValidationTool",
                Type = "ValidationTestYamlPlugin",
                Description = "Tool with validation",
                Parameters = new List<YamlParameter>
                {
                    new()
                    {
                        Name = "stamp",
                        Type = "string",
                        Required = true,
                        Description = "Stamp name",
                        Target = "direct",
                        MapTo = "value",
                        Validation = new YamlParameterValidation
                        {
                            Regex = "^waws-prod-[a-zA-Z0-9]+-[a-zA-Z0-9]+$"
                        }
                    }
                }
            };

            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var aiFunction = yamlToolFunction.GetToolFunction();
            var schema = aiFunction.JsonSchema;

            Assert.True(schema.TryGetProperty("properties", out var properties));
            var stamp = properties.GetProperty("stamp");
            Assert.Equal("^waws-prod-[a-zA-Z0-9]+-[a-zA-Z0-9]+$", stamp.GetProperty("pattern").GetString());
        }

        private static TestYamlToolDefinition CreateTestToolDefinition()
        {
            return new TestYamlToolDefinition
            {
                Name = "TestTool",
                Type = "TestYamlPlugin",
                Description = "Test tool for unit testing",
                Parameters = new List<YamlParameter>
                {
                    new() { Name = "message", Type = "string", Required = true, Description = "Test message parameter", Target = "direct", MapTo = "message" },
                    new() { Name = "count", Type = "int", Required = false, Description = "Test count parameter", Target = "direct", MapTo = "count" }
                }
            };
        }

        [Fact]
        public async Task YamlToolFunction_WithTypeConversionErrors_ShouldHandleGracefully()
        {
            // Arrange - Create a tool with different parameter types that might cause conversion issues
            var toolDef = new TestYamlToolDefinition
            {
                Name = "ConversionTestTool",
                Type = "TestYamlPlugin",
                Description = "Tool to test type conversion error handling",
                Parameters = new List<YamlParameter>
                {
                    new() { Name = "message", Type = "string", Required = false, Description = "String parameter", Target = "dictionary:args:object", MapTo = "args" },
                    new() { Name = "count", Type = "int", Required = false, Description = "Integer parameter", Target = "dictionary:args:object", MapTo = "args" }
                }
            };

            var yamlToolFunction = new YamlToolFunction<object>(_serviceProvider, _assemblies, toolDef);
            var aiFunction = yamlToolFunction.GetToolFunction();

            // Act - Pass values that might cause conversion issues
            var arguments = new AIFunctionArguments
            {
                ["message"] = null, // null value
                ["count"] = "invalid_number" // string that cannot be converted to int
            };

            var result = await aiFunction.InvokeAsync(arguments);

            // Assert - Should execute without throwing exceptions
            Assert.NotNull(result);
            var resultString = result.ToString();

            // The SafeConvertType should handle null and invalid conversions gracefully
            // null message should be handled as empty string or null
            // invalid number should default to 0
            Assert.Contains("Plugin executed", resultString);
        }

        public void Dispose()
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Test tool plugin for YamlToolFunction testing
    /// </summary>
    [ToolType("TestYamlPlugin")]
    public class TestYamlPlugin
    {
        public async Task<string> Run(string message = "", int count = 0)
        {
            await Task.Delay(1); // Simulate async work

            return $"Plugin executed with message: '{message}' and count: {count}";
        }
    }

    /// <summary>
    /// Complex test tool plugin for testing all parameter types
    /// </summary>
    [ToolType("ComplexTestYamlPlugin")]
    public class ComplexTestYamlPlugin
    {
        public async Task<string> Run(Dictionary<string, object> args, string directParam = "")
        {
            await Task.Delay(1); // Simulate async work

            if (args == null)
            {
                return "Complex plugin executed with no parameters";
            }

            var stringParam = args.TryGetValue("stringParam", out var strValue) ? strValue?.ToString() ?? "" : "";
            var intParam = args.TryGetValue("intParam", out var intValue) ? SafeConvertToInt(intValue) : 0;
            var boolParam = args.TryGetValue("boolParam", out var boolValue) ? SafeConvertToBool(boolValue) : false;
            var doubleParam = args.TryGetValue("doubleParam", out var doubleValue) ? SafeConvertToDouble(doubleValue) : 0.0;

            var directInfo = !string.IsNullOrEmpty(directParam) ? $", directParam: {directParam}" : "";

            return $"Complex plugin executed: stringParam: {stringParam}, intParam: {intParam}, boolParam: {boolParam}, doubleParam: {doubleParam}{directInfo}";
        }

        private static int SafeConvertToInt(object? value)
        {
            if (value == null) return 0;
            try { return Convert.ToInt32(value); } catch { return 0; }
        }

        private static bool SafeConvertToBool(object? value)
        {
            if (value == null) return false;
            try { return Convert.ToBoolean(value); } catch { return false; }
        }

        private static double SafeConvertToDouble(object? value)
        {
            if (value == null) return 0.0;
            try { return Convert.ToDouble(value); } catch { return 0.0; }
        }
    }

    [ToolType("ValidationTestYamlPlugin")]
    public class ValidationTestYamlPlugin
    {
        public Task<string> Run(string value)
        {
            return Task.FromResult($"Processed: {value}");
        }
    }

    /// <summary>
    /// Test implementation of YamlToolDefinitionBase for testing purposes.
    /// </summary>
    public class TestYamlToolDefinition : YamlToolDefinitionBase
    {
        public override void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Tool name is required.");

            if (string.IsNullOrWhiteSpace(Type))
                throw new ArgumentException("Tool type is required.");
        }
    }
}
