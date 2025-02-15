using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models.ICM;

namespace FirstPartyAgent.Plugins
{
    public class IcmPlugin : IIcmPlugin
    {
        public Task<bool> AddDiscussionEntry(string incidentId, string text)
        {
            throw new NotImplementedException();
        }

        public Task<bool> AddTag(string incidentId, string tag)
        {
            throw new NotImplementedException();
        }

        public Task<List<DiscussionEntry>?> GetDiscussionEntries(string incidentId, DateTimeOffset queryFrom)
        {
            throw new NotImplementedException();
        }

        public Task<Incident?> GetIncidentInfo(string incidentId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Incident>> GetIncidents(string tenant, string metrics)
        {
            throw new NotImplementedException();
        }

        public Task<bool> MitigateIncident(string incidentId, string reason)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ResolveIncident(string incidentId, string reason)
        {
            throw new NotImplementedException();
        }
    }
}
