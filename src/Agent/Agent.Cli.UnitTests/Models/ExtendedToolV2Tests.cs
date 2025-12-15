// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class ExtendedToolV2Tests
{
    #region ParseYaml Tests - KustoTool

    [Fact]
    public void ParseYaml_WithKustoToolMinimal_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: test-kusto-tool
spec:
  type: KustoTool
  description: A test Kusto tool
";

        // Act
        var tool = ExtendedToolV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("azuresre.ai/v2", tool.ApiVersion);
        Assert.Equal("ExtendedAgentTool", tool.Kind);
        Assert.NotNull(tool.Metadata);
        Assert.Equal("test-kusto-tool", tool.Metadata.Name);
        Assert.NotNull(tool.Spec);
        Assert.IsType<KustoToolSpecV2>(tool.Spec);
        Assert.Equal("KustoTool", tool.Spec.Type);
        Assert.Equal("A test Kusto tool", tool.Spec.Description);
    }

    [Fact]
    public void ParseYaml_WithKustoToolAllProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: test-kusto-tool
  owner: test-owner
  tags:
    - production
    - kusto
spec:
  type: KustoTool
  connector: kusto-connector
  toolMode: Auto
  description: |
    A comprehensive Kusto tool
    with multiple features
  database: TestDatabase
  query: |
    TestTable
    | where timestamp > ago(1h)
    | summarize count() by bin(timestamp, 5m)
  parameters:
    - name: timeRange
      type: string
      description: Time range for query
      required: true
";

        // Act
        var tool = ExtendedToolV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("azuresre.ai/v2", tool.ApiVersion);
        Assert.NotNull(tool.Metadata);
        Assert.Equal("test-kusto-tool", tool.Metadata.Name);
        Assert.Equal("test-owner", tool.Metadata.Owner);
        Assert.NotNull(tool.Metadata.Tags);
        Assert.Equal(2, tool.Metadata.Tags.Count);

        Assert.IsType<KustoToolSpecV2>(tool.Spec);
        var kustoSpec = (KustoToolSpecV2)tool.Spec;
        Assert.Equal("KustoTool", kustoSpec.Type);
        Assert.Equal("kusto-connector", kustoSpec.Connector);
        Assert.Contains("comprehensive Kusto tool", kustoSpec.Description);
        Assert.Equal("TestDatabase", kustoSpec.Database);
        Assert.Contains("TestTable", kustoSpec.Query);
        Assert.Contains("ago(1h)", kustoSpec.Query);
        Assert.NotNull(kustoSpec.Parameters);
        Assert.Single(kustoSpec.Parameters);
        Assert.Equal("timeRange", kustoSpec.Parameters[0].Name);
    }

    #endregion

    #region ParseYaml Tests - LinkTool

    [Fact]
    public void ParseYaml_WithLinkToolMinimal_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: test-link-tool
spec:
  type: LinkTool
  description: A test Link tool
  template: https://example.com/{id}
";

        // Act
        var tool = ExtendedToolV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("azuresre.ai/v2", tool.ApiVersion);
        Assert.Equal("ExtendedAgentTool", tool.Kind);
        Assert.NotNull(tool.Metadata);
        Assert.Equal("test-link-tool", tool.Metadata.Name);
        Assert.NotNull(tool.Spec);
        Assert.IsType<LinkToolSpecV2>(tool.Spec);
        Assert.Equal("LinkTool", tool.Spec.Type);
        Assert.Equal("A test Link tool", tool.Spec.Description);

        var linkSpec = (LinkToolSpecV2)tool.Spec;
        Assert.Equal("https://example.com/{id}", linkSpec.Template);
    }

    [Fact]
    public void ParseYaml_WithLinkToolAllProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: test-link-tool
  owner: test-owner
spec:
  type: LinkTool
  connector: link-connector
  toolMode: Auto
  description: A comprehensive Link tool
  template: https://portal.azure.com/#resource/{resourceId}
  parameters:
    - name: resourceId
      type: string
      description: Azure resource ID
      required: true
