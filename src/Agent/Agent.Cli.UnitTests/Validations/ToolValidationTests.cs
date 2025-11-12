using Agent.Cli.Validations;
using Xunit;

namespace Agent.Cli.UnitTests.Validations;

public class ToolValidationTests
{
    #region Basic Tool Validation Tests

    [Fact]
    public void ValidateTool_WithEmptyName_ShouldReturnFalse()
    {
        // Act
        var result = ToolValidation.ValidateTool("", "TestType", out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Tool name must not be empty"));
    }

    [Fact]
    public void ValidateTool_WithNameContainingWhitespace_ShouldReturnFalse()
    {
        // Act
        var result = ToolValidation.ValidateTool("tool name", "TestType", out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Tool name must not contain whitespace"));
    }

    [Fact]
    public void ValidateTool_WithEmptyType_ShouldReturnFalse()
    {
        // Act
        var result = ToolValidation.ValidateTool("validToolName", "", out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Tool type must not be empty"));
    }

    [Fact]
    public void ValidateTool_WithValidParameters_ShouldReturnTrue()
    {
        // Act
        var result = ToolValidation.ValidateTool("validToolName", "TestType", out var errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
    }

    #endregion

    #region YAML Validation Tests

    [Fact]
    public void ValidateToolYaml_WithInvalidYaml_ShouldReturnFalse()
    {
        // Arrange
        var invalidYaml = "invalid: yaml: content: [unclosed";

        // Act
        var result = ToolValidation.ValidateToolYaml(invalidYaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("YAML parsing error"));
    }

    [Fact]
    public void ValidateToolYaml_WithNullContent_ShouldReturnFalse()
    {
        // Arrange
        var nullYaml = "";

        // Act
        var result = ToolValidation.ValidateToolYaml(nullYaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Invalid YAML format"));
    }

    [Fact]
    public void ValidateToolYaml_WithMissingName_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
type: KustoTool
description: Test tool
connector: test-connector
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'name' is required"));
    }

    [Fact]
    public void ValidateToolYaml_WithMissingType_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
description: Test tool
connector: test-connector
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'type' is required"));
    }

    [Fact]
    public void ValidateToolYaml_WithMissingDescription_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
connector: test-connector
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'description' is required"));
    }

    #endregion

    #region KustoTool Validation Tests

    [Fact]
    public void ValidateToolYaml_KustoTool_WithMissingConnector_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test Kusto tool
database: TestDB
query: TestQuery | take 10
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'connector' is required"));
    }

    [Fact]
    public void ValidateToolYaml_KustoTool_WithMissingDatabase_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test Kusto tool
connector: test-connector
query: TestQuery | take 10
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'database' is required"));
    }

    [Fact]
    public void ValidateToolYaml_KustoTool_WithMissingQuery_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test Kusto tool
connector: test-connector
database: TestDB
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'query' is required"));
    }

    [Fact]
    public void ValidateToolYaml_KustoTool_WithInvalidMode_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test Kusto tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
mode: invalid_mode
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Mode must be either 'query' or 'command'"));
    }

    [Theory]
    [InlineData("query")]
    [InlineData("command")]
    [InlineData("Query")]
    [InlineData("Command")]
    public void ValidateToolYaml_KustoTool_WithValidMode_ShouldPass(string mode)
    {
        // Arrange
        var yaml = $@"
name: test-tool
type: KustoTool
description: Test Kusto tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
mode: {mode}
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.True(result);
        Assert.DoesNotContain(errors, e => e.Contains("Mode must be either"));
    }

    [Fact]
    public void ValidateToolYaml_KustoTool_WithValidYaml_ShouldReturnTrue()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test Kusto tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
