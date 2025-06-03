
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
    /// </summary>
    /// <returns></returns>
    public static string GetMainDashboardUid(bool isProd)
    {
        return $"azure-sre-resources-{GetAgentName(isProd).ToLowerInvariant()}";
    }

    /// <summary>
    /// Generates a human-readable title for the main dashboard based on the agent name.
    /// </summary>
    /// <returns></returns>
    public static string GetMainDashboardTitle(bool isProd)
    {
        return $"SRE Agent {GetAgentName(isProd)}: Resource Monitoring Dashboard";
    }
}
