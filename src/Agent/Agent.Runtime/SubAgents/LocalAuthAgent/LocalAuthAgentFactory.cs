// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Linq.Expressions;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.SubAgents.LocalAuthAgent;

public class LocalAuthAgentFactory : SimpleResourceSubAgentFactoryBase<LocalAuthAgent, LocalAuthAgentInput, LocalAuthAgentActivity, LocalAuthAgentActivityInput>
{
    private readonly IRemediationPlugin remediationPlugin;
    private readonly IRecordActionsPlugin recordActionsPlugin;
    private readonly IGithubIssuePlugin githubPlugin;
    private readonly IGraphDBPlugin graphDBPlugin;

    public LocalAuthAgentFactory(
        IRemediationPlugin remediationPlugin,
        IRecordActionsPlugin recordActionsPlugin,
        IThreadOrchestrationManager mappingManager,
        IToolsRepository toolsRepository,
        IGithubIssuePlugin githubPlugin,
        IGraphDBPlugin graphDBPlugin,
        DurableTaskClient durableTaskClient
        )
        : base(toolsRepository, mappingManager, durableTaskClient)
    {
        this.remediationPlugin = remediationPlugin;
        this.recordActionsPlugin = recordActionsPlugin;
        this.githubPlugin = githubPlugin;
        this.graphDBPlugin = graphDBPlugin;
    }

    protected override IEnumerable<Expression<Func<Delegate>>> GetToolList()
    {
        var remediationPluginDefinition = new RemediationPluginDefinition(remediationPlugin);
        yield return () => remediationPluginDefinition.ServiceBusSetLocalAuthSupport;
        yield return () => remediationPluginDefinition.AzureSqlServerSetLocalAuthSupport;
        yield return () => remediationPluginDefinition.CosmosDbSetKeyBasedAuthSupport;
        yield return () => remediationPluginDefinition.EventHubSetLocalAuthSupport;
        yield return () => remediationPluginDefinition.StorageAccountSetSharedKeySupport;
        yield return () => remediationPluginDefinition.StorageAccountSetContainerPublicAccess;
        yield return () => remediationPluginDefinition.AzureAppServiceSetFtpAuthenticationSupport;
        yield return () => remediationPluginDefinition.AzureAppServiceSetScmAuthenticationSupport;

        var githubPluginDefinition = new GitHubIssuePluginDefinition(githubPlugin);
        yield return () => githubPluginDefinition.CreateGithubIssue;

        var recordActionsPluginDefinition = new RecordActionsPluginDefinition(recordActionsPlugin);
        yield return () => recordActionsPluginDefinition.GetActionDetails;

        var graphDbPluginDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        yield return () => graphDbPluginDefinition.AddIgnoreTagToResource;

        var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
        yield return () => controlFlowPluginDefinition.Wait;
        yield return () => controlFlowPluginDefinition.MarkPlanComplete;
        yield return () => controlFlowPluginDefinition.NotifyUser;
        yield return () => controlFlowPluginDefinition.AskUserForInput;
    }

}
