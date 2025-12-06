// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Xunit;

namespace Agent.Cli.UnitTests.Models;

public class LinkToolV1Tests
{
    #region ParseYaml Tests

    [Fact]
    public void ParseYaml_WithValidMinimalYaml_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
name: test-link-tool
type: LinkTool
description: A test Link tool
template: https://example.com/{id}
";

        // Act
        var tool = LinkToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("test-link-tool", tool.Name);
        Assert.Equal("LinkTool", tool.Type);
        Assert.Equal("A test Link tool", tool.Description);
        Assert.Equal("https://example.com/{id}", tool.Template);
    }

    [Fact]
    public void ParseYaml_WithAllProperties_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
name: comprehensive-link-tool
metadata:
  name: link-metadata
  owner: test-owner
  tags:
    - production
    - portal
type: LinkTool
connector: link-connector
description: |
  A comprehensive Link tool
  for Azure Portal navigation
template: https://portal.azure.com/#resource/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}
parameters:
  - name: subscriptionId
    type: string
    description: Azure subscription ID
    required: true
  - name: resourceGroup
    type: string
    description: Resource group name
    required: true
  - name: provider
    type: string
    description: Resource provider namespace
    required: true
";

        // Act
        var tool = LinkToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("comprehensive-link-tool", tool.Name);
        Assert.NotNull(tool.Metadata);
        Assert.Equal("link-metadata", tool.Metadata.Name);
        Assert.Equal("test-owner", tool.Metadata.Owner);
        Assert.NotNull(tool.Metadata.Tags);
        Assert.Equal(2, tool.Metadata.Tags.Count);
        Assert.Contains("production", tool.Metadata.Tags);
        Assert.Contains("portal", tool.Metadata.Tags);

        Assert.Equal("LinkTool", tool.Type);
        Assert.Equal("link-connector", tool.Connector);
        Assert.Contains("comprehensive Link tool", tool.Description);
        Assert.Contains("https://portal.azure.com", tool.Template);
        Assert.Contains("{subscriptionId}", tool.Template);

        Assert.NotNull(tool.Parameters);
        Assert.Equal(3, tool.Parameters.Count);
        Assert.Equal("subscriptionId", tool.Parameters[0].Name);
        Assert.Equal("string", tool.Parameters[0].Type);
        Assert.Equal("Azure subscription ID", tool.Parameters[0].Description);
        Assert.True(tool.Parameters[0].Required);
        Assert.Equal("resourceGroup", tool.Parameters[1].Name);
        Assert.Equal("provider", tool.Parameters[2].Name);
    }

    [Fact]
    public void ParseYaml_WithOptionalFieldsOmitted_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yaml = @"
