// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Data.DataModels
{
    public class AppHealthHistoryDocument : ICosmosDocument
    {
        public AppHealthHistoryDocument(
            string id,
            string appId,
            string appName,
            string resourceType)
        {
            Id = id;
            AppId = appId;
            AppName = appName;
            ResourceType = resourceType;
            HistoryData = new List<AppHealthInfoData>();
        }

        public string Id { get; set; }
        public string DocumentType => "AppHealthHistory";
        public string PartitionKey => AppId;

        public string AppId { get; set; }
        public string AppName { get; set; }
        public string ResourceType { get; set; }

        // Collection of historical data points
        public List<AppHealthInfoData> HistoryData { get; set; }

        // Last updated timestamp
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public static string ContainerName => Data.AgentDataConfiguration.ThreadContainerName;

        public class AppHealthInfoData
        {
            public DateTime LastDataCaptureTimeStampInUTC { get; set; }
            public ScorecardHealthState Health { get; set; } = ScorecardHealthState.Unknown;
            public double? Availability { get; set; }
            public double? AvgCpuUsage { get; set; }
            public double? AvgMemoryUsage { get; set; }
            public double? Transactions { get; set; }
        }
    }
}
