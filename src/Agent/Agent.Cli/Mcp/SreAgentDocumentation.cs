// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Mcp.Docs;

namespace Agent.Cli.Mcp;

/// <summary>
/// Provides comprehensive documentation about SRE Agent concepts.
/// This is used by the MCP server to provide context to AI assistants.
/// Delegates to specialized documentation files in the Docs/ folder.
/// </summary>
public static class SreAgentDocumentation
{
    public static string GetOverview() => OverviewDocs.GetOverview();

    public static string GetDeploymentWorkflowDocumentation() =>
        $"{WorkflowDocs.GetDeploymentWorkflow()}\n\n{WorkflowDocs.GetDashboardWorkflowGuidance()}\n\n{WorkflowDocs.GetSelfReflectionChecklist()}";

    public static string GetDashboardWorkflowGuidance() => WorkflowDocs.GetDashboardWorkflowGuidance();

    public static string GetSelfReflectionChecklist() => WorkflowDocs.GetSelfReflectionChecklist();

    public static string GetArchitectureDocumentation() => ArchitectureDocs.GetArchitecture();

    public static string GetCliCommandStructure() => ArchitectureDocs.GetCliStructure();

    public static string GetAgentDocumentation() => AgentDocs.GetAgentDocumentation();

    public static string GetToolDocumentation() => ToolDocs.GetToolDocumentation();

    public static string GetTriggerDocumentation() => YamlSchemaDocs.GetTriggerDocumentation();

    public static string GetSubagentDocumentation() => AgentDocs.GetSubagentDocumentation();

    public static string GetScheduledTaskDocumentation() => YamlSchemaDocs.GetScheduledTaskDocumentation();

    public static string GetPlatformSubagentsDocumentation() => AgentDocs.GetPlatformSubagentsDocumentation();

    public static string GetSubagentBuildingDocumentation() => AgentDocs.GetSubagentBuildingDocumentation();

    public static string GetQuickStartGuide() => QuickStartDocs.GetQuickStartGuide();

    public static string GetYamlSchemaDocumentation() => YamlSchemaDocs.GetYamlSchemaDocumentation();

    public static string GetConnectorDocumentation() => ToolDocs.GetConnectorDocumentation();

    public static string GetMcpConfigurationGuide() => QuickStartDocs.GetMcpConfigurationGuide();

    public static string GetAllDocumentation()
    {
        var sections = new[]
        {
            GetOverview(),
            GetDeploymentWorkflowDocumentation(),
            GetArchitectureDocumentation(),
            GetCliCommandStructure(),
            GetAgentDocumentation(),
            GetToolDocumentation(),
            GetTriggerDocumentation(),
            GetSubagentDocumentation(),
            GetSubagentBuildingDocumentation(),
            GetScheduledTaskDocumentation(),
            GetPlatformSubagentsDocumentation(),
            GetYamlSchemaDocumentation(),
            GetConnectorDocumentation(),
            GetMcpConfigurationGuide(),
            GetQuickStartGuide()
        };
        return string.Join("\n\n---\n\n", sections);
    }
}
