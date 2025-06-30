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
        public const string LoggersApiVer = "2020-06-01-preview";
        public const string VirtualNetworkAPIVer = "2024-05-01";
        public const string APIMAPIVersion = "2024-06-01-preview";
        public const string ManagementAzureBaseUrl = "https://management.azure.com";

        public const string UnknownRuleName = "Unnamed Rule";
        public const string NSGWriteAction = "networkSecurityGroups/write";
        public const string SecurityRuleAction = "securityRules";
        public const string SecurityRuleActionTitle = "Security Rule";
        public const string NRMSRulePrefix = "NRMS";

        public const int SubnetResourceIdSegmentCount = 8;
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

        public SubnetResourceInfo(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("SubnetResourceId cannot be null or empty.", nameof(value));

            var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < Constants.SubnetResourceIdSegmentCount || !parts.Contains("virtualNetworks") || !parts.Contains("subnets"))
                throw new FormatException($"Invalid subnet ID format: {value}");

        // SubnetResourceId format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/virtualNetworks/{vnetName}/subnets/{subnetName}
        Value = value;
        SubscriptionId = parts[1];
        ResourceGroupName = parts[3];
        VnetName = parts[7];
        SubnetName = parts[9];
    }

        public override string ToString() => Value;

        public static implicit operator string(SubnetResourceInfo id) => id.Value;
        public static implicit operator SubnetResourceInfo(string value) => new SubnetResourceInfo(value);
    }
}
