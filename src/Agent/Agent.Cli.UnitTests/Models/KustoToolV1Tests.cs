// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class KustoToolV1Tests
{
    #region ParseYaml Tests

    [Fact]
    public void ParseYaml_WithValidMinimalYaml_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
name: test-kusto-tool
type: KustoTool
description: A test Kusto tool
database: TestDatabase
query: |
  TestTable
  | take 10
";

        // Act
        var tool = KustoToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("test-kusto-tool", tool.Name);
        Assert.Equal("KustoTool", tool.Type);
        Assert.Equal("A test Kusto tool", tool.Description);
        Assert.Equal("TestDatabase", tool.Database);
        Assert.Contains("TestTable", tool.Query);
        Assert.Contains("take 10", tool.Query);
    }

    [Fact]
    public void ParseYaml_WithAllProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
name: comprehensive-kusto-tool
metadata:
  name: kusto-metadata
  owner: test-owner
  tags:
    - production
    - kusto
type: KustoTool
connector: kusto-connector
description: |
  A comprehensive Kusto tool
  with multiple features
database: ProductionDatabase
query: |
  TestTable
  | where timestamp > ago(1h)
  | summarize count() by bin(timestamp, 5m)
  | order by timestamp desc
parameters:
  - name: timeRange
    type: string
    description: Time range for the query
    required: true
  - name: aggregation
    type: string
    description: Aggregation method
    required: false
";

        // Act
        var tool = KustoToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("comprehensive-kusto-tool", tool.Name);
        Assert.NotNull(tool.Metadata);
        Assert.Equal("kusto-metadata", tool.Metadata.Name);
        Assert.Equal("test-owner", tool.Metadata.Owner);
        Assert.NotNull(tool.Metadata.Tags);
        Assert.Equal(2, tool.Metadata.Tags.Count);
        Assert.Contains("production", tool.Metadata.Tags);
        Assert.Contains("kusto", tool.Metadata.Tags);

        Assert.Equal("KustoTool", tool.Type);
        Assert.Equal("kusto-connector", tool.Connector);
        Assert.Contains("comprehensive Kusto tool", tool.Description);
        Assert.Equal("ProductionDatabase", tool.Database);
        Assert.Contains("TestTable", tool.Query);
        Assert.Contains("ago(1h)", tool.Query);
        Assert.Contains("summarize count()", tool.Query);

        Assert.NotNull(tool.Parameters);
        Assert.Equal(2, tool.Parameters.Count);
        Assert.Equal("timeRange", tool.Parameters[0].Name);
        Assert.Equal("string", tool.Parameters[0].Type);
        Assert.Equal("Time range for the query", tool.Parameters[0].Description);
        Assert.True(tool.Parameters[0].Required);
        Assert.Equal("aggregation", tool.Parameters[1].Name);
        Assert.False(tool.Parameters[1].Required);
    }

    [Fact]
    public void ParseYaml_WithOptionalFieldsOmitted_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
name: simple-kusto-tool
type: KustoTool
description: Simple tool
";

        // Act
        var tool = KustoToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("simple-kusto-tool", tool.Name);
        Assert.Equal("KustoTool", tool.Type);
        Assert.Equal("Simple tool", tool.Description);
        Assert.Null(tool.Database);
        Assert.Null(tool.Query);
        Assert.Null(tool.Connector);
        Assert.Null(tool.Parameters);
    }

    #endregion

    #region ToYaml Tests

    [Fact]
    public void ToYaml_WithMinimalProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new KustoToolV1
        {
            Name = "test-kusto-tool",
            Type = "KustoTool",
            Description = "A test Kusto tool",
            Database = "TestDatabase",
            Query = "TestTable | take 10"
        };

        // Act
        var yaml = tool.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("name: test-kusto-tool", yaml);
        Assert.Contains("type: KustoTool", yaml);
        Assert.Contains("A test Kusto tool", yaml);
        Assert.Contains("database: TestDatabase", yaml);
        Assert.Contains("TestTable", yaml);
    }

    [Fact]
    public void ToYaml_WithAllProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new KustoToolV1
        {
            Name = "comprehensive-kusto-tool",
            Metadata = new ResourceMetadataModel
            {
                Name = "kusto-metadata",
                Owner = "test-owner",
                Tags = new List<string> { "production", "kusto" }
            },
            Type = "KustoTool",
            Connector = "kusto-connector",
            Description = "A comprehensive Kusto tool\nwith multiple features",
            Database = "ProductionDatabase",
            Query = "TestTable\n| where timestamp > ago(1h)\n| summarize count() by bin(timestamp, 5m)",
            Parameters = new List<ToolParameterV1>
            {
                new ToolParameterV1
                {
                    Name = "timeRange",
                    Type = "string",
                    Description = "Time range for the query",
                    Required = true
                },
                new ToolParameterV1
                {
                    Name = "aggregation",
                    Type = "string",
                    Description = "Aggregation method",
                    Required = false
                }
            }
        };

        // Act
        var yaml = tool.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("name: comprehensive-kusto-tool", yaml);
        Assert.Contains("name: kusto-metadata", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("- production", yaml);
        Assert.Contains("- kusto", yaml);
        Assert.Contains("type: KustoTool", yaml);
        Assert.Contains("connector: kusto-connector", yaml);
        Assert.Contains("comprehensive Kusto tool", yaml);
        Assert.Contains("database: ProductionDatabase", yaml);
        Assert.Contains("TestTable", yaml);
        Assert.Contains("ago(1h)", yaml);
        Assert.Contains("- name: timeRange", yaml);
        Assert.Contains("type: string", yaml);
        Assert.Contains("required: true", yaml);
        Assert.Contains("- name: aggregation", yaml);
    }

    [Fact]
    public void ToYaml_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalTool = new KustoToolV1
        {
            Name = "test-kusto-tool",
            Metadata = new ResourceMetadataModel
            {
                Name = "kusto-metadata",
                Owner = "test-owner"
            },
            Type = "KustoTool",
            Connector = "kusto-connector",
            Description = "Test description",
            Database = "TestDB",
            Query = "TestTable | take 10"
        };

        // Act
        var yaml = originalTool.ToYaml();
        var deserializedTool = KustoToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Equal(originalTool.Name, deserializedTool.Name);
        Assert.Equal(originalTool.Metadata?.Name, deserializedTool.Metadata?.Name);
        Assert.Equal(originalTool.Metadata?.Owner, deserializedTool.Metadata?.Owner);
        Assert.Equal(originalTool.Type, deserializedTool.Type);
        Assert.Equal(originalTool.Connector, deserializedTool.Connector);
        Assert.Equal(originalTool.Description, deserializedTool.Description);
        Assert.Equal(originalTool.Database, deserializedTool.Database);
        Assert.Equal(originalTool.Query, deserializedTool.Query);
    }

    [Fact]
    public void ToYaml_WithComplexQuery_ShouldPreserveFormatting()
    {
        // Arrange
        var tool = new KustoToolV1
        {
            Name = "complex-query-tool",
            Type = "KustoTool",
            Description = "Tool with complex query",
            Database = "TestDB",
            Query = "let startTime = ago(24h);\nlet endTime = now();\nTestTable\n| where timestamp between (startTime .. endTime)\n| summarize count()"
        };

        // Act
        var yaml = tool.ToYaml();
        var deserializedTool = KustoToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Contains("let startTime", deserializedTool.Query);
        Assert.Contains("let endTime", deserializedTool.Query);
        Assert.Contains("TestTable", deserializedTool.Query);
    }

    #endregion
}