name: simple-link-tool
type: LinkTool
description: Simple link tool
template: https://example.com
";

        // Act
        var tool = LinkToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(tool);
        Assert.Equal("simple-link-tool", tool.Name);
        Assert.Equal("LinkTool", tool.Type);
        Assert.Equal("Simple link tool", tool.Description);
        Assert.Equal("https://example.com", tool.Template);
        Assert.Null(tool.Connector);
        Assert.Null(tool.Parameters);
        Assert.Null(tool.Metadata);
    }

    #endregion

    #region ToYaml Tests

    [Fact]
    public void ToYaml_WithMinimalProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new LinkToolV1
        {
            Name = "test-link-tool",
            Type = "LinkTool",
            Description = "A test Link tool",
            Template = "https://example.com/{id}"
        };

        // Act
        var yaml = tool.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("name: test-link-tool", yaml);
        Assert.Contains("type: LinkTool", yaml);
        Assert.Contains("A test Link tool", yaml);
        Assert.Contains("template: https://example.com/{id}", yaml);
    }

    [Fact]
    public void ToYaml_WithAllProperties_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new LinkToolV1
        {
            Name = "comprehensive-link-tool",
            Metadata = new ResourceMetadataModel
            {
                Name = "link-metadata",
                Owner = "test-owner",
                Tags = new List<string> { "production", "portal" }
            },
            Type = "LinkTool",
            Connector = "link-connector",
            Description = "A comprehensive Link tool\nfor Azure Portal navigation",
            Template = "https://portal.azure.com/#resource/{resourceId}",
            Parameters = new List<ToolParameterV1>
            {
                new ToolParameterV1
                {
                    Name = "resourceId",
                    Type = "string",
                    Description = "Azure resource ID",
                    Required = true
                },
                new ToolParameterV1
                {
                    Name = "view",
                    Type = "string",
                    Description = "Portal view to navigate to",
                    Required = false
                }
            }
        };

        // Act
        var yaml = tool.ToYaml();

        // Assert
        Assert.NotNull(yaml);
        Assert.Contains("name: comprehensive-link-tool", yaml);
        Assert.Contains("name: link-metadata", yaml);
        Assert.Contains("owner: test-owner", yaml);
        Assert.Contains("- production", yaml);
        Assert.Contains("- portal", yaml);
        Assert.Contains("type: LinkTool", yaml);
        Assert.Contains("connector: link-connector", yaml);
        Assert.Contains("comprehensive Link tool", yaml);
        Assert.Contains("template: https://portal.azure.com/#resource/{resourceId}", yaml);
        Assert.Contains("- name: resourceId", yaml);
        Assert.Contains("type: string", yaml);
        Assert.Contains("required: true", yaml);
        Assert.Contains("- name: view", yaml);
    }

    [Fact]
    public void ToYaml_RoundTrip_ShouldPreserveData()
    {
        // Arrange
        var originalTool = new LinkToolV1
        {
            Name = "test-link-tool",
            Metadata = new ResourceMetadataModel
            {
                Name = "link-metadata",
                Owner = "test-owner"
            },
            Type = "LinkTool",
            Connector = "link-connector",
            Description = "Test description",
            Template = "https://example.com/{id}"
        };

        // Act
        var yaml = originalTool.ToYaml();
        var deserializedTool = LinkToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Equal(originalTool.Name, deserializedTool.Name);
        Assert.Equal(originalTool.Metadata?.Name, deserializedTool.Metadata?.Name);
        Assert.Equal(originalTool.Metadata?.Owner, deserializedTool.Metadata?.Owner);
        Assert.Equal(originalTool.Type, deserializedTool.Type);
        Assert.Equal(originalTool.Connector, deserializedTool.Connector);
        Assert.Equal(originalTool.Description, deserializedTool.Description);
        Assert.Equal(originalTool.Template, deserializedTool.Template);
    }

    [Fact]
    public void ToYaml_WithComplexTemplate_ShouldPreserveUrl()
    {
        // Arrange
        var tool = new LinkToolV1
        {
            Name = "complex-template-tool",
            Type = "LinkTool",
            Description = "Tool with complex URL template",
            Template = "https://portal.azure.com/#blade/Microsoft_Azure_Monitoring/AzureMonitoringBrowseBlade/resourceId/%2Fsubscriptions%2F{subscriptionId}%2FresourceGroups%2F{resourceGroup}%2Fproviders%2F{provider}%2F{resourceType}%2F{resourceName}"
        };

        // Act
        var yaml = tool.ToYaml();
        var deserializedTool = LinkToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Contains("portal.azure.com", deserializedTool.Template);
        Assert.Contains("{subscriptionId}", deserializedTool.Template);
        Assert.Contains("{resourceGroup}", deserializedTool.Template);
        Assert.Contains("%2F", deserializedTool.Template);
    }

    [Fact]
    public void ToYaml_WithParametersContainingSpecialCharacters_ShouldSerializeCorrectly()
    {
        // Arrange
        var tool = new LinkToolV1
        {
            Name = "special-chars-tool",
            Type = "LinkTool",
            Description = "Tool with special characters in parameters",
            Template = "https://example.com/api?query={queryParam}&filter={filterParam}",
            Parameters = new List<ToolParameterV1>
            {
                new ToolParameterV1
                {
                    Name = "queryParam",
                    Type = "string",
                    Description = "Query parameter with & and = characters",
                    Required = true
                }
            }
        };

        // Act
        var yaml = tool.ToYaml();
        var deserializedTool = LinkToolV1.ParseYaml(yaml);

        // Assert
        Assert.NotNull(deserializedTool);
        Assert.Contains("query={queryParam}", deserializedTool.Template);
        Assert.Contains("filter={filterParam}", deserializedTool.Template);
        Assert.NotNull(deserializedTool.Parameters);
        Assert.Single(deserializedTool.Parameters);
        Assert.Equal("queryParam", deserializedTool.Parameters[0].Name);
    }

    #endregion
}
