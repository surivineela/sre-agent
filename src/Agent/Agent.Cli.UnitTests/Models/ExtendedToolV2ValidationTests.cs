// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class ExtendedToolV2ValidationTests
{
    #region Metadata Validation Tests

    [Fact]
    public void Validate_WithEmptyName_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "" },
            Spec = new ToolSpecV2 { Type = "KustoTool", Description = "Test" }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a name in metadata"));
    }

    [Fact]
    public void Validate_WithNullName_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = null },
            Spec = new ToolSpecV2 { Type = "KustoTool", Description = "Test" }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a name in metadata"));
    }

    [Fact]
    public void Validate_WithWhitespaceName_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "   " },
            Spec = new ToolSpecV2 { Type = "KustoTool", Description = "Test" }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a name in metadata"));
    }

    #endregion

    #region Spec Validation Tests

    [Fact]
    public void Validate_WithNullSpec_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = null!
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a spec section"));
    }

    [Fact]
    public void Validate_WithEmptyType_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new ToolSpecV2 { Type = "", Description = "Test" }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a 'type' specified"));
    }

    [Fact]
    public void Validate_WithNullType_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new ToolSpecV2 { Type = null, Description = "Test" }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a 'type' specified"));
    }

    [Fact]
    public void Validate_WithEmptyDescription_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new ToolSpecV2 { Type = "KustoTool", Description = "" }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a 'description'"));
    }

    [Fact]
    public void Validate_WithNullDescription_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new ToolSpecV2 { Type = "KustoTool", Description = null }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a 'description'"));
    }

    [Fact]
    public void Validate_WithUnsupportedType_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new ToolSpecV2 { Type = "UnsupportedTool", Description = "Test description" }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Unsupported tool type: UnsupportedTool"));
    }

    #endregion

    #region KustoTool Validation Tests

    [Fact]
    public void Validate_KustoTool_WithMissingConnector_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "",
                Database = "TestDB",
                Query = "TestQuery | take 10"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("KustoTool must have a 'connector' specified"));
    }

    [Fact]
    public void Validate_KustoTool_WithMissingDatabase_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "",
                Query = "TestQuery | take 10"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("KustoTool must have a 'database' specified"));
    }

    [Fact]
    public void Validate_KustoTool_WithMissingQuery_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = ""
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("KustoTool must have a 'query' specified"));
    }

    [Fact]
    public void Validate_KustoTool_WithWrongSpecType_ShouldReturnError()
    {
        // Arrange - Using base ToolSpecV2 instead of KustoToolSpecV2
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new ToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("KustoTool must use KustoToolSpecV2"));
    }

    [Fact]
    public void Validate_KustoTool_WithAllRequiredFields_ShouldPass()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("kustotool")]
    [InlineData("KUSTOTOOL")]
    [InlineData("KustoTool")]
    public void Validate_KustoTool_CaseInsensitiveType_ShouldWork(string toolType)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = toolType,
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region LinkTool Validation Tests

    [Fact]
    public void Validate_LinkTool_WithMissingTemplate_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new LinkToolSpecV2
            {
                Type = "LinkTool",
                Description = "Test Link tool",
                Template = ""
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("LinkTool must have a 'template' specified"));
    }

    [Fact]
    public void Validate_LinkTool_WithWrongSpecType_ShouldReturnError()
    {
        // Arrange - Using base ToolSpecV2 instead of LinkToolSpecV2
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new ToolSpecV2
            {
                Type = "LinkTool",
                Description = "Test Link tool"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("LinkTool must use LinkToolSpecV2"));
    }

    [Fact]
    public void Validate_LinkTool_WithAllRequiredFields_ShouldPass()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new LinkToolSpecV2
            {
                Type = "LinkTool",
                Description = "Test Link tool",
                Template = "https://example.com/{id}"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("linktool")]
    [InlineData("LINKTOOL")]
    [InlineData("LinkTool")]
    public void Validate_LinkTool_CaseInsensitiveType_ShouldWork(string toolType)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new LinkToolSpecV2
            {
                Type = toolType,
                Description = "Test Link tool",
                Template = "https://example.com/{id}"
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    #endregion

    #region Parameter Validation Tests

    [Fact]
    public void Validate_WithParameterMissingName_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "",
                        Type = "string",
                        Description = "Test parameter"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Parameter must have a 'name'"));
    }

    [Fact]
    public void Validate_WithParameterMissingType_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = "",
                        Description = "Test parameter"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Parameter 'testParam' must have a 'type'"));
    }

    [Fact]
    public void Validate_WithParameterMissingDescription_ShouldReturnError()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = "string",
                        Description = ""
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Parameter 'testParam' must have a 'description'"));
    }

    [Theory]
    [InlineData("invalidType")]
    [InlineData("number")]
    [InlineData("text")]
    public void Validate_WithInvalidParameterType_ShouldReturnError(string paramType)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = paramType,
                        Description = "Test parameter"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains($"Parameter 'testParam' has invalid type '{paramType}'"));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("bool")]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("object")]
    [InlineData("array")]
    public void Validate_WithValidParameterType_ShouldPass(string paramType)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = paramType,
                        Description = "Test parameter"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("STRING")]
    [InlineData("Int")]
    [InlineData("BOOL")]
    public void Validate_WithParameterType_CaseInsensitive_ShouldPass(string paramType)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = paramType,
                        Description = "Test parameter"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("invalidMapTo")]
    [InlineData("params")]
    [InlineData("input")]
    public void Validate_WithInvalidParameterMapTo_ShouldReturnError(string mapTo)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = "string",
                        Description = "Test parameter",
                        MapTo = mapTo
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains($"Parameter 'testParam' has invalid mapTo '{mapTo}'"));
    }

    [Theory]
    [InlineData("args")]
    [InlineData("context")]
    [InlineData("body")]
    public void Validate_WithValidParameterMapTo_ShouldPass(string mapTo)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = "string",
                        Description = "Test parameter",
                        MapTo = mapTo
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("ARGS")]
    [InlineData("Context")]
    [InlineData("BODY")]
    public void Validate_WithParameterMapTo_CaseInsensitive_ShouldPass(string mapTo)
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "testParam",
                        Type = "string",
                        Description = "Test parameter",
                        MapTo = mapTo
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMultipleParameters_ShouldValidateAll()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "param1",
                        Type = "string",
                        Description = "First parameter"
                    },
                    new ToolParameterV2
                    {
                        Name = "param2",
                        Type = "int",
                        Description = "Second parameter"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMultipleParameterErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "test-tool" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "Test Kusto tool",
                Connector = "test-connector",
                Database = "TestDB",
                Query = "TestQuery | take 10",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "",
                        Type = "string",
                        Description = "First parameter"
                    },
                    new ToolParameterV2
                    {
                        Name = "param2",
                        Type = "invalidType",
                        Description = "Second parameter"
                    },
                    new ToolParameterV2
                    {
                        Name = "param3",
                        Type = "string",
                        Description = "",
                        MapTo = "invalid"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Parameter must have a 'name'"));
        Assert.Contains(errors, e => e.Contains("Parameter 'param2' has invalid type"));
        Assert.Contains(errors, e => e.Contains("Parameter 'param3' must have a 'description'"));
        Assert.Contains(errors, e => e.Contains("Parameter 'param3' has invalid mapTo"));
    }

    #endregion

    #region Complete Tool Validation Tests

    [Fact]
    public void Validate_CompleteValidKustoTool_ShouldPass()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "QueryServiceMetrics",
                Owner = "team@example.com"
            },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Connector = "production-telemetry-connector",
                Description = "Query service performance and health metrics from production telemetry systems.",
                Database = "ProductionTelemetry",
                Query = @"let timeRange = ago({timeRange});
