using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Core.Models.ServiceNow;

namespace Agent.Plugins.Interface;

public interface IServiceNowPlugin
{
    Task<ServiceNowIncident> GetServiceNowIncident(string incidentId);
    Task<string> PostServiceNowDiscussionEntry(string incidentId, string discussionEntry);
    Task<string> AcknowledgeServiceNowIncident(string incidentId);
    Task<string> ResolveServiceNowIncident(string incidentId, string discussionEntry);
}
