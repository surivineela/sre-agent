// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DataModels;
using Agent.Data.DataModels.Legacy;
using Agent.Data.Json;
using Agent.Framework;
using Shouldly;
using Xunit;

namespace Agent.Tests.Unit.Json;

public class LegacyDocumentModelConverterTests
{
    private readonly JsonSerializerOptions _options;

    public LegacyDocumentModelConverterTests()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        _options.Converters.Add(new LegacyDocumentModelConverter<AgentDocumentModel, AgentDocumentModelLegacy>());
        _options.Converters.Add(new LegacyDocumentModelConverter<ToolDocumentModel, ToolDocumentModelLegacy>());
        _options.Converters.Add(new LegacyDocumentModelConverter<ConnectorDocumentModel, ConnectorDocumentModelLegacy>());
        _options.Converters.Add(new LegacyDocumentModelConverter<CommonToolsListDocumentModel, CommonToolsListDocumentModelLegacy>());
        _options.Converters.Add(new LegacyDocumentModelConverter<CommonPromptDocumentModel, CommonPromptDocumentModelLegacy>());
        _options.Converters.Add(new LegacyDocumentModelConverter<PlugInConfigDocumentModel, PlugInConfigDocumentModelLegacy>());
    }

    #region AgentDocumentModel Tests

    [Fact]
    public void Deserialize_NewAgentSchema_ReturnsAgentDocumentModel()
    {
        // Arrange
        var json = """
        {
            "metadata": {
                "id": "test-agent-id",
                "operationId": "op-123",
                "owner": "test-owner",
                "version": "1.0.0",
                "tags": ["tag1", "tag2"],
                "createdAt": "2024-01-01T00:00:00Z",
                "updatedAt": "2024-01-02T00:00:00Z"
            },
            "spec": {
                "name": "TestAgent",
                "instructions": "Test instructions",
                "handoffDescription": "Test handoff",
                "handoffs": ["Agent1", "Agent2"],
                "tools": ["Tool1", "Tool2"],
                "connectors": ["Connector1"],
                "allowParallelToolCalls": true,
                "maxReflectionCount": 5,
                "temperature": 0.7,
                "enableVanillaMode": false
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AgentDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Tags.ShouldBe(new[] { "tag1", "tag2" });

        result.Name.ShouldBe("TestAgent");
        result.Spec.Instructions.ShouldBe("Test instructions");
        result.Spec.HandoffDescription.ShouldBe("Test handoff");
        result.Spec.Handoffs.ShouldBe(new[] { "Agent1", "Agent2" });
        result.Spec.Tools.ShouldBe(new[] { "Tool1", "Tool2" });
        result.Spec.Connectors.ShouldBe(new[] { "Connector1" });
        result.Spec.AllowParallelToolCalls.ShouldBe(true);
        result.Spec.MaxReflectionCount.ShouldBe(5);
        result.Spec.Temperature.ShouldBe(0.7f);
        result.Spec.EnableVanillaMode.ShouldBe(false);
    }

    [Fact]
    public void Deserialize_LegacyAgentSchema_ConvertsToAgentDocumentModel()
    {
        // Arrange
        var json = """
        {
            "id": "legacy-agent-id",
            "name": "LegacyAgent",
            "instructions": "Legacy instructions",
            "handoffDescription": "Legacy handoff",
            "handoffs": ["Agent1"],
            "tools": ["Tool1"],
            "connectors": ["Connector1"],
            "allowParallelToolCalls": false,
            "agentsAsTools": [],
            "maxReflectionCount": 3,
            "criticPromptPath": "",
            "criticOnHandOff": false,
            "customReflectionNote": "",
            "commonPrompts": [],
            "disableDocumentRetrieval": false,
            "enableHandoffPromptOverride": false,
            "commonTools": [],
            "agentType": 0,
            "orchestrationStartAgents": [],
            "nextAgentMappings": [],
            "metadata": {
                "owner": "legacy-owner",
                "version": "0.9.0"
            },
            "operationId": "legacy-op-123"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AgentDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();

        result.Name.ShouldBe("LegacyAgent");
        result.Spec.Instructions.ShouldBe("Legacy instructions");
        result.Spec.HandoffDescription.ShouldBe("Legacy handoff");
        result.Spec.Handoffs.ShouldBe(new[] { "Agent1" });
        result.Spec.Tools.ShouldBe(new[] { "Tool1" });
        result.Spec.Connectors.ShouldBe(new[] { "Connector1" });
        result.Spec.AllowParallelToolCalls.ShouldBe(false);
        result.Spec.MaxReflectionCount.ShouldBe(3);
    }

    [Fact]
    public void Serialize_AgentDocumentModel_WritesNewSchema()
    {
        // Arrange
        var model = new AgentDocumentModel(
            new ResourceMetadata
            {
                Name = "SerializedAgent",
                Tags = new List<string> { "production" },
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            },
            new AgentSpec
            {
                Instructions = "Serialized instructions",
                Tools = new List<string> { "Tool1" },
                Temperature = 0.5f
            }
        );

        // Act
        var json = JsonSerializer.Serialize(model, _options);
        var deserialized = JsonSerializer.Deserialize<AgentDocumentModel>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Name.ShouldBe("SerializedAgent");
        deserialized.Spec.Instructions.ShouldBe("Serialized instructions");
        deserialized.Spec.Temperature.ShouldBe(0.5f);

        // Verify new schema structure is used
        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"spec\"");
    }

    #endregion

    #region ToolDocumentModel Tests - Kusto Tool

    [Fact]
    public void Deserialize_NewKustoToolSchema_ReturnsKustoToolDocumentModel()
    {
        // Arrange
        var json = """
        {
            "type": "KustoTool",
            "metadata": {
                "id": "kusto-tool-id",
                "operationId": "op-789",
                "owner": "tool-owner",
                "version": "1.0.0",
                "tags": ["kusto", "query"]
            },
            "spec": {
                "name": "TestKustoTool",
                "type": "KustoTool",
                "connector": "KustoConnector",
                "description": "Test Kusto tool",
                "parameters": [
                    {
                        "name": "param1",
                        "type": "string",
                        "description": "Parameter 1"
                    }
                ],
                "attributes": ["attribute1"],
                "mode": 0,
                "database": "TestDatabase",
                "query": "TestTable | take 10"
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<ToolDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<KustoToolDocumentModel>();

        var kustoTool = (KustoToolDocumentModel)result;
        kustoTool.Metadata.Tags.ShouldBe(new[] { "kusto", "query" });

        kustoTool.Name.ShouldBe("TestKustoTool");
        kustoTool.Spec.Type.ShouldBe("KustoTool");
        kustoTool.Spec.Connector.ShouldBe("KustoConnector");
        kustoTool.Spec.Description.ShouldBe("Test Kusto tool");
        kustoTool.Spec.Database.ShouldBe("TestDatabase");
        kustoTool.Spec.Query.ShouldBe("TestTable | take 10");
        kustoTool.Spec.Mode.ShouldBe(Agent.Data.DataModels.KustoExecutionMode.Function);
        kustoTool.Spec.Parameters.ShouldNotBeNull();
        kustoTool.Spec.Parameters.Count.ShouldBe(1);
        kustoTool.Spec.Parameters[0].Name.ShouldBe("param1");
    }

    [Fact]
    public void Deserialize_LegacyKustoToolSchema_ConvertsToKustoToolDocumentModel()
    {
        // Arrange
        var json = """
        {
            "type": "KustoTool",
            "id": "legacy-kusto-id",
            "name": "LegacyKustoTool",
            "connector": "LegacyConnector",
            "description": "Legacy Kusto tool",
            "parameters": [],
            "attributes": [],
            "mode": 1,
            "database": "LegacyDB",
            "query": "LegacyTable",
            "metadata": {
                "owner": "legacy-owner"
            },
            "operationId": "legacy-op"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<ToolDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<KustoToolDocumentModel>();

        var kustoTool = (KustoToolDocumentModel)result;

        kustoTool.Name.ShouldBe("LegacyKustoTool");
        kustoTool.Spec.Type.ShouldBe("KustoTool");
        kustoTool.Spec.Connector.ShouldBe("LegacyConnector");
        kustoTool.Spec.Description.ShouldBe("Legacy Kusto tool");
        kustoTool.Spec.Database.ShouldBe("LegacyDB");
        kustoTool.Spec.Query.ShouldBe("LegacyTable");
        kustoTool.Spec.Mode.ShouldBe(Agent.Data.DataModels.KustoExecutionMode.Query);
    }

    [Fact]
    public void Serialize_KustoToolDocumentModel_WritesNewSchema()
    {
        // Arrange
        var model = new KustoToolDocumentModel(
            new ResourceMetadata { Name = "SerializedKustoTool" },
            new KustoToolSpec
            {
                Type = "KustoTool",
                Connector = "KustoConnector",
                Description = "Serialized Kusto tool",
                Mode = Agent.Data.DataModels.KustoExecutionMode.Function,
                Database = "SerializedDB",
                Function = "MyFunction()"
            }
        );

        // Act
        var json = JsonSerializer.Serialize<ToolDocumentModel>(model, _options);
        var deserialized = JsonSerializer.Deserialize<ToolDocumentModel>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<KustoToolDocumentModel>();

        var kustoTool = (KustoToolDocumentModel)deserialized;
        kustoTool.Name.ShouldBe("SerializedKustoTool");
        kustoTool.Spec.Database.ShouldBe("SerializedDB");
        kustoTool.Spec.Function.ShouldBe("MyFunction()");

        // Verify new schema structure
        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"spec\"");
        // Verify polymorphic type discriminator property and value
        json.ShouldContain("\"type\"");
        json.ShouldContain("\"type\": \"KustoTool\"");
    }

    #endregion

    #region ToolDocumentModel Tests - Link Tool

    [Fact]
    public void Deserialize_NewLinkToolSchema_ReturnsLinkToolDocumentModel()
    {
        // Arrange
        var json = """
        {
            "type": "LinkTool",
            "metadata": {
                "id": "link-tool-id",
                "operationId": "op-link",
                "owner": "link-owner",
                "version": "1.0.0"
            },
            "spec": {
                "name": "TestLinkTool",
                "type": "LinkTool",
                "connector": "HttpConnector",
                "description": "Test link tool",
                "parameters": [],
                "attributes": [],
                "template": "https://example.com/{param1}"
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<ToolDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<LinkToolDocumentModel>();

        var linkTool = (LinkToolDocumentModel)result;

        linkTool.Name.ShouldBe("TestLinkTool");
        linkTool.Spec.Type.ShouldBe("LinkTool");
        linkTool.Spec.Description.ShouldBe("Test link tool");
        linkTool.Spec.Template.ShouldBe("https://example.com/{param1}");
    }

    [Fact]
    public void Deserialize_LegacyLinkToolSchema_ConvertsToLinkToolDocumentModel()
    {
        // Arrange
        var json = """
        {
            "type": "LinkTool",
            "id": "legacy-link-id",
            "name": "LegacyLinkTool",
            "connector": "LegacyHttpConnector",
            "description": "Legacy link tool",
            "parameters": [],
            "attributes": [],
            "template": "https://legacy.com/{id}",
            "metadata": {
                "owner": "legacy-link-owner"
            },
            "operationId": "legacy-link-op"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<ToolDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<LinkToolDocumentModel>();

        var linkTool = (LinkToolDocumentModel)result;

        linkTool.Name.ShouldBe("LegacyLinkTool");
        linkTool.Spec.Type.ShouldBe("LinkTool");
        linkTool.Spec.Template.ShouldBe("https://legacy.com/{id}");
    }

    [Fact]
    public void Serialize_LinkToolDocumentModel_WritesNewSchema()
    {
        // Arrange
        var model = new LinkToolDocumentModel(
            new ResourceMetadata { Name = "SerializedLinkTool" },
            new LinkToolSpec
            {
                Type = "LinkTool",
                Connector = "HttpConnector",
                Description = "Serialized link tool",
                Template = "https://serialized.com/{param}"
            }
        );

        // Act
        var json = JsonSerializer.Serialize<ToolDocumentModel>(model, _options);
        var deserialized = JsonSerializer.Deserialize<ToolDocumentModel>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<LinkToolDocumentModel>();

        var linkTool = (LinkToolDocumentModel)deserialized;
        linkTool.Name.ShouldBe("SerializedLinkTool");
        linkTool.Spec.Template.ShouldBe("https://serialized.com/{param}");

        // Verify new schema structure
        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"spec\"");
        // Verify polymorphic type discriminator property and value
        json.ShouldContain("\"type\"");
        json.ShouldContain("\"type\": \"LinkTool\"");
    }

    #endregion

    #region ConnectorDocumentModel Tests - Kusto Connector

    [Fact]
    public void Deserialize_NewKustoConnectorSchema_ReturnsKustoConnectorDocumentModel()
    {
        // Arrange
        var json = """
        {
            "type": "Kusto",
            "metadata": {
                "id": "kusto-connector-id",
                "operationId": "op-conn-123",
                "owner": "connector-owner",
                "version": "1.0.0",
                "tags": ["kusto", "database"]
            },
            "spec": {
                "name": "TestKustoConnector",
                "type": "Kusto",
                "description": "Test Kusto connector",
                "enabled": true,
                "clusterUrl": "https://test.kusto.windows.net",
                "database": "TestDB",
                "clusterHint": "test-cluster"
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<ConnectorDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<KustoConnectorDocumentModel>();

        var kustoConnector = (KustoConnectorDocumentModel)result;
        kustoConnector.Metadata.Tags.ShouldBe(new[] { "kusto", "database" });

        kustoConnector.Name.ShouldBe("TestKustoConnector");
        kustoConnector.Spec.Type.ShouldBe("Kusto");
        kustoConnector.Spec.Description.ShouldBe("Test Kusto connector");
        kustoConnector.Spec.Enabled.ShouldBe(true);
        kustoConnector.Spec.ClusterUrl.ShouldBe("https://test.kusto.windows.net");
        kustoConnector.Spec.Database.ShouldBe("TestDB");
        kustoConnector.Spec.ClusterHint.ShouldBe("test-cluster");
    }

    [Fact]
    public void Deserialize_LegacyKustoConnectorSchema_ConvertsToKustoConnectorDocumentModel()
    {
        // Arrange
        var json = """
        {
            "type": "Kusto",
            "id": "legacy-connector-id",
            "name": "LegacyKustoConnector",
            "description": "Legacy Kusto connector",
            "enabled": false,
            "clusterUrl": "https://legacy.kusto.windows.net",
            "database": "LegacyDB",
            "auth": {},
            "metadata": {
                "owner": "legacy-connector-owner",
                "version": "0.5.0"
            },
            "operationId": "legacy-conn-op"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<ConnectorDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeOfType<KustoConnectorDocumentModel>();

        var kustoConnector = (KustoConnectorDocumentModel)result;

        kustoConnector.Name.ShouldBe("LegacyKustoConnector");
        kustoConnector.Spec.Type.ShouldBe("Kusto");
        kustoConnector.Spec.Description.ShouldBe("Legacy Kusto connector");
        kustoConnector.Spec.Enabled.ShouldBe(false);
        kustoConnector.Spec.ClusterUrl.ShouldBe("https://legacy.kusto.windows.net");
        kustoConnector.Spec.Database.ShouldBe("LegacyDB");
    }

    [Fact]
    public void Serialize_KustoConnectorDocumentModel_WritesNewSchema()
    {
        // Arrange
        var model = new KustoConnectorDocumentModel(
            new ResourceMetadata
            {
                Name = "SerializedKustoConnector"
            },
            new KustoConnectorSpec
            {
                Type = "Kusto",
                Description = "Serialized Kusto connector",
                Enabled = true,
                ClusterUrl = "https://serialized.kusto.windows.net",
                Database = "SerializedDB"
            }
        );

        // Act
        var json = JsonSerializer.Serialize<ConnectorDocumentModel>(model, _options);
        var deserialized = JsonSerializer.Deserialize<ConnectorDocumentModel>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.ShouldBeOfType<KustoConnectorDocumentModel>();

        var kustoConnector = (KustoConnectorDocumentModel)deserialized;
        kustoConnector.Name.ShouldBe("SerializedKustoConnector");
        kustoConnector.Spec.ClusterUrl.ShouldBe("https://serialized.kusto.windows.net");
        kustoConnector.Spec.Database.ShouldBe("SerializedDB");

        // Verify new schema structure
        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"spec\"");
        // Verify polymorphic type discriminator property and value
        json.ShouldContain("\"type\"");
        json.ShouldContain("\"type\": \"Kusto\"");
    }

    #endregion

    #region CommonToolsListDocumentModel Tests

    [Fact]
    public void Deserialize_NewCommonToolsListSchema_ReturnsCommonToolsListDocumentModel()
    {
        // Arrange
        var json = """
        {
            "metadata": {
                "id": "common-tools-id",
                "operationId": "op-tools-123",
                "owner": "tools-owner",
                "version": "1.0.0",
                "tags": ["common", "tools"]
            },
            "spec": {
                "name": "StandardTools",
                "commonToolsList": ["tool1", "tool2", "tool3"]
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<CommonToolsListDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Tags.ShouldBe(new[] { "common", "tools" });

        result.Name.ShouldBe("StandardTools");
        result.Spec.CommonToolsList.ShouldBe(new[] { "tool1", "tool2", "tool3" });
    }

    [Fact]
    public void Deserialize_LegacyCommonToolsListSchema_ConvertsToCommonToolsListDocumentModel()
    {
        // Arrange
        var json = """
        {
            "id": "legacy-tools-id",
            "name": "LegacyStandardTools",
            "commonToolsList": ["legacyTool1", "legacyTool2"],
            "operationId": "legacy-tools-op",
            "metadata": {
                "owner": "legacy-tools-owner",
                "version": "0.8.0"
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<CommonToolsListDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();

        result.Name.ShouldBe("LegacyStandardTools");
        result.Spec.CommonToolsList.ShouldBe(new[] { "legacyTool1", "legacyTool2" });
    }

    [Fact]
    public void Serialize_CommonToolsListDocumentModel_WritesNewSchema()
    {
        // Arrange
        var model = new CommonToolsListDocumentModel(
            new ResourceMetadata
            {
                Name = "SerializedToolsList"
            },
            new CommonToolListSpec
            {
                CommonToolsList = new List<string> { "serializedTool1", "serializedTool2", "serializedTool3" }
            }
        );

        // Act
        var json = JsonSerializer.Serialize(model, _options);
        var deserialized = JsonSerializer.Deserialize<CommonToolsListDocumentModel>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Name.ShouldBe("SerializedToolsList");
        deserialized.Spec.CommonToolsList.ShouldBe(new[] { "serializedTool1", "serializedTool2", "serializedTool3" });

        // Verify new schema structure
        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"spec\"");
    }

    #endregion

    #region CommonPromptDocumentModel Tests

    [Fact]
    public void Deserialize_NewCommonPromptSchema_ReturnsCommonPromptDocumentModel()
    {
        // Arrange
        var json = """
        {
            "metadata": {
                "id": "common-prompt-id",
                "operationId": "op-prompt-123",
                "owner": "prompt-owner",
                "version": "1.0.0",
                "tags": ["prompt", "common"]
            },
            "spec": {
                "name": "WelcomePrompt",
                "prompt": "Welcome to the system! How can I help you today?"
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<CommonPromptDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Tags.ShouldBe(new[] { "prompt", "common" });

        result.Name.ShouldBe("WelcomePrompt");
        result.Spec.Prompt.ShouldBe("Welcome to the system! How can I help you today?");
    }

    [Fact]
    public void Deserialize_LegacyCommonPromptSchema_ConvertsToCommonPromptDocumentModel()
    {
        // Arrange
        var json = """
        {
            "id": "legacy-prompt-id",
            "name": "LegacyWelcomePrompt",
            "prompt": "Legacy welcome message",
            "metadata": {
                "owner": "legacy-prompt-owner",
                "version": "0.7.0"
            },
            "operationId": "legacy-prompt-op"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<CommonPromptDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();

        result.Name.ShouldBe("LegacyWelcomePrompt");
        result.Spec.Prompt.ShouldBe("Legacy welcome message");
    }

    [Fact]
    public void Serialize_CommonPromptDocumentModel_WritesNewSchema()
    {
        // Arrange
        var model = new CommonPromptDocumentModel(
            new ResourceMetadata
            {
                Name = "SerializedPrompt"
            },
            new CommonPromptSpec
            {
                Prompt = "This is a serialized prompt for testing."
            }
        );

        // Act
        var json = JsonSerializer.Serialize(model, _options);
        var deserialized = JsonSerializer.Deserialize<CommonPromptDocumentModel>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Name.ShouldBe("SerializedPrompt");
        deserialized.Spec.Prompt.ShouldBe("This is a serialized prompt for testing.");

        // Verify new schema structure
        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"spec\"");
    }

    #endregion

    #region PlugInConfigDocumentModel Tests

    [Fact]
    public void Deserialize_NewPlugInConfigSchema_ReturnsPlugInConfigDocumentModel()
    {
        // Arrange
        var json = """
        {
            "metadata": {
                "id": "plugin-config-id",
                "operationId": "op-plugin-123",
                "owner": "plugin-owner",
                "version": "1.0.0",
                "tags": ["plugin", "config"]
            },
            "spec": {
                "name": "TestPluginConfig",
                "config": {
                    "apiKey": "test-key-123",
                    "timeout": 30,
                    "enabled": true
                }
            }
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<PlugInConfigDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();
        result.Metadata.Tags.ShouldBe(new[] { "plugin", "config" });

        result.Name.ShouldBe("TestPluginConfig");
        result.Spec.Config.ShouldNotBeNull();
        result.Spec.Config.Count.ShouldBe(3);
    }

    [Fact]
    public void Deserialize_LegacyPlugInConfigSchema_ConvertsToPlugInConfigDocumentModel()
    {
        // Arrange
        var json = """
        {
            "id": "legacy-plugin-id",
            "name": "LegacyPluginConfig",
            "config": {
                "endpoint": "https://legacy.api.com",
                "retries": 3
            },
            "metadata": {
                "owner": "legacy-plugin-owner",
                "version": "0.6.0"
            },
            "operationId": "legacy-plugin-op"
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<PlugInConfigDocumentModel>(json, _options);

        // Assert
        result.ShouldNotBeNull();

        result.Name.ShouldBe("LegacyPluginConfig");
        result.Spec.Config.ShouldNotBeNull();
        result.Spec.Config.Count.ShouldBe(2);
    }

    [Fact]
    public void Serialize_PlugInConfigDocumentModel_WritesNewSchema()
    {
        // Arrange
        var model = new PlugInConfigDocumentModel(
            new ResourceMetadata
            {
                Name = "SerializedPluginConfig"
            },
            new PluginConfigSpec
            {
                Config = new Dictionary<string, object>
                {
                    { "url", "https://serialized.api.com" },
                    { "maxRetries", 5 },
                    { "useCache", true }
                }
            }
        );

        // Act
        var json = JsonSerializer.Serialize(model, _options);
        var deserialized = JsonSerializer.Deserialize<PlugInConfigDocumentModel>(json, _options);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Name.ShouldBe("SerializedPluginConfig");
        deserialized.Spec.Config.ShouldNotBeNull();
        deserialized.Spec.Config.Count.ShouldBe(3);

        // Verify new schema structure
        json.ShouldContain("\"metadata\"");
        json.ShouldContain("\"spec\"");
    }

    #endregion
}