let serviceName = ""{serviceName}"";
ServiceMetrics
| where TimeGenerated >= timeRange
| limit 1000",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "serviceName",
                        Type = "string",
                        Required = true,
                        Description = "Name of the service to query",
                        MapTo = "args"
                    },
                    new ToolParameterV2
                    {
                        Name = "timeRange",
                        Type = "string",
                        Required = false,
                        Description = "Time range for the query",
                        MapTo = "args"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_CompleteValidLinkTool_ShouldPass()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "GenerateIncidentLink",
                Owner = "team@example.com"
            },
            Spec = new LinkToolSpecV2
            {
                Type = "LinkTool",
                Description = "Generate a link to view incident details",
                Template = "https://incidents.example.com/view/{incidentId}",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "incidentId",
                        Type = "string",
                        Required = true,
                        Description = "ID of the incident"
                    }
                }
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel { Name = "" },
            Spec = new KustoToolSpecV2
            {
                Type = "KustoTool",
                Description = "",
                Connector = "",
                Database = "",
                Query = ""
            }
        };

        // Act
        var errors = tool.Validate();

        // Assert
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Tool must have a name in metadata"));
        Assert.Contains(errors, e => e.Contains("Tool must have a 'description'"));
        Assert.Contains(errors, e => e.Contains("KustoTool must have a 'connector' specified"));
        Assert.Contains(errors, e => e.Contains("KustoTool must have a 'database' specified"));
        Assert.Contains(errors, e => e.Contains("KustoTool must have a 'query' specified"));
    }

    #endregion
}
