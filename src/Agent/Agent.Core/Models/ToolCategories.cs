namespace Agent.Core.Models;

public static class ToolCategories
{
    public const string AzureOperation = "Azure Operation";
    public const string CodeAnalysis = "Code Analysis";
    public const string Configuration = "Configuration";
    public const string Deployment = "Deployment";
    public const string DevOps = "DevOps";
    public const string Diagnostics = "Diagnostics";
    public const string IncidentManagement = "Incident Management";
    public const string KnowledgeBase = "Knowledge Base";
    public const string LogQuery = "Log Query";
    public const string Monitoring = "Monitoring";
    public const string Remediation = "Remediation";
    public const string RoleAssignment = "Role Assignment";

    //Category for tools that are only intended for use within the agent framework/system and need not be exposed to the user.
    public const string System = "System";

    public const string Utility = "Utility";
    public const string Visualization = "Visualization";
}
