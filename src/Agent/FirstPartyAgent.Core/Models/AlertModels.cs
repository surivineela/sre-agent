// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace FirstPartyAgent.Core.Models
{
    public class ICMAlertConfig
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string? Id { get; set; }
        public int TeamId { get; set; }
        public string AlertingId { get; set; }
        public string? IncidentTitle { get; set; }
        public string? IncidentTitleContains { get; set; }
        public List<string> OwningTeams { get; set; } = new List<string>();
        public string AgentMode { get; set; }
        public bool UseCorrelationIdForKustoQuery { get; set; }
        public List<GenevaActionConfigBase>? GenevaActions { get; set; } = new List<GenevaActionConfigBase>();
        public List<string>? AllowedGenevaActions { get; set; } = new List<string>();
        public List<ICMConfigKustoQueryModel> KustoQueries { get; set; } = new List<ICMConfigKustoQueryModel>();
        public List<string> Owners { get; set;} = new List<string>();
        public int ActionTimeoutIntervalInMinutes { get; set; }
        public string DefaultHumanInterventionLoop { get; set; }
        public List<string> RoutingInstructions { get; set; } = new List<string>();
        public List<string> MitigationInstructions { get; set; } = new List<string>();
        public List<string> MonitoringInstructions { get; set; } = new List<string>();
        public List<string> IncidentProcessingGuide { get; set; } = new List<string>();
    }

    public class GenevaActionConfig : GenevaActionConfigBase
    {
        [Required]
        public bool IsWriteAction { get; set; }
        [Required]
        public bool IsAllowedOnExternalSubs { get; set; }
    }

    public class GenevaActionsConfigCosmos
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<GenevaActionConfig> GenevaActions { get; set; }
        public int TeamId { get; set; }
    }

    public class GenevaActionConfigBase
    {
        public string ActionName { get; set; }
        public string TenantId { get; set; }
        public string WorkflowName { get; set; }
        public List<string> WorkflowInputParameters { get; set; }
    }

    public class ICMConfigKustoQueryModel : KustoQueryModel
    {
        public string Cloud { get; set; } = string.Empty;
        public string Cluster { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
    }

    public class KustoQueryModel
    {
        public string Title { get; set; }
        public string KustoQuery { get; set; }
    }

    public class KustoCluster
    {
        public string Cloud { get; set; }
        public string ServiceName { get; set; }
        public string Cluster { get; set; }
        public string Database { get; set; }
    }

    public class AlertDetailsBase
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ServiceName { get; set; }
        public string ServiceId { get; set; }
        public string CreatedBy { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public KustoQueryModel PrimaryKustoQuery { get; set; }
        public List<KustoQueryModel> SecondaryKustoQueries { get; set; }
        public List<KustoCluster> KustoClusters { get; set; }
    }

    public class AlertDetails : AlertDetailsBase
    {
        public int? Severity { get; set; }
        public string RoutingID { get; set; }
        public string TeamAssignedTo { get; set; }
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

    public class WawsAlertDetails:AlertDetailsBase
    {
        public List<WawsAlertAction> Actions { get; set; }
    }

    public class WawsAlertAction
    {
        public int? Severity { get; set; }
        public string RoutingID { get; set; }
        public string TeamAssignedTo { get; set; }
    }

    public class IcmTeam
    {
        public int? IcmServiceId { get; set; }
        public string IcmServiceName { get; set; }
        public string IcmTeamName { get; set; }
        public int? IcmTeamId { get; set; }
        public string TeamPublicId { get; set; }
    }

    public class AgentDeployment
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int TeamId { get; set; }
        public string SubscriptionId { get; set; }
        public string ResourceGroup { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }

    public class TeamConfig
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; }
    }

    public class IcmIncidentBasicInfo
    {
        public string Title { get; set; }
        public int Severity { get; set; }
        public string State { get; set; }
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class AgentFactoryConfigCosmos<T>
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; }
        public T Content { get; set; }
    }
}
