// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using ModelContextProtocol.Server;

namespace Agent.Cli.Mcp.Tools;

/// <summary>
/// MCP tools for SRE Agent authoring. The workflow is:
/// 1. plan_agent_architecture - Design the solution
/// 2. list_connectors - Check existing connectors
/// 3. create_kusto_connector - Create connector if needed (auto-fallback to manual instructions)
/// 4. generate_and_validate_yaml - Generate agent/tool YAMLs
/// 5. apply_yaml_to_server - Deploy to server
/// </summary>
[McpServerToolType]
public class SreAgentTools
{
    #region STEP 1: Planning (MUST BE CALLED FIRST)

    [McpServerTool(Name = "plan_agent_architecture"),
     Description("""
        FIRST STEP: Design the agent solution before writing YAML.

        Produces:
        1. Mermaid component diagram - ALWAYS DISPLAY TO USER
        2. List of tools needed (system tools vs custom KustoTool/LinkTool)
        3. Connectors required
        4. Implementation checklist

        CRITICAL: After calling this tool, you MUST:
        1. Display the full diagram to the user
        2. Ask for user confirmation before proceeding
        3. DO NOT generate YAML until user approves the plan

        Keep it simple - prefer one well-designed agent over multiple subagents.
        """)]
    public static ArchitecturePlanResult PlanAgentArchitecture(
        [Description("What the user wants to build")] string requirements,
        [Description("Trigger: 'icm', 'scheduled', or 'manual'")] string triggerType = "manual",
        [Description("Kusto connector name if known")] string? kustoConnector = null)
    {
        if (string.IsNullOrWhiteSpace(requirements))
            return new ArchitecturePlanResult(false, "Requirements are required to plan the architecture.", null, null, null, null, null, null);

        var agents = new List<AgentComponent>();
        var tools = new List<ToolComponent>();
        var connectors = new List<string>();
        var checklist = new List<string>();

        // Analyze requirements to determine components
        var reqLower = requirements.ToLowerInvariant();
        var mainAgentName = ExtractAgentName(requirements);

        // Main agent is always needed
        agents.Add(new AgentComponent(mainAgentName, "Main orchestrator agent", true));

        // Determine tools needed based on requirements
        // Kusto-centric: Internal teams diagnose via telemetry, not direct Azure access
        if (reqLower.Contains("kusto") || reqLower.Contains("kql") || reqLower.Contains("log") || reqLower.Contains("telemetry") || reqLower.Contains("metric") || reqLower.Contains("error") || reqLower.Contains("diagnos"))
        {
            tools.Add(new ToolComponent("KustoTool", "Custom (ExtendedAgentTool YAML)", "Query telemetry via Kusto - use subscriptionId, resourceGroup, resourceName as keys"));
            connectors.Add(kustoConnector ?? "Kusto connector - run 'srectl tool show-connectors' to find yours");
        }

        if (reqLower.Contains("icm") || reqLower.Contains("incident") || reqLower.Contains("ticket"))
        {
            tools.Add(new ToolComponent("ICM System Tools", "System", "GetIncidentInfo, PostDiscussionEntry, MitigateIncident, etc."));
            connectors.Add("ICM connector");
        }

        if (reqLower.Contains("email") || reqLower.Contains("notify") || reqLower.Contains("outlook"))
        {
            connectors.Add("Outlook connector for email notifications");
        }

        if (reqLower.Contains("teams") || reqLower.Contains("chat"))
        {
            connectors.Add("Teams connector for notifications");
        }

        if (reqLower.Contains("link") || reqLower.Contains("url") || reqLower.Contains("dashboard"))
        {
            tools.Add(new ToolComponent("LinkTool", "Custom (ExtendedAgentTool YAML)", "Generate URLs to dashboards/portals"));
        }

        if (reqLower.Contains("chart") || reqLower.Contains("graph") || reqLower.Contains("visual"))
        {
            tools.Add(new ToolComponent("Charting System Tools", "System", "GenerateBarChart, GeneratePieChart, etc."));
        }

        // Subagents: Only suggest if truly complex (avoid by default)
        // Most use cases work better with one well-designed agent

        // Handle trigger-specific setup
        var triggerInfo = triggerType.ToLowerInvariant() switch
        {
            "icm" => "ICM trigger: Configure incident handler for your team",
            "scheduled" => "Scheduled trigger: Create CronScheduledTask YAML",
            "manual" => "Manual: Invoke via 'srectl agent chat --name <agent>'",
            _ => "Manual invocation via CLI or API"
        };

        // Build mermaid diagram
        var mermaid = BuildMermaidDiagram(mainAgentName, agents, tools, triggerType, connectors);

        // Build checklist
        checklist.Add("1. Use 'list_connectors' to check existing connectors");
        checklist.Add("2. If needed, use 'create_kusto_connector' to add new connector");
        checklist.Add("3. Create KustoTool YAML for each query pattern");
        checklist.Add("4. Create agent YAML with clear instructions");
        checklist.Add("5. Deploy: tools FIRST, then agent");
        checklist.Add("6. Test: 'srectl agent chat --name <agent>'");

        // Connector notes
        var connectorNotes = !string.IsNullOrEmpty(kustoConnector)
            ? $"Using Kusto connector: {kustoConnector}"
            : "Run 'list_connectors' to find your Kusto connector name";

        // Build full output with explicit confirmation request
        var fullOutput = new StringBuilder();
        fullOutput.AppendLine("# Architecture Plan");
        fullOutput.AppendLine();
        fullOutput.AppendLine("## Component Diagram");
        fullOutput.AppendLine("**DISPLAY THIS DIAGRAM TO THE USER:**");
        fullOutput.AppendLine();
        fullOutput.AppendLine(mermaid);
        fullOutput.AppendLine();
        fullOutput.AppendLine("## Components Summary");
        fullOutput.AppendLine($"- **Main Agent:** {mainAgentName}");
        if (tools.Count > 0)
        {
            fullOutput.AppendLine($"- **Tools:** {string.Join(", ", tools.Select(t => t.Name))}");
        }
        if (connectors.Count > 0)
        {
            fullOutput.AppendLine($"- **Connectors Needed:** {string.Join(", ", connectors)}");
        }
        fullOutput.AppendLine($"- **Trigger:** {triggerInfo}");
        fullOutput.AppendLine();
        fullOutput.AppendLine("## Implementation Checklist");
        foreach (var item in checklist)
        {
            fullOutput.AppendLine($"- [ ] {item}");
        }
        fullOutput.AppendLine();
        fullOutput.AppendLine("---");
        fullOutput.AppendLine("## ⚠️ STOP: USER CONFIRMATION REQUIRED");
        fullOutput.AppendLine();
        fullOutput.AppendLine("**Before proceeding, ask the user:**");
        fullOutput.AppendLine("1. Does this architecture match your requirements?");
        fullOutput.AppendLine("2. Are the components correct?");
        fullOutput.AppendLine("3. Do you want me to proceed with implementation?");
        fullOutput.AppendLine();
        fullOutput.AppendLine("**DO NOT generate YAML until the user confirms the plan.**");

        return new ArchitecturePlanResult(
            true,
            null,
            fullOutput.ToString(),
            [.. agents],
            [.. tools],
            [.. connectors],
            [.. checklist],
            connectorNotes);
    }

