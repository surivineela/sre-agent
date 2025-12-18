// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Tests.E2E.Helpers;

/// <summary>
/// Helper class to generate YAML content for different tool types and versions in tests.
/// </summary>
public static class TestYamlHelper
{
    // ============================================================
    // V1 Tool YAML Generators
    // ============================================================

    /// <summary>
    /// Generates a V1 KustoTool YAML string.
    /// </summary>
    public static string GetKustoToolV1(
        string name,
        string description = "Test description",
        string connector = "TestConnector",
        string database = "TestDatabase",
        string query = "TestQuery",
        List<(string name, string type, string description)>? parameters = null)
    {
        var parametersYaml = "";
        if (parameters != null && parameters.Count > 0)
        {
            parametersYaml = "\nparameters:";
            foreach (var param in parameters)
            {
                parametersYaml += $@"
  - name: {param.name}
    type: {param.type}
    description: {param.description}";
            }
        }

        return $@"version: v1
name: {name}
type: KustoTool
description: {description}
connector: {connector}
database: {database}
query: {query}{parametersYaml}
";
    }

    /// <summary>
    /// Generates a V1 LinkTool YAML string.
    /// </summary>
    public static string GetLinkToolV1(
        string name,
        string description = "Test description",
        string template = "https://test.example.com",
        List<(string name, string type, string description)>? parameters = null)
    {
        var parametersYaml = "";
        if (parameters != null && parameters.Count > 0)
        {
            parametersYaml = "\nparameters:";
            foreach (var param in parameters)
            {
                parametersYaml += $@"
  - name: {param.name}
    type: {param.type}
    description: {param.description}";
            }
        }

        return $@"version: v1
name: {name}
type: LinkTool
description: {description}
template: {template}{parametersYaml}
";
    }

    // ============================================================
    // V2 Tool YAML Generators
    // ============================================================

    /// <summary>
    /// Generates a V2 KustoTool YAML string.
    /// </summary>
    public static string GetKustoToolV2(
        string name,
        string description = "Test description",
        string connector = "TestConnector",
        string database = "TestDatabase",
        string query = "TestQuery",
        List<(string name, string type, string description)>? parameters = null)
    {
        var parametersYaml = "";
        if (parameters != null && parameters.Count > 0)
        {
            parametersYaml = "\n  parameters:";
            foreach (var param in parameters)
            {
                parametersYaml += $@"
    - name: {param.name}
      type: {param.type}
      description: {param.description}";
            }
        }

        return $@"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: {name}
  owner: someone
  tags:
spec:
  type: KustoTool
  connector: {connector}
  toolMode: Auto
  description: ""{description}""
  database: {database}
  query: |-
    {query}{parametersYaml}
";
    }

    /// <summary>
    /// Generates a V2 LinkTool YAML string.
    /// </summary>
    public static string GetLinkToolV2(
        string name,
        string description = "Test description",
        string template = "https://test.example.com",
        List<(string name, string type, string description)>? parameters = null)
    {
        var parametersYaml = "";
        if (parameters != null && parameters.Count > 0)
        {
            parametersYaml = "\n  parameters:";
            foreach (var param in parameters)
            {
                parametersYaml += $@"
    - name: {param.name}
      type: {param.type}
      description: {param.description}";
            }
        }

        return $@"api_version: azuresre.ai/v2
kind: ExtendedAgentTool
metadata:
  name: {name}
  description: {description}
  owner: someone
  tags:
spec:
  type: LinkTool
  toolMode: Auto
  description: ""{description}""
  template: {template}{parametersYaml}
";
    }

    // ============================================================
    // Convenience Methods with Default Values
    // ============================================================

    /// <summary>
    /// Generates a minimal V1 KustoTool with common test defaults.
    /// </summary>
    public static string GetMinimalKustoToolV1(string name) => GetKustoToolV1(name);

    /// <summary>
    /// Generates a minimal V1 LinkTool with common test defaults.
    /// </summary>
    public static string GetMinimalLinkToolV1(string name) => GetLinkToolV1(name);

    /// <summary>
    /// Generates a minimal V2 KustoTool with common test defaults.
    /// </summary>
    public static string GetMinimalKustoToolV2(string name) => GetKustoToolV2(name);

    /// <summary>
    /// Generates a minimal V2 LinkTool with common test defaults.
    /// </summary>
    public static string GetMinimalLinkToolV2(string name) => GetLinkToolV2(name);

    // ============================================================
    // V1 ToolList YAML Generator
    // ============================================================

    /// <summary>
    /// Generates a V1 ToolList YAML string containing multiple tools.
    /// </summary>
    public static string GetToolListV1(
        string listName,
        List<(string name, string type, string description, string? connector, string? database, string? query, string? template)> tools)
    {
        var toolsYaml = "";
        foreach (var tool in tools)
        {
            toolsYaml += $@"
  - name: {tool.name}
    type: {tool.type}
    description: {tool.description}";

            if (tool.type == "KustoTool")
            {
                toolsYaml += $@"
    connector: {tool.connector}
    database: {tool.database}
    query: {tool.query}";
            }
            else if (tool.type == "LinkTool")
            {
                toolsYaml += $@"
    template: {tool.template}";
            }
        }

        return $@"api_version: agent.platform.ai/v1
kind: ToolList
metadata:
  name: {listName}
  owner: someone
spec:
  tools:{toolsYaml}
";
    }

    // ============================================================
    // Agent YAML Generators
    // ============================================================

    /// <summary>
    /// Generates a minimal V2 Agent YAML string for testing.
    /// </summary>
    public static string GetMinimalAgentV2(
        string name,
        string instructions = "This is a test agent",
        List<string>? tools = null,
        List<string>? handoffs = null)
    {
        var toolsYaml = "";
        if (tools != null && tools.Count > 0)
        {
            toolsYaml = "\n  tools:";
            foreach (var tool in tools)
            {
                toolsYaml += $"\n    - {tool}";
            }
        }

        var handoffsYaml = "";
        if (handoffs != null && handoffs.Count > 0)
        {
            handoffsYaml = "\n  handoffs:";
            foreach (var handoff in handoffs)
            {
                handoffsYaml += $"\n    - {handoff}";
            }
        }

        return $@"api_version: azuresre.ai/v2
kind: ExtendedAgent
metadata:
  name: {name}
  owner: someone
spec:
  instructions: |
    {instructions}{toolsYaml}{handoffsYaml}
";
    }
}
