// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using Agent.Core.Helpers;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.LocalAuthAgent;

//public record ResourceInformationWithAction<T>(SimpleResourceSubAgentResourceInformation Resource, T Action);

public record LocalAuthAgentActivityInput(
    [Description("Into what state should we put key-based access for these resources?")]
        FeatureState LocalAuthSetLocalAuthSupport,
    [Description("The list of resources (as resource IDs) to affect in this run.")]
        List<SimpleResourceSubAgentResourceInformation> Resources
    )
    : SimpleResourceSubAgentActivityInput(Resources)
{
    public LocalAuthAgentActivityInput()
        : this(
            FeatureState.Disabled,
            new List<SimpleResourceSubAgentResourceInformation>())
    {
    }

    public override string GetPlanText()
    {
        return $"""
                I can turn off key-based authentication on the resources listed:
                """;
    }
}

[DurableTask]
public class LocalAuthAgentActivity : SimpleResourceSubAgentActivityBase<LocalAuthAgentActivityInput>
{
    public LocalAuthAgentActivity(IChatClient chatClient) : base(chatClient)
    {
    }

    public override string GetPromptText(LocalAuthAgentActivityInput input)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(LocalAuthAgent), "LocalAuthAgentPlan.txt");
        var systemPrompt = File.ReadAllText(path)
            .Replace("{{ResourceType}}", ResourceTypeName)
            .Replace("{{ActionToTake}}", ActionToTake(input))
            .Replace("{{ToolsList}}", string.Join("\n- ", ToolNames));

        return systemPrompt;
    }

    /// <summary>
    /// Normally, this would be the name of the resource type. However, in this case, we are not
    /// acting on a single resource type, so we use the generic term.
    /// </summary>
    public override string ResourceTypeName { get; } = "resource";

    public override string ActionToTake(LocalAuthAgentActivityInput input)
    {
        var result = new StringBuilder();
        result.Append(input.LocalAuthSetLocalAuthSupport == FeatureState.Enabled
            ? "enable local-auth access"
            : "disable local-auth access"
            );
        return result.ToString();
    }

    public override string[] ToolNames { get; } = [
        nameof(IRemediationPlugin.ServiceBusSetLocalAuthSupport),
        nameof(IRemediationPlugin.AzureSqlServerSetLocalAuthSupport),
        nameof(IRemediationPlugin.StorageAccountSetContainerPublicAccess),
        nameof(IRemediationPlugin.StorageAccountSetSharedKeySupport),
        nameof(IRemediationPlugin.EventHubSetLocalAuthSupport),
        nameof(IRemediationPlugin.CosmosDbSetKeyBasedAuthSupport),
        nameof(IRemediationPlugin.AzureAppServiceSetFtpAuthenticationSupport),
        nameof(IRemediationPlugin.AzureAppServiceSetScmAuthenticationSupport),
        nameof(ControlFlowPluginDefinition.Wait)];

}
