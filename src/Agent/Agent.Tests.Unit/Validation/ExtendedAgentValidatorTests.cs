// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Web.Validation;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Agent.Tests.Unit.Validation;

public class ExtendedAgentValidatorTests
{
    private readonly Mock<ILogger<ExtendedAgentValidator>> _mockLogger;
    private readonly Mock<IExtendedAgentRepository> _mockRepository;
    private readonly ExtendedAgentValidator _validator;

    #region Test Data

    /// <summary>
    /// Valid resource names that should pass validation.
    /// Pattern: ^[a-zA-Z0-9\-_]{1,128}$
    /// </summary>
    public static TheoryData<string> ValidNames => new()
    {
        "valid-name",
        "valid_name",
        "ValidName",
        "validname123",
        "a",
        "A",
        "1",
        "test-agent_123",
        "MyAgent-v2_test"
    };

    /// <summary>
    /// Names with special characters that should fail validation.
    /// </summary>
    public static TheoryData<string> InvalidNamesWithSpecialCharacters => new()
    {
        "name!invalid",
        "name@invalid",
        "name#invalid",
        "name$invalid",
        "name%invalid",
        "name^invalid",
        "name&invalid",
        "name*invalid",
        "name(invalid)",
        "name.invalid",
        "name/invalid",
        "name\\invalid",
        "name:invalid",
        "name;invalid",
        "name'invalid",
        "name\"invalid",
        "name<invalid>",
        "name,invalid",
        "name?invalid",
        "name|invalid",
        "name`invalid",
        "name~invalid",
        "name+invalid",
        "name=invalid",
        "name[invalid]",
        "name{invalid}"
    };

    /// <summary>
    /// Empty or null names that should fail validation.
    /// </summary>
    public static TheoryData<string?> EmptyOrNullNames => new()
    {
        "",
        null
    };

    #endregion

    public ExtendedAgentValidatorTests()
    {
        _mockLogger = new Mock<ILogger<ExtendedAgentValidator>>();
        _mockRepository = new Mock<IExtendedAgentRepository>();
        _validator = new ExtendedAgentValidator(_mockLogger.Object, _mockRepository.Object);
    }

    #region Agent Validation Tests

    [Theory]
    [MemberData(nameof(ValidNames))]
    public async Task ValidateAgentAsync_ValidName_NameValidationPasses(string name)
    {
        // Arrange
        var model = CreateAgentDocumentModel(name);

        // Act
        var result = await _validator.ValidateAgentAsync(model);

        // Assert - Name validation should pass
        Assert.DoesNotContain(result.Errors, e => e.Contains("Resource name"));
    }

