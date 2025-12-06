// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class ExtendedToolListV1Tests
{
    #region ParseYaml Tests

    [Fact]
    public void ParseYaml_WithValidMinimalYaml_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
metadata:
  name: test-tools
spec:
  tools:
    - name: TestTool
      type: KustoTool
      description: A test Kusto tool
";

        // Act
        var toolList = ExtendedToolListV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(toolList);
        Assert.NotNull(toolList.Metadata);
        Assert.Equal("test-tools", toolList.Metadata.Name);
        Assert.NotNull(toolList.Spec);
        Assert.NotNull(toolList.Spec.Tools);
        Assert.Single(toolList.Spec.Tools);
        Assert.Equal("TestTool", toolList.Spec.Tools[0].Name);
        Assert.Equal("KustoTool", toolList.Spec.Tools[0].Type);
        Assert.Equal("A test Kusto tool", toolList.Spec.Tools[0].Description);
    }

    [Fact]
    public void ParseYaml_WithMultipleTools_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
metadata:
  name: test-tools
  owner: test-owner
  tags:
    - production
    - kusto
spec:
  tools:
    - name: KustoTool1
      type: KustoTool
      connector: kusto-connector
      description: First Kusto tool
      database: TestDB
      query: |
        TestTable
        | take 10
    - name: LinkTool1
      type: LinkTool
      connector: link-connector
      description: First Link tool
      template: https://example.com/{id}
";

        // Act
        var toolList = ExtendedToolListV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(toolList);
        Assert.NotNull(toolList.Metadata);
        Assert.Equal("test-tools", toolList.Metadata.Name);
        Assert.Equal("test-owner", toolList.Metadata.Owner);
        Assert.NotNull(toolList.Metadata.Tags);
        Assert.Equal(2, toolList.Metadata.Tags.Count);

        Assert.NotNull(toolList.Spec);
        Assert.NotNull(toolList.Spec.Tools);
        Assert.Equal(2, toolList.Spec.Tools.Count);

        // Verify first tool (Kusto)
        var kustoTool = toolList.Spec.Tools[0];
        Assert.Equal("KustoTool1", kustoTool.Name);
        Assert.Equal("KustoTool", kustoTool.Type);
        Assert.Equal("kusto-connector", kustoTool.Connector);
        Assert.Equal("First Kusto tool", kustoTool.Description);
        Assert.Equal("TestDB", kustoTool.Database);
        Assert.Contains("TestTable", kustoTool.Query);

        // Verify second tool (Link)
        var linkTool = toolList.Spec.Tools[1];
        Assert.Equal("LinkTool1", linkTool.Name);
        Assert.Equal("LinkTool", linkTool.Type);
        Assert.Equal("link-connector", linkTool.Connector);
        Assert.Equal("First Link tool", linkTool.Description);
        Assert.Equal("https://example.com/{id}", linkTool.Template);
    }

    [Fact]
    public void ParseYaml_WithToolParameters_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
metadata:
  name: test-tools
spec:
  tools:
    - name: ParameterizedTool
      type: KustoTool
      description: Tool with parameters
      parameters:
        - name: param1
          type: string
          description: First parameter
          required: true
        - name: param2
          type: number
          required: false
";

        // Act
        var toolList = ExtendedToolListV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(toolList);
        Assert.NotNull(toolList.Spec.Tools);
        Assert.Single(toolList.Spec.Tools);

        var tool = toolList.Spec.Tools[0];
        Assert.NotNull(tool.Parameters);
        Assert.Equal(2, tool.Parameters.Count);

        Assert.Equal("param1", tool.Parameters[0].Name);
        Assert.Equal("string", tool.Parameters[0].Type);
        Assert.Equal("First parameter", tool.Parameters[0].Description);
        Assert.True(tool.Parameters[0].Required);

        Assert.Equal("param2", tool.Parameters[1].Name);
        Assert.Equal("number", tool.Parameters[1].Type);
        Assert.False(tool.Parameters[1].Required);
    }

    #endregion

    #region ToYaml Tests

    [Fact]
    public void ToYaml_WithMinimalProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var toolList = new ExtendedToolListV1
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-tools"
            },
            Spec = new ExtendedToolListSpecV1
            {
                Tools = new List<ExtendedToolItemV1>
                {
                    new ExtendedToolItemV1
                    {
                        Name = "TestTool",
                        Type = "KustoTool",
                        Description = "A test tool"
                    }
                }
            }
        };

        // Act
        var yaml = toolList.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("name: test-tools", yaml);
        Assert.Contains("- name: TestTool", yaml);
        Assert.Contains("type: KustoTool", yaml);
        Assert.Contains("A test tool", yaml);
    }

    [Fact]
    public void ToYaml_WithMultipleTools_ShouldSerializeCorrectly()
    {
        // Arrange
        var toolList = new ExtendedToolListV1
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-tools",
                Owner = "test-owner",
                Tags = new List<string> { "production", "kusto" }
            },
            Spec = new ExtendedToolListSpecV1
            {
                Tools = new List<ExtendedToolItemV1>
                {
                    new ExtendedToolItemV1
                    {
                        Name = "KustoTool1",
                        Type = "KustoTool",
                        Connector = "kusto-connector",
                        Description = "First Kusto tool",
                        Database = "TestDB",
                        Query = "TestTable\n| take 10"
                    },
                    new ExtendedToolItemV1
                    {
                        Name = "LinkTool1",
                        Type = "LinkTool",
                        Connector = "link-connector",
                        Description = "First Link tool",
                        Template = "https://example.com/{id}"
                    }
                }
            }
        };

        // Act
        var yaml = toolList.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("name: test-tools", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("- production", yaml);
        Assert.Contains("- kusto", yaml);
        Assert.Contains("- name: KustoTool1", yaml);
        Assert.Contains("type: KustoTool", yaml);
        Assert.Contains("database: TestDB", yaml);
        Assert.Contains("- name: LinkTool1", yaml);
        Assert.Contains("type: LinkTool", yaml);
        Assert.Contains("template: https://example.com/{id}", yaml);
    }

    [Fact]
    public void ToYaml_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalToolList = new ExtendedToolListV1
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-tools",
                Owner = "test-owner"
            },
            Spec = new ExtendedToolListSpecV1
            {
                Tools = new List<ExtendedToolItemV1>
                {
                    new ExtendedToolItemV1
                    {
                        Name = "TestTool",
                        Type = "KustoTool",
                        Description = "Test description",
                        Database = "TestDB",
                        Query = "TestQuery"
                    }
                }
            }
        };

        // Act
        var yaml = originalToolList.ToYaml();
        var deserializedToolList = ExtendedToolListV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedToolList);
        Assert.Equal(originalToolList.Metadata.Name, deserializedToolList.Metadata?.Name);
        Assert.Equal(originalToolList.Metadata.Owner, deserializedToolList.Metadata?.Owner);
        Assert.Equal(originalToolList.Spec.Tools.Count, deserializedToolList.Spec.Tools.Count);
        Assert.Equal(originalToolList.Spec.Tools[0].Name, deserializedToolList.Spec.Tools[0].Name);
        Assert.Equal(originalToolList.Spec.Tools[0].Type, deserializedToolList.Spec.Tools[0].Type);
        Assert.Equal(originalToolList.Spec.Tools[0].Database, deserializedToolList.Spec.Tools[0].Database);
    }

    #endregion
}
