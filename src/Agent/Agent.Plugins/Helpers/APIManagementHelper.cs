using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Helpers;

public class APIManagementHelper
{
    public static class Constants
    {
        public const string ActivityLogApiVer = "2015-04-01";
        public const string AppInsightsApiVer = "2018-05-01-preview";
        public const string MetricsInsightsApiVer = "2024-02-01";
        public const string LoggersApiVer = "2020-06-01-preview";
        public const string VirtualNetworkAPIVer = "2024-05-01";
        public const string APIMAPIVersion = "2024-06-01-preview";
        public const string ManagementAzureBaseUrl = "https://management.azure.com";

        public const string UnknownRuleName = "Unnamed Rule";
        public const string NSGWriteAction = "networkSecurityGroups/write";
        public const string SecurityRuleAction = "securityRules";
        public const string SecurityRuleActionTitle = "Security Rule";
        public const string NRMSRulePrefix = "NRMS";

        public const double SpikeMultiplier = 2.0; // Defining a spike as 2x the average
        public const double CPUThreshold = 80.0; // Defining the CPU Threshold as 80.0

        public const string AppInsightsTimeInterval = "PT30M"; // 30-minute granularity
        public const string LatencyAggregations = "Average,Maximum";
        public const string CPUAggregations = "Average,Maximum";
    }

    public class NSGRuleDetails
    {
        public string NSGId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Access { get; set; } = "";
        public string Protocol { get; set; } = "";
        public int Priority { get; set; }
        public string Source { get; set; } = "";
        public string Destination { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public class VirtualNetworkDetails
    {
        public string Name { get; set; } = "";
        public string Location { get; set; } = "";
        public List<string> AddressPrefixes { get; set; } = new();
        public List<string> DnsServers { get; set; } = new();
        public List<VirtualSubnetDetails> Subnets { get; set; } = new();
    }

    public class VirtualSubnetDetails
    {
        public string Name { get; set; } = "";
        public string AddressPrefix { get; set; } = "";
        public string? NetworkSecurityGroupId { get; set; }
        public string? PrivateEndpointPolicies { get; set; }
        public List<string> ServiceEndpoints { get; set; } = new();
    }

    public readonly struct SubnetResourceInfo
    {
        public string Value { get; }
        public string SubscriptionId { get; }
        public string ResourceGroupName { get; }
        public string VnetName { get; }
        public string SubnetName { get; }

        public SubnetResourceInfo(string subnetResourceId)
        {
            if (string.IsNullOrWhiteSpace(subnetResourceId))
                throw new ArgumentException("SubnetResourceId cannot be null or empty.", nameof(subnetResourceId));

            // SubnetResourceId format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}/subnets/{subnetName}
            var match = System.Text.RegularExpressions.Regex.Match(
                subnetResourceId,
                @"^/subscriptions/(?<subscriptionId>[^/]+)/resourceGroups/(?<resourceGroupName>[^/]+)/providers/Microsoft\.Network/virtualNetworks/(?<vnetName>[^/]+)/subnets/(?<subnetName>[^/]+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
                throw new FormatException($"Invalid subnet ID format: {subnetResourceId}");

            Value = subnetResourceId;
            SubscriptionId = match.Groups["subscriptionId"].Value;
            ResourceGroupName = match.Groups["resourceGroupName"].Value;
            VnetName = match.Groups["vnetName"].Value;
            SubnetName = match.Groups["subnetName"].Value;
        }

        public override string ToString() => Value;

        public static implicit operator string(SubnetResourceInfo id) => id.Value;
        public static implicit operator SubnetResourceInfo(string subnetResourceId) => new SubnetResourceInfo(subnetResourceId);
    }

    public class LatencyMetricsData
    {
        public List<LatencyDataPoint> LatencyPoints { get; set; } = new();
        public List<LatencyDataPoint> SpikePoints { get; set; } = new();
        public double OverallAvg { get; set; }
        public double OverallMax { get; set; }
        public int SpikeCount { get; set; }

        public bool HasData => LatencyPoints.Any();
    }

    public class LatencyDataPoint
    {
        public DateTime Time { get; set; }
        public double? Avg { get; set; }
        public double? Max { get; set; }

        public LatencyDataPoint(DateTime time, double? avg, double? max)
        {
            Time = time;
            Avg = avg;
            Max = max;
        }
    }
}