mode: query
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.True(result);
    }

    #endregion

    // KustoQuery validation tests removed - tool type ignored for validation purposes

    #region Parameter Validation Tests

    [Fact]
    public void ValidateToolYaml_WithParameterMissingName_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - type: string
    description: Test parameter
    required: true
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'name' is required"));
    }

    [Fact]
    public void ValidateToolYaml_WithParameterMissingType_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    description: Test parameter
    required: true
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'type' is required"));
    }

    [Fact]
    public void ValidateToolYaml_WithParameterMissingDescription_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    type: string
    required: true
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Field 'description' is required"));
    }

    [Theory]
    [InlineData("invalidType")]
    [InlineData("number")]
    [InlineData("text")]
    public void ValidateToolYaml_WithInvalidParameterType_ShouldReturnFalse(string paramType)
    {
        // Arrange
        var yaml = $@"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    type: {paramType}
    description: Test parameter
    required: true
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains($"Parameter type '{paramType}' is not valid"));
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("bool")]
    [InlineData("float")]
    [InlineData("double")]
    public void ValidateToolYaml_WithValidParameterType_ShouldPass(string paramType)
    {
        // Arrange
        var yaml = $@"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    type: {paramType}
    description: Test parameter
    required: true
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.True(result);
        Assert.DoesNotContain(errors, e => e.Contains("Parameter type"));
    }

    [Theory]
    [InlineData("invalidMapTo")]
    [InlineData("params")]
    [InlineData("input")]
    public void ValidateToolYaml_WithInvalidParameterMapTo_ShouldReturnFalse(string mapTo)
    {
        // Arrange
        var yaml = $@"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    type: string
    description: Test parameter
    required: true
    map_to: {mapTo}
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains($"Parameter map_to '{mapTo}' is not valid"));
    }

    [Theory]
    [InlineData("args")]
    [InlineData("context")]
    [InlineData("body")]
    public void ValidateToolYaml_WithValidParameterMapTo_ShouldPass(string mapTo)
    {
        // Arrange
        var yaml = $@"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    type: string
    description: Test parameter
    required: true
    map_to: {mapTo}
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.True(result);
        Assert.DoesNotContain(errors, e => e.Contains("Parameter map_to"));
    }

    [Fact]
    public void ValidateToolYaml_WithInvalidRequiredValue_ShouldReturnFalse()
    {
        // Arrange
        var yaml = @"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    type: string
    description: Test parameter
    required: ""not a boolean""
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.False(result);
        Assert.Contains(errors, e => e.Contains("Parameter 'required' field must be a boolean value"));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void ValidateToolYaml_WithValidRequiredValue_ShouldPass(string required)
    {
        // Arrange
        var yaml = $@"
name: test-tool
type: KustoTool
description: Test tool
connector: test-connector
database: TestDB
query: TestQuery | take 10
parameters:
  - name: testParam
    type: string
    description: Test parameter
    required: {required}
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.True(result);
        Assert.DoesNotContain(errors, e => e.Contains("Parameter 'required' field"));
    }

    #endregion

    #region Complete Valid Tool Tests

    [Fact]
    public void ValidateToolYaml_WithCompleteValidKustoTool_ShouldReturnTrue()
    {
        // Arrange
        var yaml = @"
name: QueryServiceMetrics
type: KustoTool
connector: production-telemetry-connector
description: |
  Query service performance and health metrics from production telemetry systems.
mode: query
function: QueryServiceMetrics
database: ProductionTelemetry
cluster_hint: prod-telemetry-cluster
query: |
  let timeRange = ago({timeRange});
  let serviceName = ""{serviceName}"";
  ServiceMetrics
  | where TimeGenerated >= timeRange
  | limit 1000
parameters:
  - name: serviceName
    type: string
    required: true
    description: Name of the service to query
    map_to: args
    target: dictionary:args:string
    default: ""all""
  - name: timeRange
    type: string
    required: false
    description: Time range for the query
    map_to: args
    target: dictionary:args:string
    default: ""4h""
";

        // Act
        var result = ToolValidation.ValidateToolYaml(yaml, out var errors);

        // Assert
        Assert.True(result);
        Assert.Empty(errors);
    }

    // ValidateToolYaml_WithCompleteValidKustoQuery_ShouldReturnTrue test removed - KustoQuery tool type ignored for validation purposes

    #endregion
}
