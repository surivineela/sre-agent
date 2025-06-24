// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Helpers;

public class AgentNameHelper
{
    /// <summary>
    /// Retrieves the agent name from the environment variable "AGENT_NAME".
    /// </summary>
    /// <returns>The agent name as a string.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the "AGENT_NAME" environment variable is not set.</exception>
    public static string GetAgentName(bool isProd)
    {
        // AGENT_NAME is set here
        // https://github.com/serverless-paas-balam/sreagent-infra/blob/6c0ebfc229330a9992043dd9a7f2641a02d7806a/pkg/controllers/agent_controller.go#L132
        var name = Environment.GetEnvironmentVariable("AGENT_NAME");
        if (string.IsNullOrEmpty(name))
        {
            if (isProd)
            {
                throw new InvalidOperationException("AGENT_NAME environment variable is not set.");
            }

            return "test-agent"; // Default value for non-production environments
        }

        return name;
    }

    /// <summary>
    /// Generates a unique identifier for the main dashboard based on the agent name.
    /// The returned uid is guaranteed to be 40 characters or less, because Grafana has a limit of 40 characters for dashboard UIDs.
    /// https://github.com/grafana/grafana/issues/11620
    /// </summary>
    /// <returns></returns>
    public static string GetMainDashboardUid(bool isProd)
    {
        var name = $"{GetAgentName(isProd).ToLowerInvariant()}-azure-sre-resources";
        return name.Length <= 40 ? name : name[..40];
    }

    /// <summary>
    /// Generates a human-readable title for the main dashboard based on the agent name.
    /// </summary>
    /// <returns></returns>
    public static string GetMainDashboardTitle(bool isProd)
    {
        return $"SRE Agent {GetAgentName(isProd)}: Resource Monitoring Dashboard";
    }

    public static string GetCustomerUploadedDocumentBlobContainerName(bool isProd)
    {
        var name = GetAgentName(isProd).ToLowerInvariant();
        return $"{name}-docs";
    }

    public static string GetCustomerUploadedDocumentIndexName(bool isProd)
    {
        var name = GetAgentName(isProd).ToLowerInvariant();
        var fullName = $"{name}-doc-index";
        return fullName.Length > 128 ? fullName[..128] : fullName;
    }

    public static string GetCustomerUploadedDocumentIndexerName(bool isProd)
    {
        var name = GetAgentName(isProd).ToLowerInvariant();
        var fullName = $"{name}-doc-indexer";
        return fullName.Length > 128 ? fullName[..128] : fullName;
    }

    public static string GetCustomerUploadedDocumentSkillSetName(bool isProd)
    {
        var name = GetAgentName(isProd).ToLowerInvariant();
        var fullName = $"{name}-doc-skills";
        return fullName.Length > 128 ? fullName[..128] : fullName;
    }

    public static string GetCustomerUploadedDocumentAISearchDatasourceName(bool isProd)
    {
        var name = GetAgentName(isProd).ToLowerInvariant();
        var fullName = $"{name}-doc-ds";
        return fullName.Length > 64 ? fullName[..64] : fullName;
    }
}
