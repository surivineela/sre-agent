// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Converters;
using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Converters;

public class ExtendedToolConverterTests
{
    #region KustoToolV1 to ExtendedToolV2 Tests

    [Fact]
    public void ConvertToV2_WithKustoToolV1_ShouldConvertSuccessfully()
    {
        // Arrange
        var kustoV1 = new KustoToolV1
        {
            Name = "TestKustoTool",
            Type = "KustoTool",
            Connector = "test-connector",
            Description = "Test description",
            Database = "TestDB",
            Query = "TestQuery | limit 10",
            Parameters = new List<ToolParameterV1>
            {
                new() { Name = "param1", Type = "string", Required = true }
            }
        };

        // Act
        var result = ExtendedToolConverter.ConvertToV2(kustoV1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestKustoTool", result.Metadata.Name);
        Assert.IsType<KustoToolSpecV2>(result.Spec);

        var spec = (KustoToolSpecV2)result.Spec;
        Assert.Equal("KustoTool", spec.Type);
        Assert.Equal("test-connector", spec.Connector);
        Assert.Equal("Test description", spec.Description);
        Assert.Equal("TestDB", spec.Database);
        Assert.Equal("TestQuery | limit 10", spec.Query);
        Assert.Single(spec.Parameters!);
        Assert.Equal("param1", spec.Parameters![0].Name);
    }

    [Fact]
    public void ConvertToV2_WithKustoToolV1_WithMetadata_ShouldPreferTopLevelName()
    {
        // Arrange
        var kustoV1 = new KustoToolV1
        {
            Name = "TopLevelName",
            Metadata = new ResourceMetadataModel
            {
                Name = "MetadataName",
                Owner = "test-owner",
                Tags = new List<string> { "tag1", "tag2" }
            },
            Type = "KustoTool"
        };

        // Act
        var result = ExtendedToolConverter.ConvertToV2(kustoV1);

        // Assert
        Assert.Equal("TopLevelName", result.Metadata.Name);  // Top-level name is preferred
        Assert.Equal("test-owner", result.Metadata.Owner);
        Assert.Equal(2, result.Metadata.Tags?.Count);
    }

    [Fact]
    public void ConvertToV2_WithNullKustoToolV1_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ExtendedToolConverter.ConvertToV2((KustoToolV1)null!));
    }

    #endregion

    #region LinkToolV1 to ExtendedToolV2 Tests

    [Fact]
    public void ConvertToV2_WithLinkToolV1_ShouldConvertSuccessfully()
    {
        // Arrange
        var linkV1 = new LinkToolV1
        {
            Name = "TestLinkTool",
            Type = "LinkTool",
            Connector = "link-connector",
            Description = "Link description",
            Template = "https://example.com/{id}",
            Parameters = new List<ToolParameterV1>
            {
                new() { Name = "id", Type = "string", Required = true }
            }
        };

        // Act
        var result = ExtendedToolConverter.ConvertToV2(linkV1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestLinkTool", result.Metadata.Name);
        Assert.IsType<LinkToolSpecV2>(result.Spec);

        var spec = (LinkToolSpecV2)result.Spec;
        Assert.Equal("LinkTool", spec.Type);
        Assert.Equal("link-connector", spec.Connector);
        Assert.Equal("Link description", spec.Description);
        Assert.Equal("https://example.com/{id}", spec.Template);
        Assert.Single(spec.Parameters!);
        Assert.Equal("id", spec.Parameters![0].Name);
    }

    [Fact]
    public void ConvertToV2_WithNullLinkToolV1_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ExtendedToolConverter.ConvertToV2((LinkToolV1)null!));
    }

    #endregion

    #region ExtendedToolListV1 to ExtendedToolV2 List Tests

    [Fact]
    public void ConvertToV2_WithToolList_ShouldConvertAllTools()
    {
        // Arrange
        var toolList = new ExtendedToolListV1
        {
            Spec = new ExtendedToolListSpecV1
            {
                Tools = new List<ExtendedToolItemV1>
                {
                    new()
                    {
                        Name = "KustoTool1",
                        Type = "KustoTool",
                        Connector = "kusto-conn",
                        Description = "Kusto tool",
                        Database = "DB1",
                        Query = "Query1"
                    },
                    new()
                    {
                        Name = "LinkTool1",
                        Type = "LinkTool",
                        Connector = "link-conn",
                        Description = "Link tool",
                        Template = "https://example.com"
                    }
                }
            }
        };

        // Act
        var results = ExtendedToolConverter.ConvertToV2(toolList);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);

        // Verify first tool (KustoTool)
        Assert.Equal("KustoTool1", results[0].Metadata.Name);
        Assert.IsType<KustoToolSpecV2>(results[0].Spec);
        var kustoSpec = (KustoToolSpecV2)results[0].Spec;
        Assert.Equal("kusto-conn", kustoSpec.Connector);
        Assert.Equal("DB1", kustoSpec.Database);

        // Verify second tool (LinkTool)
        Assert.Equal("LinkTool1", results[1].Metadata.Name);
        Assert.IsType<LinkToolSpecV2>(results[1].Spec);
        var linkSpec = (LinkToolSpecV2)results[1].Spec;
        Assert.Equal("link-conn", linkSpec.Connector);
        Assert.Equal("https://example.com", linkSpec.Template);
    }

    [Fact]
    public void ConvertToV2_WithToolListContainingMetadata_ShouldPreserveMetadata()
    {
        // Arrange
        var toolList = new ExtendedToolListV1
        {
            Spec = new ExtendedToolListSpecV1
            {
                Tools = new List<ExtendedToolItemV1>
                {
                    new()
                    {
                        Name = "Tool1",
                        Type = "KustoTool",
                        Metadata = new ResourceMetadataModel
                        {
                            Name = "MetadataToolName",
                            Owner = "team@example.com",
                            Tags = new List<string> { "production", "monitoring" }
                        }
                    }
                }
            }
        };

        // Act
        var results = ExtendedToolConverter.ConvertToV2(toolList);

        // Assert
        Assert.Single(results);
        Assert.Equal("Tool1", results[0].Metadata.Name);  // Top-level name is preferred
        Assert.Equal("team@example.com", results[0].Metadata.Owner);
        Assert.Equal(2, results[0].Metadata.Tags?.Count);
    }

    [Fact]
    public void ConvertToV2_WithEmptyToolList_ShouldReturnEmptyList()
    {
        // Arrange
        var toolList = new ExtendedToolListV1
        {
            Spec = new ExtendedToolListSpecV1
            {
                Tools = new List<ExtendedToolItemV1>()
            }
        };

        // Act
        var results = ExtendedToolConverter.ConvertToV2(toolList);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public void ConvertToV2_WithNullToolList_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ExtendedToolConverter.ConvertToV2((ExtendedToolListV1)null!));
    }

    [Fact]
    public void ConvertToV2_WithToolListWithNullSpec_ShouldReturnEmptyList()
    {
        // Arrange
        var toolList = new ExtendedToolListV1
        {
            Spec = null!
        };

        // Act
        var results = ExtendedToolConverter.ConvertToV2(toolList);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public void ConvertToV2_WithToolListWithParameters_ShouldConvertParameters()
    {
        // Arrange
        var toolList = new ExtendedToolListV1
        {
            Spec = new ExtendedToolListSpecV1
            {
                Tools = new List<ExtendedToolItemV1>
                {
                    new()
                    {
                        Name = "ToolWithParams",
                        Type = "KustoTool",
                        Parameters = new List<ToolParameterV1>
                        {
                            new()
                            {
                                Name = "region",
                                Type = "string",
                                Description = "Azure region",
                                MapTo = "args",
                                Required = true,
                                Target = "dictionary:args:string"
                            },
                            new()
                            {
                                Name = "count",
                                Type = "int",
                                Required = false,
                                Value = 10
                            }
                        }
                    }
                }
            }
        };

        // Act
        var results = ExtendedToolConverter.ConvertToV2(toolList);

        // Assert
        Assert.Single(results);
        var spec = results[0].Spec;
        Assert.NotNull(spec.Parameters);
        Assert.Equal(2, spec.Parameters.Count);

        Assert.Equal("region", spec.Parameters[0].Name);
        Assert.Equal("string", spec.Parameters[0].Type);
        Assert.Equal("Azure region", spec.Parameters[0].Description);
        Assert.True(spec.Parameters[0].Required);

        Assert.Equal("count", spec.Parameters[1].Name);
        Assert.Equal("int", spec.Parameters[1].Type);
        Assert.False(spec.Parameters[1].Required);
        Assert.Equal(10, spec.Parameters[1].Value);
    }

    #endregion
}
