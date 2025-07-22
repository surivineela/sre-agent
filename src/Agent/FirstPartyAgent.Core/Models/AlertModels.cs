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
        public string AlertingId { get; set; } = string.Empty;
        public string? IncidentTitle { get; set; }
        public string? IncidentTitleContains { get; set; }
        public List<string> OwningTeams { get; set; } = new List<string>();
        public string AgentMode { get; set; } = string.Empty;
        public bool UseCorrelationIdForKustoQuery { get; set; }
        public List<GenevaActionConfigBase>? GenevaActions { get; set; } = new List<GenevaActionConfigBase>();
        public List<string>? AllowedGenevaActions { get; set; } = new List<string>();
        public List<ICMConfigKustoQueryModel> KustoQueries { get; set; } = new List<ICMConfigKustoQueryModel>();
        public List<string> Owners { get; set; } = new List<string>();
        public int ActionTimeoutIntervalInMinutes { get; set; }
        public string DefaultHumanInterventionLoop { get; set; } = string.Empty;
        public List<string> RoutingInstructions { get; set; } = new List<string>();
        public List<string> MitigationInstructions { get; set; } = new List<string>();
        public List<string> MonitoringInstructions { get; set; } = new List<string>();
        public List<string> IncidentProcessingGuide { get; set; } = new List<string>();
        public string? AgentName { get; set; }
        public string? ValidationQuery { get; set; }
        public string? MonitorId { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }

    public class GenevaActionConfig : GenevaActionConfigBase
    {
        [Required]
        public bool IsWriteAction { get; set; }
        [Required]
        public bool IsAllowedOnExternalSubs { get; set; }

        public bool IsApprovalNeeded { get; set; }

        public Guid? ServiceTreeId { get; set; }
    }

    public class GenevaActionsConfigCosmos
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<GenevaActionConfig> GenevaActions { get; set; } = [];
        public int TeamId { get; set; }
        public Guid ServiceTreeId { get; set; } = Guid.NewGuid();
    }

    public class GenevaActionConfigBase
    {
        public string ActionName { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;  
        public string WorkflowName { get; set; } = string.Empty;
        public List<string> WorkflowInputParameters { get; set; } = [];
    }

    public class ICMConfigKustoQueryModel : KustoQueryModel
    {
        public string Cloud { get; set; } = string.Empty;
        public string Cluster { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
    }

    public class KustoQueryModel
    {
        public string Title { get; set; } = string.Empty;
        public string KustoQuery { get; set; } = string.Empty;
    }

    public class KustoCluster
    {
        public string Cloud { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string Cluster { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
    }

    public class AlertDetailsBase
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public KustoQueryModel PrimaryKustoQuery { get; set; } = new KustoQueryModel();
        public List<KustoQueryModel> SecondaryKustoQueries { get; set; } = new List<KustoQueryModel>();
        public List<KustoCluster> KustoClusters { get; set; } = new List<KustoCluster>();
    }

    public class AlertDetails : AlertDetailsBase
    {
        public int? Severity { get; set; }
        public string RoutingID { get; set; } = string.Empty;
        public string TeamAssignedTo { get; set; } = string.Empty;
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

    public class WawsAlertDetails : AlertDetailsBase
    {
        public List<WawsAlertAction> Actions { get; set; } = [];
    }

    public class WawsAlertAction
    {
        public int? Severity { get; set; }
        public string RoutingID { get; set; } = string.Empty;
        public string TeamAssignedTo { get; set; } = string.Empty;
    }

    public class IcmTeam
    {
        public int? IcmServiceId { get; set; }
        public string IcmServiceName { get; set; } = string.Empty;
        public string IcmTeamName { get; set; } = string.Empty;
        public int? IcmTeamId { get; set; }
        public string TeamPublicId { get; set; } = string.Empty;
    }

    public class IcmService
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class IcmTeams
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public int? ServiceId;
        public List<Team> Teams { get; set; } = new List<Team>();

        public class Team
        {
            public int? Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string PublicId { get; set; } = string.Empty;
        }

        [JsonPropertyName("_ts")]
        [JsonProperty("_ts")]
        public int Timestamp { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTimeOffset Datetime => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
    }

    public class AgentDeployment
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int TeamId { get; set; }
        public string SubscriptionId { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    public class TeamConfig
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
    }

    public class IcmIncidentBasicInfo
    {
        public string Title { get; set; } = string.Empty;
        public int Severity { get; set; } 
        public string State { get; set; } = string.Empty;
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class AgentFactoryConfigCosmos<T>
    {
        [JsonPropertyName("id")]
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        public T? Content { get; set; }

        [JsonPropertyName("_ts")]
        [JsonProperty("_ts")]
        public int Timestamp { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public DateTimeOffset Datetime => DateTimeOffset.FromUnixTimeSeconds(Timestamp);
    }
    
    public class AgentFactoryConfigIds
    {
        public const string IcmTeams = "icmTeams";
        public const string TeamFilters = "teamFilters";
        public const string DefaultIcmTeam = "defaultIcmTeam";
        public const string IcmServices = "icmServices";
    }
}
