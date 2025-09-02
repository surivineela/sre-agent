// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Agent.Core.Models.ICM
{
    public class Incident
    {
        public required string IncidentId { get; set; }
        public required IncidentType IncidentType { get; set; }
        public required string CloudInstance { get; set; }
        public required string Slice { get; set; }
        public required int HitCount { get; set; }
        public required string ParentIncidentId { get; set; } = string.Empty;
        public required string Environment { get; set; } = string.Empty;
        public required string CreatedBy { get; set; }
        public required DateTime ImpactStartDate { get; set; }
        public required DateTime CreatedDate { get; set; }
        public DateTime? MitigatedDate { get; set; }
        public MitigationData? MitigateData { get; set; }
        public ResolutionData? ResolveData { get; set; }
        public required DateTime LastModifiedDate { get; set; }
        public required IncidentStatus Status { get; set; }
        public required string OwningService { get; set; }
        public required string OwningServiceId { get; set; }
        public required string OwningTeam { get; set; }
        public required string OwningTeamName { get; set; }
        public required string Owner { get; set; }
        public required string Severity { get; set; }
        public required string Title { get; set; }
        public required string Keywords { get; set; }
        public required string Summary { get; set; }
        public required string DiscussionEntry { get; set; }
        public required string MonitoringRole { get; set; }
        public string? MonitorId { get; set; }
        public required string MonitoringSlice { get; set; }
        public required string SubscriptionId { get; set; }
        public required string[] Tags { get; set; } = Array.Empty<string>();
        public string? Stamp { get; set; }
        public string? Datacenter { get; set; }
    }

    public class MitigationData
    {
        public required string MitigationSteps { get; set; }
        public required string MitigatedBy { get; set; }
        public DateTime? MitigateTime { get; set; }
    }

    public class ResolutionData
    {
        public required string ResolvedBy { get; set; }
        public DateTime? ResolveTime { get; set; }
    }

    public class DiscussionEntry
    {
        public required string IncidentId { get; set; }
        public required DateTime Date { get; set; }
        public required string ChangedBy { get; set; }
        public required string Text { get; set; }
        public required bool IsHtml { get; set; }
        public string? Cause { get; set; }
    }

    public class CustomField
    {
        public required string CustomFieldName { get; set; }
        public required string CustomFieldValue { get; set; }
    }

    public class SearchItem
    {
        public required string Id { get; set; }
        public required string Title { get; set; }
        public required string ResponsibleServiceName { get; set; }
        public required DateTime CreatedDate { get; set; }
        public DateTime? MitigatedDate { get; set; }
        public DateTime? ResolvedDate { get; set; }
        public required string HowFixed { get; set; }
        public required string State { get; set; }
    }

    public class IncidentAdvancedSearchResultItem : SearchItem
    {
        public required string CorrelationId { get; set; }
        public required string OccurringEnvironment { get; set; }
        public required string OccurringDatacenter { get; set; }
        public required string OccurringDeviceGroup { get; set; }
        public required string OccurringDeviceName { get; set; }
        public required string OccurringServiceInstanceId { get; set; }
        public required string IncidentType { get; set; }
        public required string Keywords { get; set; }
        public required DateTime ModifiedDate { get; set; }
        public required string OwningTeamId { get; set; }
        public required string OwningTenantId { get; set; }
        public required string OwningContactAlias { get; set; }
        public required string ParentIncidentId { get; set; }
        public required string RoutingId { get; set; }
        public required string Severity { get; set; }
        public required Guid SourceIncidentId { get; set; }
        public required string SourceId { get; set; }
    }


    public class ODataResponse<T>
    {
        [JsonProperty("odata.metadata")]
        public required string OdataMetadata { get; set; }

        [JsonProperty("value")]
        public required List<T> Value { get; set; }
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
                var id = ExternalLinkEntityRef.Id;
                var lastOpenBracket = id.LastIndexOf('[');
                var lastCloseBracket = id.LastIndexOf(']');

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

        public required RepairItemEntityReference ExternalLinkEntityRef { get; set; }
        public required RepairItemEntityReference IcmEntityRef { get; set; }
        public required string Title { get; set; }
        public required string Owner { get; set; }
        public required string Status { get; set; }
        public required DateTime ChangedDate { get; set; }
        public required string AdditionalData { get; set; }
        public required int ExternalLinkConfigId { get; set; }
        public required object KeepRemoteEntityUntouched { get; set; }
        public DateTime? CreatedDate { get; set; }
        public required int ExternalLinkTypeId { get; set; }
        public required string CustomTags { get; set; }

        private RepairItemAdditionalData? _parsedAdditionalData =  null;

        public RepairItemAdditionalData? ParsedAdditionalData =>
            _parsedAdditionalData ??=
            !string.IsNullOrEmpty(AdditionalData)
                ? JsonConvert.DeserializeObject<RepairItemAdditionalData>(AdditionalData)
                : null;
    }

    public class RepairItemEntityReference
    {
        public required string IdType { get; set; }
        public required string Id { get; set; }
        public required string DisplayName { get; set; }
        public required string EntityTypeId { get; set; }
        public required string EntityTypeName { get; set; }
    }

    public class RepairItemAdditionalData
    {
        public required RepairItemType RepairItemType { get; set; }
        public required int RepairItemDeliveryType { get; set; } // Using int because values in data don't match enum (101, 102, 103)
        public required string WorkItemType { get; set; }
        public required string Source { get; set; }
        public required int Revision { get; set; }
        public required string Areapath { get; set; }
        public required string ProjectName { get; set; }
        public required string Tags { get; set; }
        public required DateTime VSTSCreatedDate { get; set; }
        public DateTime? VSTSClosedDate { get; set; }
        public required string RepairItemOwningServiceId { get; set; }
        public required string RepairItemOwningTeamId { get; set; }
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

