using Agent.Core.Models.ICM;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Tests.Integration.Mocks
{
    public class MockIcmPlugin : IIcmPlugin
    {
        public string? LastDiscussionEntryText { get; private set; }

        public Task<Incident?> GetIncidentInfo(string incidentId)
        {
            // Return a mock incident or null
            return Task.FromResult<Incident?>(new Incident
            {
                IncidentId = incidentId,
                Title = "Mock Incident",
                Summary = "This is a mock incident",
                DiscussionEntry = "Mock discussion entry"
            });
        }

        public Task<List<Incident>> GetIncidents(string tenant, string metrics)
        {
            // Return a list of mock incidents
            return Task.FromResult(new List<Incident>
            {
                new Incident
                {
                    IncidentId = "100000000",
                    Title = "Mock Incident 1",
                    Summary = "This is the first mock incident",
                    DiscussionEntry = "Mock discussion entry 1"
                },
                new Incident
                {
                    IncidentId = "20000000",
                    Title = "Mock Incident 2",
                    Summary = "This is the second mock incident",
                    DiscussionEntry = "Mock discussion entry 2"
                }
            });
        }

        public Task<bool> MitigateIncident(string incidentId, string reason)
        {
            // Return a mock result
            return Task.FromResult(true);
        }

        public Task<bool> ResolveIncident(string incidentId, string reason)
        {
            // Return a mock result
            return Task.FromResult(true);
        }

        public Task<bool> AddTag(string incidentId, string tag)
        {
            // Return a mock result
            return Task.FromResult(true);
        }

        public Task<List<DiscussionEntry>?> GetDiscussionEntries(string incidentId, DateTimeOffset queryFrom)
        {
            // Return a list of mock discussion entries
            return Task.FromResult<List<DiscussionEntry>?>(new List<DiscussionEntry>
            {
                new DiscussionEntry
                {
                    IncidentId = incidentId,
                    Date = DateTimeOffset.Now.DateTime,
                    ChangedBy = "Mock User",
                    Text = "Mock discussion entry text",
                    IsHtml = false
                }
            });
        }

        public Task<bool> AddDiscussionEntry(string incidentId, string text)
        {
            LastDiscussionEntryText = text;
            // Return a mock result
            return Task.FromResult(true);
        }
    }
    
}