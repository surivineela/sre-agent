// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FirstPartyAgent.Models
{
    public class Incident
    {
        public string IncidentId { get; set; } = string.Empty;
        public IncidentType IncidentType { get; set; }
        public string CloudInstance { get; set; } = string.Empty;
        public string Slice { get; set; } = string.Empty;
        public int HitCount { get; set; }
        public string ParentIncidentId { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime ImpactStartDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public IncidentStatus Status { get; set; }
        public string OwningService { get; set; } = string.Empty;
        public string OwningServiceId { get; set; } = string.Empty;
        public string OwningTeam { get; set; } = string.Empty;
        public string OwningTeamName { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string DiscussionEntry { get; set; } = string.Empty;
        public string MonitoringRole { get; set; } = string.Empty;
        public string MonitoringSlice { get; set; } = string.Empty;
        public string SubscriptionId { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string? MonitorId { get; set; }
        public string Stamp { get; set; } = string.Empty;
        public string Datacenter { get; set; } = string.Empty;
    }

    public class DiscussionEntry
    {
        public string IncidentId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string ChangedBy { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsHtml { get; set; }
        public string? Cause { get; set; }
    }

    public class CustomField
    {
        public string CustomFieldName { get; set; } = string.Empty;
        public string CustomFieldValue { get; set; } = string.Empty;
    }

    public class SearchItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ResponsibleServiceName { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? MitigatedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string HowFixed { get; set; } = string.Empty;
        public string State { get; set; }  = string.Empty;
    }

    public class IncidentAdvancedSearchResultItem: SearchItem
    {
        public string CorrelationId { get; set; } = string.Empty;
        public string OccurringEnvironment { get; set; } = string.Empty;
        public string OccurringDatacenter { get; set; } = string.Empty;
        public string OccurringDeviceGroup { get; set; } = string.Empty;
        public string OccurringDeviceName { get; set; } = string.Empty;
        public string OccurringServiceInstanceId { get; set; } = string.Empty;
        public string IncidentType { get; set; } = string.Empty;
        public string Keywords { get; set; } = string.Empty;
        public DateTime ModifiedDate { get; set; }
        public string OwningTeamId { get; set; } = string.Empty;
        public string OwningTenantId { get; set; } = string.Empty;
        public string OwningContactAlias { get; set; } = string.Empty;
        public string ParentIncidentId { get; set; } = string.Empty;
        public string RoutingId { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public Guid SourceIncidentId { get; set; } = Guid.Empty;
        public string SourceId { get; set; } = string.Empty;
    }


    public class ODataResponse<T>
    {
        [JsonProperty("odata.metadata")]
        public string OdataMetadata { get; set; } = string.Empty;

        [JsonProperty("value")]
        public List<T> Value { get; set; } = [];
    }

    public class IncidentRepairItem
    {
        public int Id { get; set; }

        public string RepairItemId
        {
            get
            {
                if (string.IsNullOrEmpty(ExternalLinkEntityRef?.Id))
                {
                    // Todo: Handle the case where repair item id is to be taken from IcmEntityRef.Id
                    // For now, return empty string to avoid null reference exceptions as I am yet to find such a scenario
                    return string.Empty;
                }

                // The format is like "[]_[guid]_[guid]_[12345]" - extract the last part in brackets
                string id = ExternalLinkEntityRef.Id;
                int lastOpenBracket = id.LastIndexOf('[');
                int lastCloseBracket = id.LastIndexOf(']');

                if (lastOpenBracket >= 0 && lastCloseBracket > lastOpenBracket)
                {
                    return id.Substring(lastOpenBracket + 1, lastCloseBracket - lastOpenBracket - 1);
                }

                return string.Empty;
            }
        }

        public string RepairItemDeepLink
        {
            get => $"https://msazure.visualstudio.com/One/_workitems/edit/{RepairItemId}";
        }

        public RepairItemEntityReference ExternalLinkEntityRef { get; set; } = new RepairItemEntityReference();
        public RepairItemEntityReference IcmEntityRef { get; set; } = new RepairItemEntityReference();
        public string Title { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ChangedDate { get; set; }
        public string AdditionalData { get; set; } = string.Empty;
        public int ExternalLinkConfigId { get; set; }
        public object KeepRemoteEntityUntouched { get; set; } = new object();
        public DateTime? CreatedDate { get; set; }
        public int ExternalLinkTypeId { get; set; }
        public string CustomTags { get; set; } = string.Empty;

        private RepairItemAdditionalData? _parsedAdditionalData;

        public RepairItemAdditionalData? ParsedAdditionalData =>
            _parsedAdditionalData ??= 
            !string.IsNullOrEmpty(AdditionalData)
                ? JsonConvert.DeserializeObject<RepairItemAdditionalData>(AdditionalData)
                : null;
    }

    public class RepairItemEntityReference
    {
        public string IdType { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string EntityTypeId { get; set; } = string.Empty;
        public string EntityTypeName { get; set; } = string.Empty;
    }

    public class RepairItemAdditionalData
    {
        public RepairItemType RepairItemType { get; set; }
        public int RepairItemDeliveryType { get; set; } // Using int because values in data don't match enum (101, 102, 103)
        public string WorkItemType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int Revision { get; set; }
        public string Areapath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string Tags { get; set; } = string.Empty;
        public DateTime VSTSCreatedDate { get; set; } 
        public DateTime? VSTSClosedDate { get; set; }
        public string RepairItemOwningServiceId { get; set; } = string.Empty;
        public string RepairItemOwningTeamId { get; set; } = string.Empty;
        public int IncidentSeverity { get; set; }

        public RepairItemDeliveryTypeEnum DeliveryType => RepairItemDeliveryType switch
                {
                    1 or 101 => RepairItemDeliveryTypeEnum.ShortTerm,
                    2 or 102 => RepairItemDeliveryTypeEnum.LongTerm,
                    3 or 103 => RepairItemDeliveryTypeEnum.MediumTerm,
                    _ => RepairItemDeliveryTypeEnum.Invalid
                };

        public string ExpectedDeliveryDuration
        {
            get
            {
                return RepairItemDeliveryType switch
                {
                    1 or 101 => "Short Term - 2 weeks",
                    2 or 102 => "Long Term - 1 year",
                    3 or 103 => "Medium Term - 6 months",
                    _ => "Unknown"
                };
            }
        }
    }

    //Livesite/IcmTool/Core/Models/RepairItemType.cs
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RepairItemType
    {
        Invalid = 0,
        Fix = 1,
        Detection = 2,
        Mitigation = 3,
        Other = 4,
        Repair = 5,
        Diagnose = 6,
        Notification = 7,
        Engagement = 8,
        TestRelease = 9,
        Process = 10,
        Resiliency = 11
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum RepairItemDeliveryTypeEnum
    {
        Invalid = 0,
        ShortTerm = 1,
        LongTerm = 2,
        MediumTerm = 3
    }
}

