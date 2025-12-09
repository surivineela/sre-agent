// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for extension commands.
/// </summary>
public static class ExtensionCommandOptions
{
    // ============================================================
    // Extension GenerateEv2 Command Options
    // ============================================================

    public static class GenerateEv2
    {
        public static readonly Option<string> ToolsFolderOption = new("--tools-folder")
        {
            Required = true,
            Description = "Path to the tools folder containing tool configurations"
        };

        public static readonly Option<string> AgentFolderOption = new("--agent-folder")
        {
            Required = true,
            Description = "Path to the agent folder containing agent configurations"
        };

        public static readonly Option<string> OutputOption = new("--output")
        {
            Required = true,
            Description = "Output folder where generated EV2 files will be placed"
        };

        public static readonly Option<string?> ServiceIdentifierOption = new("--service-identifier")
        {
            Description = "EV2 service identifier GUID (required with other EV2 options)"
        };

        public static readonly Option<string?> ServiceGroupOption = new("--service-group")
        {
            Description = "EV2 service group name (required with other EV2 options)"
        };

        public static readonly Option<string?> EnvironmentOption = new("--environment")
        {
            Description = "Environment name (required with other EV2 options)"
        };

        public static readonly Option<string?> TenantIdOption = new("--tenant-id")
        {
            Description = "Azure AD Tenant ID (required with other EV2 options)"
        };

        public static readonly Option<string?> SubscriptionKeyOption = new("--subscription-key")
        {
            Description = "EV2 subscription key name (required with other EV2 options)"
        };

        public static readonly Option<string?> SubscriptionIdOption = new("--subscription-id")
        {
            Description = "Azure subscription ID GUID (required with other EV2 options)"
        };

        public static readonly Option<string?> ResourceGroupOption = new("--resource-group")
        {
            Description = "Azure resource group name (required with other EV2 options)"
        };

        public static readonly Option<string?> AgentNameOption = new("--agent-name")
        {
            Description = "Agent name for deployment (required with other EV2 options)"
        };
    }
}
