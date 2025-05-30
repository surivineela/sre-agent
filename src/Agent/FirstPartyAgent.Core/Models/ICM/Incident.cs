// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FirstPartyAgent.Models
{
    public class Incident
    {
        public string IncidentId { get; set; }
        public IncidentType IncidentType { get; set; }
        public string CloudInstance { get; set; }
        public string Slice { get; set; }
        public int HitCount { get; set; }
        public string ParentIncidentId { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string CreatedBy { get; set; }
        public DateTime ImpactStartDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public IncidentStatus Status { get; set; }
        public string OwningService { get; set; }
        public string OwningServiceId { get; set; }
        public string OwningTeam { get; set; }
        public string OwningTeamName { get; set; }
        public string Owner { get; set; }
        public string Severity { get; set; }
        public string Title { get; set; }
        public string Keywords { get; set; }
        public string Summary { get; set; }
        public string DiscussionEntry { get; set; }
        public string MonitoringRole { get; set; }
        public string MonitoringSlice { get; set; }
        public string SubscriptionId { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    public class DiscussionEntry
    {
        public string IncidentId { get; set; }
        public DateTime Date { get; set; }
        public string ChangedBy { get; set; }
        public string Text { get; set; }
        public bool IsHtml { get; set; }
        public string? Cause { get; set; }
    }

    public class CustomField
    {
        public string CustomFieldName { get; set; }
        public string CustomFieldValue { get; set; }
    }

    public class SearchItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ResponsibleServiceName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? MitigatedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public string HowFixed { get; set; }
        public string State { get; set; }
    }

    public class IncidentAdvancedSearchResultItem: SearchItem
    {
        public string CorrelationId { get; set; }
        public string OccurringEnvironment { get; set; }
        public string OccurringDatacenter { get; set; }
        public string OccurringDeviceGroup { get; set; }
        public string OccurringDeviceName { get; set; }
        public string OccurringServiceInstanceId { get; set; }
        public string IncidentType { get; set; }
        public string Keywords { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string OwningTeamId { get; set; }
        public string OwningTenantId { get; set; }
        public string OwningContactAlias { get; set; }
        public string ParentIncidentId { get; set; }
        public string RoutingId { get; set; }
        public string Severity { get; set; }
        public Guid SourceIncidentId { get; set; }
        public string SourceId { get; set; }
    }


    public class ODataResponse<T>
    {
        [JsonProperty("odata.metadata")]
        public string OdataMetadata { get; set; }

        [JsonProperty("value")]
        public List<T> Value { get; set; }
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

        public RepairItemEntityReference ExternalLinkEntityRef { get; set; }
        public RepairItemEntityReference IcmEntityRef { get; set; }
        public string Title { get; set; }
        public string Owner { get; set; }
        public string Status { get; set; }
        public DateTime ChangedDate { get; set; }
        public string AdditionalData { get; set; }
        public int ExternalLinkConfigId { get; set; }
        public object KeepRemoteEntityUntouched { get; set; }
        public DateTime? CreatedDate { get; set; }
        public int ExternalLinkTypeId { get; set; }
        public string CustomTags { get; set; }

        private RepairItemAdditionalData _parsedAdditionalData;

        public RepairItemAdditionalData? ParsedAdditionalData =>
            _parsedAdditionalData ??= 
            !string.IsNullOrEmpty(AdditionalData)
                ? JsonConvert.DeserializeObject<RepairItemAdditionalData>(AdditionalData)
                : null;
    }

    public class RepairItemEntityReference
    {
        public string IdType { get; set; }
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string EntityTypeId { get; set; }
        public string EntityTypeName { get; set; }
    }

    public class RepairItemAdditionalData
    {
        public RepairItemType RepairItemType { get; set; }
        public int RepairItemDeliveryType { get; set; } // Using int because values in data don't match enum (101, 102, 103)
        public string WorkItemType { get; set; }
        public string Source { get; set; }
        public int Revision { get; set; }
        public string Areapath { get; set; }
        public string ProjectName { get; set; }
        public string Tags { get; set; }
        public DateTime VSTSCreatedDate { get; set; }
        public DateTime? VSTSClosedDate { get; set; }
        public string RepairItemOwningServiceId { get; set; }
        public string RepairItemOwningTeamId { get; set; }
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

