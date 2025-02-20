using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentCore
{
    public class Incident
    {
        public string IncidentId { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        //public string DiscussionEntry { get; set; }
    }

    public class DiscussionEntry
    {
        public string IncidentId { get; set; }
        public DateTime Date { get; set; }
        public string ChangedBy { get; set; }
        public string Text { get; set; }
        public bool IsHtml { get; set; }
    }

    public class ODataResponse<T>
    {
        [JsonProperty("odata.metadata")]
        public string OdataMetadata { get; set; }

        [JsonProperty("value")]
        public List<T> Value { get; set; }
    }
}
