using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Core.Models.ServiceNow;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class ServiceNowPluginDefinition
{
    private readonly IServiceNowPlugin _serviceNowPlugin;

    public ServiceNowPluginDefinition(IServiceNowPlugin serviceNowPlugin)
    {
        _serviceNowPlugin = serviceNowPlugin ?? throw new ArgumentNullException(nameof(serviceNowPlugin));
    }

    [Description("Get ServiceNow incident details")]
    public async Task<ServiceNowIncident> GetServiceNowIncident(
        [Description("ServiceNow incident system ID (sys_id)")] string incidentSystemId)
    {
        return await _serviceNowPlugin.GetServiceNowIncident(incidentSystemId);
    }

    [Description("Post ServiceNow discussion entry")]
    public async Task<string> PostServiceNowDiscussionEntry(
        [Description("ServiceNow incident system ID (sys_id)")] string incidentSystemId,
        [Description("Discussion Entry")] string discussionEntry)
    {
        return await _serviceNowPlugin.PostServiceNowDiscussionEntry(incidentSystemId, discussionEntry);
    }

    [Description("Acknowledges a ServiceNow incident")]
    public async Task<string> AcknowledgeServiceNowIncident(
        [Description("ServiceNow incident system ID (sys_id)")] string incidentSystemId)
    {
        return await _serviceNowPlugin.AcknowledgeServiceNowIncident(incidentSystemId);
    }

    [Description("Resolve a ServiceNow incident")]
    public async Task<string> ResolveServiceNowIncident(
        [Description("ServiceNow incident system ID (sys_id)")] string incidentSystemId,
        [Description("Discussion Entry")] string discussionEntry)
    {
        return await _serviceNowPlugin.ResolveServiceNowIncident(incidentSystemId, discussionEntry);
    }
}