    [Theory]
    [MemberData(nameof(EmptyOrNullNames))]
    public async Task ValidateAgentAsync_EmptyOrNullName_ReturnsError(string? name)
    {
        // Arrange
        var model = CreateAgentDocumentModel(name ?? string.Empty);

        // Act
        var result = await _validator.ValidateAgentAsync(model);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("Resource name is required"));
    }

    [Theory]
    [MemberData(nameof(InvalidNamesWithSpecialCharacters))]
    public async Task ValidateAgentAsync_NameWithSpecialCharacters_ReturnsNameError(string name)
    {
        // Arrange
        var model = CreateAgentDocumentModel(name);

        // Act
        var result = await _validator.ValidateAgentAsync(model);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    [Fact]
    public async Task ValidateAgentAsync_NameExceeds128Characters_ReturnsError()
    {
        // Arrange
        var longName = new string('a', 129);
        var model = CreateAgentDocumentModel(longName);

        // Act
        var result = await _validator.ValidateAgentAsync(model);

        // Assert
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    [Fact]
    public async Task ValidateAgentAsync_NameExactly128Characters_ReturnsNoNameError()
    {
        // Arrange
        var name = new string('a', 128);
        var model = CreateAgentDocumentModel(name);

        // Act
        var result = await _validator.ValidateAgentAsync(model);

        // Assert - Name validation should pass
        Assert.DoesNotContain(result.Errors, e => e.Contains("Resource name"));
    }

    #endregion

    #region Tool Validation Tests

    [Theory]
    [MemberData(nameof(ValidNames))]
    public async Task ValidateToolAsync_ValidName_ReturnsNoErrors(string name)
    {
        // Arrange
        var model = CreateToolDocumentModel(name);

        // Act
        var result = await _validator.ValidateToolAsync(model);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [MemberData(nameof(EmptyOrNullNames))]
    public async Task ValidateToolAsync_EmptyOrNullName_ReturnsError(string? name)
    {
        // Arrange
        var model = CreateToolDocumentModel(name ?? string.Empty);

        // Act
        var result = await _validator.ValidateToolAsync(model);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Resource name is required"));
    }

    [Theory]
    [MemberData(nameof(InvalidNamesWithSpecialCharacters))]
    public async Task ValidateToolAsync_NameWithSpecialCharacters_ReturnsError(string name)
    {
        // Arrange
        var model = CreateToolDocumentModel(name);

        // Act
        var result = await _validator.ValidateToolAsync(model);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    [Fact]
    public async Task ValidateToolAsync_NameExceeds128Characters_ReturnsError()
    {
        // Arrange
        var longName = new string('a', 129);
        var model = CreateToolDocumentModel(longName);

        // Act
        var result = await _validator.ValidateToolAsync(model);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    [Fact]
    public async Task ValidateToolAsync_NameExactly128Characters_ReturnsNoErrors()
    {
        // Arrange
        var name = new string('a', 128);
        var model = CreateToolDocumentModel(name);

        // Act
        var result = await _validator.ValidateToolAsync(model);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region Connector Validation Tests

    [Theory]
    [MemberData(nameof(ValidNames))]
    public async Task ValidateConnectorAsync_ValidName_ReturnsNoErrors(string name)
    {
        // Arrange
        var model = CreateConnectorDocumentModel(name);

        // Act
        var result = await _validator.ValidateConnectorAsync(model);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [MemberData(nameof(InvalidNamesWithSpecialCharacters))]
    public async Task ValidateConnectorAsync_NameWithSpecialCharacters_ReturnsError(string name)
    {
        // Arrange
        var model = CreateConnectorDocumentModel(name);

        // Act
        var result = await _validator.ValidateConnectorAsync(model);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    #endregion

    #region PluginConfig Validation Tests

    [Theory]
    [MemberData(nameof(ValidNames))]
    public async Task ValidatePluginConfigAsync_ValidName_ReturnsNoErrors(string name)
    {
        // Arrange
        var model = CreatePlugInConfigDocumentModel(name);

        // Act
        var result = await _validator.ValidatePluginConfigAsync(model);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [MemberData(nameof(InvalidNamesWithSpecialCharacters))]
    public async Task ValidatePluginConfigAsync_NameWithSpecialCharacters_ReturnsError(string name)
    {
        // Arrange
        var model = CreatePlugInConfigDocumentModel(name);

        // Act
        var result = await _validator.ValidatePluginConfigAsync(model);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    #endregion

    #region CommonPrompt Validation Tests

    [Theory]
    [MemberData(nameof(ValidNames))]
    public async Task ValidateCommonPromptAsync_ValidName_ReturnsNoErrors(string name)
    {
        // Arrange
        var model = CreateCommonPromptDocumentModel(name);

        // Act
        var result = await _validator.ValidateCommonPromptAsync(model);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [MemberData(nameof(InvalidNamesWithSpecialCharacters))]
    public async Task ValidateCommonPromptAsync_NameWithSpecialCharacters_ReturnsError(string name)
    {
        // Arrange
        var model = CreateCommonPromptDocumentModel(name);

        // Act
        var result = await _validator.ValidateCommonPromptAsync(model);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    #endregion

    #region CommonToolsList Validation Tests

    [Theory]
    [MemberData(nameof(ValidNames))]
    public async Task ValidateCommonToolsListAsync_ValidName_ReturnsNoErrors(string name)
    {
        // Arrange
        var model = CreateCommonToolsListDocumentModel(name);

        // Act
        var result = await _validator.ValidateCommonToolsListAsync(model);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [MemberData(nameof(InvalidNamesWithSpecialCharacters))]
    public async Task ValidateCommonToolsListAsync_NameWithSpecialCharacters_ReturnsError(string name)
    {
        // Arrange
        var model = CreateCommonToolsListDocumentModel(name);

        // Act
        var result = await _validator.ValidateCommonToolsListAsync(model);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("is invalid") && e.Contains("Name must be 1-128 characters"));
    }

    #endregion

    #region Helper Methods

    private static ToolDocumentModel CreateToolDocumentModel(string name)
    {
        return new ToolDocumentModel(
            new ResourceMetadata { Name = name },
            new ToolSpec { Type = "TestTool", Description = "Test tool description" }
        );
    }

    private static ConnectorDocumentModel CreateConnectorDocumentModel(string name)
    {
        return new ConnectorDocumentModel(
            new ResourceMetadata { Name = name },
            new ConnectorSpec { Type = "TestConnector", Description = "Test connector description" }
        );
    }

    private static PlugInConfigDocumentModel CreatePlugInConfigDocumentModel(string name)
    {
        return new PlugInConfigDocumentModel(
            new ResourceMetadata { Name = name },
            new PluginConfigSpec()
        );
    }

    private static CommonPromptDocumentModel CreateCommonPromptDocumentModel(string name)
    {
        return new CommonPromptDocumentModel(
            new ResourceMetadata { Name = name },
            new CommonPromptSpec { Prompt = "Test prompt content" }
        );
    }

    private static CommonToolsListDocumentModel CreateCommonToolsListDocumentModel(string name)
    {
        return new CommonToolsListDocumentModel(
            new ResourceMetadata { Name = name },
            new CommonToolListSpec { CommonToolsList = new List<string> { "tool1", "tool2" } }
        );
    }

    private static AgentDocumentModel CreateAgentDocumentModel(string name)
    {
        return new AgentDocumentModel(
            new ResourceMetadata { Name = name },
            new AgentSpec
            {
                Instructions = "This is a test agent with sufficient instructions to pass the minimum length validation requirement",
                HandoffDescription = string.Empty,
                Handoffs = new List<string>()
            }
        );
    }

    #endregion
}
