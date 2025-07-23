// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Agent.Plugins.Models
{
    public class ICMAlertConfig
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string? Id { get; set; }
        public int TeamId { get; set; }
        public string? AlertingId { get; set; }
        public string? IncidentTitle { get; set; }
        public string? IncidentTitleContains { get; set; }
        public List<string> OwningTeams { get; set; } = new List<string>();
        public string? AgentMode { get; set; }
        public bool UseCorrelationIdForKustoQuery { get; set; }
        public List<GenevaActionConfigBase>? GenevaActions { get; set; } = new List<GenevaActionConfigBase>();
        public List<string>? AllowedGenevaActions { get; set; } = new List<string>();
        public List<ICMConfigKustoQueryModel> KustoQueries { get; set; } = new List<ICMConfigKustoQueryModel>();
        public List<string> Owners { get; set; } = new List<string>();
        public int ActionTimeoutIntervalInMinutes { get; set; }
        public string? DefaultHumanInterventionLoop { get; set; }
        public List<string> RoutingInstructions { get; set; } = new List<string>();
        public List<string> MitigationInstructions { get; set; } = new List<string>();
        public List<string> MonitoringInstructions { get; set; } = new List<string>();
        public List<string> IncidentProcessingGuide { get; set; } = new List<string>();
        public string? AgentName { get; set; }
        public string? ValidationQuery { get; set; }
        public string? MonitorId { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
    public class ICMConfigKustoQueryModel : KustoQueryModel
    {
        public string? ClusterName { get; set; }
        public string? DatabaseName { get; set; }
    }

    public class KustoQueryModel
    {
        public string? KustoQuery { get; set; }
        public int Order { get; set; }
        public string? KustoQueryName { get; set; }
        public string? KustoQueryDescription { get; set; }
    }

    public class KustoCluster
    {
        public string? ClusterName { get; set; }
        public string? DatabaseName { get; set; }
    }

    public class AlertDetailsBase
    {
        public Guid? Id { get; set; }
        public string? ServiceName { get; set; }
        public string? ServiceId { get; set; }
        public string? CreatedBy { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public KustoQueryModel? PrimaryKustoQuery { get; set; }
        public List<KustoQueryModel>? SecondaryKustoQueries { get; set; }
        public List<KustoCluster>? KustoClusters { get; set; }
    }

    public class AlertDetails : AlertDetailsBase
    {
        public int? Severity { get; set; }
        public string? RoutingID { get; set; }
        public string? TeamAssignedTo { get; set; }
        public int? TeamId { get; set; }

        public AlertDetails()
        {
        }

        public AlertDetails(AlertDetailsBase alertDetails)
        {
            Id = alertDetails.Id;
            ServiceName = alertDetails.ServiceName;
            ServiceId = alertDetails.ServiceId;
            CreatedBy = alertDetails.CreatedBy;
            Title = alertDetails.Title;
            Description = alertDetails.Description;
            PrimaryKustoQuery = alertDetails.PrimaryKustoQuery;
            SecondaryKustoQueries = alertDetails.SecondaryKustoQueries;
            KustoClusters = alertDetails.KustoClusters;
        }
    }

    public class AgentFactoryConfigCosmos<T>
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        public T? Content { get; set; }
    }
}