    private static string ExtractAgentName(string requirements)
    {
        var words = requirements.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            var name = string.Join("-", words.Take(3).Select(w => w.ToLowerInvariant().Trim(',', '.', ':', ';')));
            return SanitizeKebabCase(name);
        }
        return "sre-agent";
    }

    private static string BuildMermaidDiagram(string mainAgent, List<AgentComponent> agents, List<ToolComponent> tools, string triggerType, List<string> connectors)
    {
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TD");
        sb.AppendLine();

        // Trigger
        var triggerNode = triggerType.ToLowerInvariant() switch
        {
            "icm" => "ICM[\"🔔 ICM Incident\"]",
            "teams" => "Teams[\"💬 Teams Message\"]",
            "scheduled" => "Cron[\"⏰ Scheduled Task\"]",
            _ => "User[\"👤 User/API\"]"
        };
        sb.AppendLine($"    {triggerNode}");
        sb.AppendLine();

        // Main agent
        sb.AppendLine($"    {triggerNode.Split('[')[0]} --> MainAgent[\"{mainAgent}\"]");
        sb.AppendLine();

        // Subagents
        var subagents = agents.Where(a => !a.IsMain).ToList();
        if (subagents.Count > 0)
        {
            sb.AppendLine("    subgraph Subagents");
            foreach (var sub in subagents)
            {
                var nodeId = sub.Name.Replace("-", "");
                sb.AppendLine($"        {nodeId}[\"{sub.Name}\"]");
            }
            sb.AppendLine("    end");
            sb.AppendLine();
            foreach (var sub in subagents)
            {
                var nodeId = sub.Name.Replace("-", "");
                sb.AppendLine($"    MainAgent -.->|handoff| {nodeId}");
            }
            sb.AppendLine();
        }

        // Tools
        if (tools.Count > 0)
        {
            sb.AppendLine("    subgraph Tools");
            foreach (var tool in tools)
            {
                var nodeId = tool.Name.Replace(" ", "");
                var icon = tool.Category.Contains("Platform") ? "🔧" : "📝";
                sb.AppendLine($"        {nodeId}[[\"{icon} {tool.Name}\"]]");
            }
            sb.AppendLine("    end");
            sb.AppendLine();

            // Connect main agent to tools
            foreach (var tool in tools)
            {
                var nodeId = tool.Name.Replace(" ", "");
                sb.AppendLine($"    MainAgent --> {nodeId}");
            }

            // Connect subagents to relevant tools
            foreach (var sub in subagents)
            {
                var subNodeId = sub.Name.Replace("-", "");
                foreach (var tool in tools.Where(t => sub.Name.Contains("kusto") && t.Name.Contains("Kusto", StringComparison.OrdinalIgnoreCase) ||
                                                       sub.Name.Contains("incident") && t.Name.Contains("ICM", StringComparison.OrdinalIgnoreCase)))
                {
                    var toolNodeId = tool.Name.Replace(" ", "");
                    sb.AppendLine($"    {subNodeId} --> {toolNodeId}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("    style MainAgent fill:#4CAF50,color:#fff");
        foreach (var sub in subagents)
        {
            var nodeId = sub.Name.Replace("-", "");
            sb.AppendLine($"    style {nodeId} fill:#2196F3,color:#fff");
        }

        sb.AppendLine("```");

        // Legend
        sb.AppendLine();
        sb.AppendLine("**Legend:** 🔧 Platform tool (no YAML) | 📝 Custom tool (ExtendedAgentTool YAML)");

        return sb.ToString();
    }

    #endregion

    #region STEP 2: Documentation Reference

    [McpServerTool(Name = "get_documentation"),
     Description("Get reference documentation for SRE Agent concepts. Use AFTER plan_agent_architecture to understand specific topics.")]
    public static string GetDocumentation(
        [Description("Topic: 'agents', 'tools', 'triggers', 'subagents', 'connectors', 'yaml-schema', 'quickstart', or 'all'")] string topic)
    {
        return topic.ToLowerInvariant() switch
        {
            "overview" => SreAgentDocumentation.GetOverview(),
            "architecture" or "trigger-flow" or "flow" => SreAgentDocumentation.GetArchitectureDocumentation(),
            "cli" or "commands" => SreAgentDocumentation.GetCliCommandStructure(),
            "agents" or "agent" => SreAgentDocumentation.GetAgentDocumentation(),
            "tools" or "tool" => SreAgentDocumentation.GetToolDocumentation(),
            "triggers" or "trigger" => SreAgentDocumentation.GetTriggerDocumentation(),
            "subagents" or "handoffs" or "subagent" => SreAgentDocumentation.GetSubagentDocumentation() + "\n\n" + SreAgentDocumentation.GetSubagentBuildingDocumentation(),
            "scheduled-tasks" or "scheduled" => SreAgentDocumentation.GetScheduledTaskDocumentation(),
            "platform-subagents" or "platform" => SreAgentDocumentation.GetPlatformSubagentsDocumentation(),
            "yaml-schema" or "schema" => SreAgentDocumentation.GetYamlSchemaDocumentation(),
            "connectors" or "connector" => SreAgentDocumentation.GetConnectorDocumentation(),
            "quickstart" => SreAgentDocumentation.GetQuickStartGuide(),
            "all" => SreAgentDocumentation.GetAllDocumentation(),
            _ => $"Unknown topic: {topic}. Available: agents, tools, triggers, subagents, connectors, yaml-schema, quickstart, all"
        };
    }

    [McpServerTool(Name = "list_stable_tool_types"),
     Description("List CLI-supported BYO tool types for ExtendedAgentTool YAML. Currently: KustoTool (Kusto queries), LinkTool (URL templates).")]
    public static ListStableToolTypesResult ListStableToolTypes(
        [Description("Include verbose descriptions. Default: true")] bool verbose = true)
    {
        var types = ExtendedToolHelper.GetAvailableToolTypes()
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => new ToolTypeEntry(t.Name, verbose ? t.Description : string.Empty))
            .ToArray();

        return new ListStableToolTypesResult(true, types, null);
    }

    #endregion

    #region STEP 3: YAML Generation and Validation

    [McpServerTool(Name = "generate_and_validate_yaml"),
     Description("""
        Generate validated YAML for agents or tools. Use AFTER plan_agent_architecture.

        For 'agent': Generates ExtendedAgent YAML (azuresre.ai/v2)
        For 'tool': Generates ExtendedAgentTool YAML (azuresre.ai/v2) for KustoTool or LinkTool

        Returns blocking errors if required fields missing. Warnings for optional fields.
        """)]
    public static GenerateYamlResult GenerateAndValidateYaml(
        [Description("What to generate: 'agent' or 'tool'")] string kind,
        [Description("Name for the agent or tool")] string name,
        [Description("Description/purpose of the agent or tool")] string description,
        [Description("For agent: ignored (model=gpt-5). For tool: type (KustoTool, LinkTool)")] string modelOrType = "gpt-5",
        [Description("For agent: list of tool names to include")] string[]? tools = null,
        [Description("For agent: list of handoff/subagent names")] string[]? handoffs = null,
        [Description("For KustoTool: connector name (required)")] string? connector = null,
        [Description("For KustoTool: database name (required)")] string? database = null,
        [Description("For KustoTool: the KQL query")] string? query = null,
        [Description("For LinkTool: URL template with {param} placeholders")] string? urlTemplate = null,
        [Description("Tool parameters as 'name:type:description' strings")] string[]? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return new GenerateYamlResult(false, "kind is required ('agent' or 'tool')", null, []);

        if (string.IsNullOrWhiteSpace(name))
            return new GenerateYamlResult(false, "name is required", null, []);

        var safeName = SanitizeKebabCase(name);
        var warnings = new List<string>();

        if (kind.Equals("agent", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateAgentYaml(safeName, description, tools, handoffs, warnings);
        }
        else if (kind.Equals("tool", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateToolYaml(safeName, description, modelOrType, connector, database, query, urlTemplate, parameters, warnings);
        }
        else
        {
            return new GenerateYamlResult(false, $"Unknown kind '{kind}'. Use 'agent' or 'tool'.", null, []);
        }
    }

    private static GenerateYamlResult GenerateAgentYaml(
        string name, string description, string[]? tools, string[]? handoffs, List<string> warnings)
    {
        // Build tools list
        var toolsList = "[]";
        if (tools is { Length: > 0 })
        {
            var distinctTools = tools.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            toolsList = "\n" + string.Join("\n", distinctTools.Select(t => $"    - {t}"));

            var customTools = distinctTools.Where(t => !PlatformToolsProvider.IsPlatformTool(t)).ToList();
            if (customTools.Count > 0)
            {
                warnings.Add($"Custom tools [{string.Join(", ", customTools)}] need ExtendedAgentTool YAML first.");
            }
        }

        // Build handoffs list
        var handoffsList = "[]";
        if (handoffs is { Length: > 0 })
        {
            var distinctHandoffs = handoffs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            handoffsList = "\n" + string.Join("\n", distinctHandoffs.Select(h => $"    - {SanitizeKebabCase(h)}"));
            warnings.Add($"Handoffs [{string.Join(", ", distinctHandoffs)}] need agent YAML first.");
        }

        // Build handoff description
        var handoffDesc = string.IsNullOrWhiteSpace(description)
            ? $"Delegate to {name} for specialized tasks"
            : description;

        var yaml = $"""
            api_version: {YamlApiVersion.V2.Value}
            kind: ExtendedAgent
            metadata:
              name: {name}
            spec:
              instructions: |-
                You are '{name}'. {description}

                ## Responsibilities
                - Analyze requests and context
                - Use tools to gather information
                - Provide clear, actionable responses
              handoffDescription: '{handoffDesc}'
              handoffs: {handoffsList}
              tools: {toolsList}
              maxReflectionCount: 0
              customReflectionNote: ''
              commonPrompts: []
              enableVanillaMode: false
            """;

        return new GenerateYamlResult(true, null, yaml.Trim(), [.. warnings]);
    }

    private static GenerateYamlResult GenerateToolYaml(
        string name, string description, string toolType, string? connector, string? database, string? query,
        string? urlTemplate, string[]? parameters, List<string> warnings)
    {
        var type = toolType.ToLowerInvariant() switch
        {
            "kusto" or "kustotool" => "KustoTool",
            "link" or "linktool" => "LinkTool",
            _ => toolType
        };

        // Validate supported types
        if (type != "KustoTool" && type != "LinkTool")
        {
            return new GenerateYamlResult(false, $"Unsupported tool type '{type}'. Supported: KustoTool, LinkTool. Run list_stable_tool_types.", null, []);
        }

        // Build parameters section
        var paramsYaml = BuildParametersYaml(parameters);

        return type switch
        {
            "KustoTool" => GenerateKustoToolYaml(name, description, connector, database, query, paramsYaml, warnings),
            "LinkTool" => GenerateLinkToolYaml(name, description, urlTemplate, paramsYaml, warnings),
            _ => new GenerateYamlResult(false, $"Unknown type: {type}", null, [])
        };
    }

    private static GenerateYamlResult GenerateKustoToolYaml(
        string name, string description, string? connector, string? database, string? query, string paramsYaml, List<string> warnings)
    {
        // Blocking validation - connector and database are required
        if (string.IsNullOrWhiteSpace(connector))
        {
            return new GenerateYamlResult(false, "KustoTool requires 'connector'. Run: srectl tool show-connectors", null, []);
        }
        if (string.IsNullOrWhiteSpace(database))
        {
            return new GenerateYamlResult(false, "KustoTool requires 'database'.", null, []);
        }

        // Query warning (not blocking - can be added later)
        var queryYaml = "    // <REPLACE:your-kql-query>\n    // Use {{paramName}} for parameters";
        if (!string.IsNullOrWhiteSpace(query))
        {
            queryYaml = string.Join("\n", query.Split('\n').Select(l => $"    {l}"));
        }
        else
        {
            warnings.Add("Query is empty. Add KQL before applying.");
        }

        var yaml = $"""
            api_version: {YamlApiVersion.V2.Value}
            kind: ExtendedAgentTool
            metadata:
              name: {name}
            spec:
              type: KustoTool
              description: |
                {description}
              connector: {connector}
              database: {database}
              query: |
            {queryYaml}
            {paramsYaml}
            """;

        return new GenerateYamlResult(true, null, yaml.Trim(), [.. warnings]);
    }

    private static GenerateYamlResult GenerateLinkToolYaml(
        string name, string description, string? urlTemplate, string paramsYaml, List<string> warnings)
    {
        // Blocking validation
        if (string.IsNullOrWhiteSpace(urlTemplate))
        {
            return new GenerateYamlResult(false, "LinkTool requires 'urlTemplate'.", null, []);
        }

        var yaml = $"""
            api_version: {YamlApiVersion.V2.Value}
            kind: ExtendedAgentTool
            metadata:
              name: {name}
            spec:
              type: LinkTool
              description: |
                {description}
              template: "{urlTemplate}"
            {paramsYaml}
            """;

        return new GenerateYamlResult(true, null, yaml.Trim(), [.. warnings]);
    }

    private static string BuildParametersYaml(string[]? parameters)
    {
        if (parameters is not { Length: > 0 }) return "";

        var lines = new List<string> { "  parameters:" };
        foreach (var param in parameters)
        {
            var parts = param.Split(':');
            var paramName = parts[0].Trim();
            var paramType = parts.Length > 1 ? parts[1].Trim() : "string";
            var paramDesc = parts.Length > 2 ? parts[2].Trim() : $"Parameter {paramName}";

            lines.Add($"    - name: {paramName}");
            lines.Add($"      type: {paramType}");
            lines.Add($"      description: {paramDesc}");
            lines.Add("      required: true");
        }
        return string.Join("\n", lines);
    }

    [McpServerTool(Name = "validate_workflow"),
     Description("Validate an existing agent YAML for correctness. Use this to check YAML you didn't generate with generate_and_validate_yaml.")]
    public static WorkflowValidationResult ValidateWorkflow(
        [Description("The YAML content to validate")] string yamlContent)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(yamlContent))
            return new WorkflowValidationResult(false, ["YAML content is empty"], []);

        // Basic structure checks
        var hasName = Regex.IsMatch(yamlContent, @"^\s*name\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase) ||
                      Regex.IsMatch(yamlContent, @"^\s*metadata\s*:\s*\r?\n\s*name\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!hasName)
            errors.Add("Missing required name");

        var hasInstructions = yamlContent.Contains("instructions:", StringComparison.OrdinalIgnoreCase) ||
                              yamlContent.Contains("systemPrompt:", StringComparison.OrdinalIgnoreCase);
        if (!hasInstructions)
            errors.Add("Missing instructions");

        if (!yamlContent.Contains("model:", StringComparison.OrdinalIgnoreCase))
            warnings.Add("No model specified - server will use default");

        if (!yamlContent.Contains("api_version:", StringComparison.OrdinalIgnoreCase))
            warnings.Add("No api_version - use 'azuresre.ai/v2' for new agents");

        if (!yamlContent.Contains("tools:", StringComparison.OrdinalIgnoreCase))
            warnings.Add("No tools defined - agent will only respond with text");

        // Check for connector references
        var connectorMatches = Regex.Matches(yamlContent, @"^\s*connector\s*:\s*(\S+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (connectorMatches.Count > 0)
        {
            var connectors = connectorMatches.Select(m => m.Groups[1].Value).Distinct().ToList();
            warnings.Add($"Connectors referenced: [{string.Join(", ", connectors)}]. Run 'srectl tool show-connectors' to verify access.");
        }

        return new WorkflowValidationResult(errors.Count == 0, [.. errors], [.. warnings]);
    }

    #endregion

    #region STEP 4: Deployment

    [McpServerTool(Name = "apply_yaml_to_server"),
     Description("Apply YAML to the configured SRE Agent server. Use AFTER generating and reviewing YAML. Requires 'srectl init' configuration.")]
    public static async Task<ApplyYamlResult> ApplyYamlToServer(
        [Description("YAML content to apply")] string yamlContent,
        [Description("Optional label for logs (e.g., 'agent:my-agent')")] string? sourceName = null)
    {
        if (string.IsNullOrWhiteSpace(yamlContent))
            return new ApplyYamlResult(false, "YAML content is empty", null);

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "sreagent-mcp");
            Directory.CreateDirectory(tempDir);

            var detectedName = TryExtractYamlName(yamlContent);
            var safeLabel = string.IsNullOrWhiteSpace(sourceName) ? (detectedName ?? "resource") : sourceName;
            var fileName = $"apply-{SanitizeFileName(safeLabel)}-{DateTime.UtcNow:yyyyMMddHHmmss}.yaml";
            var tempFilePath = Path.Combine(tempDir, fileName);

            try
            {
                await File.WriteAllTextAsync(tempFilePath, yamlContent);
                var (exitCode, output, error) = RunSrectl($"apply-yaml --file \"{tempFilePath}\"");
                var msg = exitCode == 0
                    ? (string.IsNullOrWhiteSpace(output) ? "Applied successfully." : output.Trim())
                    : (string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());

                return new ApplyYamlResult(exitCode == 0, msg, detectedName);
            }
            finally
            {
                try { File.Delete(tempFilePath); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            return new ApplyYamlResult(false, $"Apply failed: {ex.Message}", null);
        }
    }

    [McpServerTool(Name = "learn_topic"),
     Description("Upload information to the agent's knowledge base for RAG retrieval. Requires 'srectl init' configuration.")]
    public static async Task<LearnTopicResult> LearnTopic(
        [Description("Topic/filename for the knowledge (e.g., 'runbook-vm-restart')")] string topicName,
        [Description("The content to upload")] string content)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return new LearnTopicResult(false, "topicName is required", null);
        if (string.IsNullOrWhiteSpace(content))
            return new LearnTopicResult(false, "content is required", null);

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "sreagent-mcp");
            Directory.CreateDirectory(tempDir);

            var safeName = SanitizeFileName(topicName);
            var fileName = safeName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? safeName : $"{safeName}.md";
            var tempFilePath = Path.Combine(tempDir, fileName);

            try
            {
                await File.WriteAllTextAsync(tempFilePath, content);
                var (exitCode, output, error) = RunSrectl($"learn --file \"{tempFilePath}\"");

                return exitCode == 0
                    ? new LearnTopicResult(true, string.IsNullOrWhiteSpace(output) ? "Uploaded successfully." : output.Trim(), fileName)
                    : new LearnTopicResult(false, $"Upload failed: {(string.IsNullOrWhiteSpace(error) ? output : error).Trim()}", null);
            }
            finally
            {
                try { File.Delete(tempFilePath); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            return new LearnTopicResult(false, $"Upload error: {ex.Message}", null);
        }
    }

    #endregion

    #region STEP 5: Connector Management

    [McpServerTool(Name = "list_connectors"),
     Description("""
        List available connectors on the configured SRE Agent server.
        Returns connector names, types, and data sources.
        Use this to find existing Kusto connectors before creating KustoTool YAML.
        """)]
    public static ListConnectorsResult ListConnectors()
    {
        try
        {
            var (exitCode, output, error) = RunSrectl("tool show-connectors");

            if (exitCode == 0)
            {
                return new ListConnectorsResult(true, output.Trim(), null);
            }
            else
            {
                var errMsg = string.IsNullOrWhiteSpace(error) ? output : error;
                if (errMsg.Contains("401") || errMsg.Contains("403"))
                {
                    return new ListConnectorsResult(false, null, "Access denied. Run 'srectl init' to configure authentication.");
                }
                return new ListConnectorsResult(false, null, $"Failed to list connectors: {errMsg.Trim()}");
            }
        }
        catch (Exception ex)
        {
            return new ListConnectorsResult(false, null, $"Error: {ex.Message}");
        }
    }

    [McpServerTool(Name = "create_kusto_connector"),
     Description("""
        Get instructions to create a Kusto connector for the SRE Agent.
        First checks if connector already exists (if so, reuse it).
        Returns Az CLI / PowerShell commands for the user to run.
        """)]
    public static CreateConnectorResult CreateKustoConnector(
        [Description("Connector name (e.g., 'my-kusto-connector')")] string name,
        [Description("Kusto cluster URL (e.g., 'https://mycluster.eastus2.kusto.windows.net')")] string clusterUrl,
        [Description("Default database name (optional)")] string? database = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new CreateConnectorResult(false, null, "Connector name is required", null);
        if (string.IsNullOrWhiteSpace(clusterUrl))
            return new CreateConnectorResult(false, null, "Cluster URL is required", null);

        // First, check if connector already exists
        var (listExit, listOutput, _) = RunSrectl("tool show-connectors");
        if (listExit == 0 && listOutput.Contains(name, StringComparison.OrdinalIgnoreCase))
        {
            return new CreateConnectorResult(true, $"Connector '{name}' already exists. Reuse it in your KustoTool YAML.", null, null);
        }

        // No CLI command for creating connectors - provide manual instructions directly
        var manualInstructions = GenerateConnectorCreationInstructions(name, clusterUrl, database);
        return new CreateConnectorResult(
            true,
            $"Connector '{name}' not found. Follow the instructions below to create it.",
            null,
            manualInstructions);
    }

    private static string GenerateConnectorCreationInstructions(string name, string clusterUrl, string? database)
    {
        // User needs to know their subscription, resource group, and agent name
        // Using regular string concatenation to avoid raw string escaping issues
        var sb = new StringBuilder();
        sb.AppendLine("## Manual Connector Creation Required");
        sb.AppendLine();
        sb.AppendLine("You need Contributor access to create connectors. Run one of these commands:");
        sb.AppendLine();
        sb.AppendLine("### Option 1: Az CLI");
        sb.AppendLine("```bash");
        sb.AppendLine("# First, run 'srectl status' to get your subscription, resource group, and agent name");
        sb.AppendLine("az rest --method PUT \\");
        sb.AppendLine($"  --url \"https://management.azure.com{{agentResourceId}}/DataConnectors/{name}?api-version=2025-05-01-preview\" \\");
        sb.AppendLine($"  --body '{{\"properties\":{{\"name\":\"{name}\",\"dataConnectorType\":\"Kusto\",\"dataSource\":\"{clusterUrl}\",\"identity\":\"system\"}}}}'");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Option 2: PowerShell");
        sb.AppendLine("```powershell");
        sb.AppendLine("# First, run 'srectl status' to get your subscription, resource group, and agent name");
        sb.AppendLine("# Requires Az module and Az.Accounts connected");
        sb.AppendLine("$body = @{");
        sb.AppendLine("    properties = @{");
        sb.AppendLine($"        name = \"{name}\"");
        sb.AppendLine("        dataConnectorType = \"Kusto\"");
        sb.AppendLine($"        dataSource = \"{clusterUrl}\"");
        sb.AppendLine("        identity = \"system\"");
        sb.AppendLine("    }");
        sb.AppendLine("} | ConvertTo-Json -Depth 10");
        sb.AppendLine();
        sb.AppendLine("Invoke-AzRestMethod -Method PUT `");
        sb.AppendLine($"  -Path \"{{agentResourceId}}/DataConnectors/{name}?api-version=2025-05-01-preview\" `");
        sb.AppendLine("  -Payload $body");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### Finding your Agent Resource ID");
        sb.AppendLine("Run: `srectl status` to get the subscription, resource group, and agent name.");
        sb.AppendLine("Or check Azure Portal > Your Agent > Properties > Resource ID.");
        sb.AppendLine();
        sb.AppendLine("The format is: `/subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.App/agents/{agentName}`");
        sb.AppendLine();
        sb.AppendLine("After creating the connector, run `list_connectors` to verify.");

        return sb.ToString();
    }

    #endregion

    #region Helper Methods

    private static (int ExitCode, string Output, string Error) RunSrectl(string arguments)
    {
        var srectlPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(srectlPath))
            return (-1, "", "Could not determine srectl executable path");

        var startInfo = new ProcessStartInfo
        {
            FileName = srectlPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            return (-1, "", "Failed to start srectl process");

        process.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(30000))
        {
            try { process.Kill(); } catch { /* ignore */ }
            return (-1, "", "Process timed out after 30 seconds");
        }

        return (process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
    }

    private static string? TryExtractYamlName(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return null;

        var match = Regex.Match(yaml, @"^\s*name\s*:\s*(?<name>[^\s#\r\n]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (match.Success)
            return TrimYamlScalar(match.Groups["name"].Value);

        match = Regex.Match(yaml, @"^\s*metadata\s*:\s*\r?\n\s*name\s*:\s*(?<name>[^\s#\r\n]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (match.Success)
            return TrimYamlScalar(match.Groups["name"].Value);

        return null;
    }

    private static string TrimYamlScalar(string value)
    {
        var trimmed = value.Trim();
        if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) || (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
            return trimmed[1..^1].Trim();
        return trimmed;
    }

    private static string SanitizeKebabCase(string name)
    {
        var cleaned = new string(name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        cleaned = Regex.Replace(cleaned, "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? "agent" : cleaned;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c) && c != ' ').ToArray());
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }

    #endregion
}

#region Result Records

public record ArchitecturePlanResult(
    bool Success,
    string? Error,
    string? MermaidDiagram,
    AgentComponent[]? Agents,
    ToolComponent[]? Tools,
    string[]? Connectors,
    string[]? Checklist,
    string? Notes);

public record AgentComponent(string Name, string Description, bool IsMain);

public record ToolComponent(string Name, string Category, string Description);

public record GenerateYamlResult(
    bool Success,
    string? Error,
    string? Yaml,
    string[] Warnings);

public record LearnTopicResult(bool Success, string Message, string? FileName);

public record WorkflowValidationResult(bool IsValid, string[] Errors, string[] Warnings);

public record ApplyYamlResult(bool Success, string Message, string? DetectedName);

public record ToolTypeEntry(string Name, string Description);

public record ListStableToolTypesResult(bool Success, ToolTypeEntry[] ToolTypes, string? Error);

public record ListConnectorsResult(bool Success, string? ConnectorList, string? Error);

public record CreateConnectorResult(bool Success, string? Message, string? Error, string? ManualInstructions);

#endregion