";

        // Act
        var tool = ExtendedToolV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.IsType<LinkToolSpecV2>(tool.Spec);
        var linkSpec = (LinkToolSpecV2)tool.Spec;
        Assert.Equal("LinkTool", linkSpec.Type);
        Assert.Equal("link-connector", linkSpec.Connector);
        Assert.Equal("A comprehensive Link tool", linkSpec.Description);
        Assert.Equal("https://portal.azure.com/#resource/{resourceId}", linkSpec.Template);
        Assert.NotNull(linkSpec.Parameters);
        Assert.Single(linkSpec.Parameters);
        Assert.Equal("resourceId", linkSpec.Parameters[0].Name);
        Assert.Equal("Azure resource ID", linkSpec.Parameters[0].Description);
        Assert.True(linkSpec.Parameters[0].Required);
    }

    #endregion

    #region ToYaml Tests - KustoTool

    [Fact]
    public void ToYaml_WithKustoToolMinimal_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-kusto-tool"
            },
            Spec = new KustoToolSpecV2
            {
                Description = "A test Kusto tool",
                Database = "TestDB",
                Query = "TestTable | take 10"
            }
        };

        // Act
        var yaml = tool.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: azuresre.ai/v2", yaml);
        Assert.Contains("kind: ExtendedAgentTool", yaml);
        Assert.Contains("name: test-kusto-tool", yaml);
        Assert.Contains("type: KustoTool", yaml);
        Assert.Contains("A test Kusto tool", yaml);
        Assert.Contains("database: TestDB", yaml);
        Assert.Contains("TestTable", yaml);
    }

    [Fact]
    public void ToYaml_WithKustoToolAllProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-kusto-tool",
                Owner = "test-owner",
                Tags = new List<string> { "production" }
            },
            Spec = new KustoToolSpecV2
            {
                Connector = "kusto-connector",
                Description = "A comprehensive Kusto tool\nwith multiple features",
                Database = "TestDatabase",
                Query = "TestTable\n| where timestamp > ago(1h)",
                Parameters = new List<ToolParameterV2>
                {
                    new ToolParameterV2
                    {
                        Name = "timeRange",
                        Type = "string",
                        Description = "Time range for query",
                        Required = true
                    }
                }
            }
        };

        // Act
        var yaml = tool.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: azuresre.ai/v2", yaml);
        Assert.Contains("name: test-kusto-tool", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("- production", yaml);
        Assert.Contains("type: KustoTool", yaml);
        Assert.Contains("connector: kusto-connector", yaml);
        Assert.Contains("database: TestDatabase", yaml);
        Assert.Contains("TestTable", yaml);
        Assert.Contains("name: timeRange", yaml);
        Assert.Contains("required: true", yaml);
    }

    #endregion

    #region ToYaml Tests - LinkTool

    [Fact]
    public void ToYaml_WithLinkToolMinimal_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-link-tool"
            },
            Spec = new LinkToolSpecV2
            {
                Description = "A test Link tool",
                Template = "https://example.com/{id}"
            }
        };

        // Act
        var yaml = tool.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("api_version: azuresre.ai/v2", yaml);
        Assert.Contains("kind: ExtendedAgentTool", yaml);
        Assert.Contains("name: test-link-tool", yaml);
        Assert.Contains("type: LinkTool", yaml);
        Assert.Contains("A test Link tool", yaml);
        Assert.Contains("template: https://example.com/{id}", yaml);
    }

    [Fact]
    public void ToYaml_RoundTrip_KustoTool_ShouldPreserveData()
    {
        // Arrange
        var originalTool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-kusto-tool",
                Owner = "test-owner"
            },
            Spec = new KustoToolSpecV2
            {
                Connector = "kusto-connector",
                Description = "Test description",
                Database = "TestDB",
                Query = "TestTable | take 10"
            }
        };

        // Act
        var yaml = originalTool.ToYaml();
        var deserializedTool = ExtendedToolV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Equal(originalTool.ApiVersion, deserializedTool.ApiVersion);
        Assert.Equal(originalTool.Kind, deserializedTool.Kind);
        Assert.Equal(originalTool.Metadata.Name, deserializedTool.Metadata.Name);
        Assert.Equal(originalTool.Metadata.Owner, deserializedTool.Metadata.Owner);
        Assert.IsType<KustoToolSpecV2>(deserializedTool.Spec);

        var originalKustoSpec = (KustoToolSpecV2)originalTool.Spec;
        var deserializedKustoSpec = (KustoToolSpecV2)deserializedTool.Spec;
        Assert.Equal(originalKustoSpec.Connector, deserializedKustoSpec.Connector);
        Assert.Equal(originalKustoSpec.Description, deserializedKustoSpec.Description);
        Assert.Equal(originalKustoSpec.Database, deserializedKustoSpec.Database);
        Assert.Equal(originalKustoSpec.Query, deserializedKustoSpec.Query);
    }

    [Fact]
    public void ToYaml_RoundTrip_LinkTool_ShouldPreserveData()
    {
        // Arrange
        var originalTool = new ExtendedToolV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = "test-link-tool"
            },
            Spec = new LinkToolSpecV2
            {
                Description = "Test link tool",
                Template = "https://example.com/{id}"
            }
        };

        // Act
        var yaml = originalTool.ToYaml();
        var deserializedTool = ExtendedToolV2.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Equal(originalTool.Metadata.Name, deserializedTool.Metadata.Name);
        Assert.IsType<LinkToolSpecV2>(deserializedTool.Spec);

        var originalLinkSpec = (LinkToolSpecV2)originalTool.Spec;
        var deserializedLinkSpec = (LinkToolSpecV2)deserializedTool.Spec;
        Assert.Equal(originalLinkSpec.Description, deserializedLinkSpec.Description);
        Assert.Equal(originalLinkSpec.Template, deserializedLinkSpec.Template);
    }

    #endregion
}
