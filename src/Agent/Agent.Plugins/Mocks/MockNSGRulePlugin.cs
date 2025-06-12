using Agent.Plugins.Interface;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;

namespace Agent.Plugins.Mocks;

public class MockNSGRulePlugin : INSGRulePlugin
{
    private readonly Dictionary<string, Dictionary<string, IList<SecurityRuleData>>> _securityRules = [];

    public void ConfigureNSG(string resourceId, IList<SecurityRuleData> securityRules, IList<SecurityRuleData> defaultSecurityRules)
    {
        _securityRules[resourceId] = new Dictionary<string, IList<SecurityRuleData>>
        {
            { "SecurityRules", securityRules },
            { "DefaultSecurityRules", defaultSecurityRules }
        };
    }

    public void ConfigureNSGDefaults(string resourceId)
    {
        var securityRules = new List<SecurityRuleData>
            {
                ArmNetworkModelFactory.SecurityRuleData(
                    description: "Allow VNET HTTPS",
                    name: "AllowHTTPS",
                    protocol: "Tcp",
                    sourcePortRange: "*",
                    destinationPortRange: "443",
                    sourceAddressPrefix: "VirtualNetwork",
                    destinationAddressPrefix: "*",
                    access: SecurityRuleAccess.Allow,
                    priority: 102,
                    direction: SecurityRuleDirection.Inbound
                )
            };

        var defaultSecurityRules = new List<SecurityRuleData>
            {
                ArmNetworkModelFactory.SecurityRuleData(
                    description: "Allow VNET Inbound",
                    name: "AllowVNETInbound",
                    protocol: "*",
                    sourcePortRange: "*",
                    destinationPortRange: "*",
                    sourceAddressPrefix: "VirtualNetwork",
                    destinationAddressPrefix: "VirtualNetwork",
                    access: SecurityRuleAccess.Allow,
                    priority: 65000,
                    direction: SecurityRuleDirection.Inbound
                ),
                ArmNetworkModelFactory.SecurityRuleData(
                    description: "Deny all inbound traffic",
                    name: "DenyAllInbound",
                    protocol: "*",
                    sourcePortRange: "*",
                    destinationPortRange: "*",
                    sourceAddressPrefix: "*",
                    destinationAddressPrefix: "*",
                    access: SecurityRuleAccess.Deny,
                    priority: 65500,
                    direction: SecurityRuleDirection.Inbound
                ),
                ArmNetworkModelFactory.SecurityRuleData(
                    description: "Allow VNET outbound traffic",
                    name: "AllowVnetOutbound",
                    protocol: "*",
                    sourcePortRange: "*",
                    destinationPortRange: "*",
                    sourceAddressPrefix: "VirtualNetwork",
                    destinationAddressPrefix: "VirtualNetwork",
                    access: SecurityRuleAccess.Allow,
                    priority: 65000,
                    direction: SecurityRuleDirection.Outbound
                ),
                ArmNetworkModelFactory.SecurityRuleData(
                    description: "Allow internet outbound traffic",
                    name: "AllowInternetOutbound",
                    protocol: "*",
                    sourcePortRange: "*",
                    destinationPortRange: "*",
                    sourceAddressPrefix: "*",
                    destinationAddressPrefix: "Internet",
                    access: SecurityRuleAccess.Allow,
                    priority: 65001,
                    direction: SecurityRuleDirection.Outbound
                ),
                ArmNetworkModelFactory.SecurityRuleData(
                    description: "Deny all outbound traffic",
                    name: "DenyAllOutbound",
                    protocol: "*",
                    sourcePortRange: "*",
                    destinationPortRange: "*",
                    sourceAddressPrefix: "*",
                    destinationAddressPrefix: "*",
                    access: SecurityRuleAccess.Deny,
                    priority: 65500,
                    direction: SecurityRuleDirection.Outbound
                )
            };

        ConfigureNSG(resourceId, securityRules, defaultSecurityRules);
    }

    public Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNSGRulesAsync(string nsgResourceId)
    {
        if (string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("NSG resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        if (!_securityRules.ContainsKey(nsgResourceId))
        {
            throw new ArgumentException($"NSG resource ID '{nsgResourceId}' not found.", nameof(nsgResourceId));
        }

        var rules = _securityRules[nsgResourceId];

        var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>
        {
            { "SecurityRules", rules["SecurityRules"].AsReadOnly() },
            { "DefaultSecurityRules", rules["DefaultSecurityRules"].AsReadOnly() }
        };

        return Task.FromResult<IDictionary<string, IReadOnlyList<SecurityRuleData>>>(result);
    }

    public Task<bool> CreateOrUpdateNSGRuleAsync(string nsgResourceId, SecurityRuleData rule)
    {
        if (string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("NSG resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule), "Security rule cannot be null.");
        }

        if (string.IsNullOrEmpty(rule.Name))
        {
            throw new ArgumentException("Rule name cannot be null or empty.", nameof(rule.Name));
        }

        if (!_securityRules.ContainsKey(nsgResourceId))
        {
            throw new ArgumentException($"NSG resource ID '{nsgResourceId}' not found.", nameof(nsgResourceId));
        }

        var rules = _securityRules[nsgResourceId]["SecurityRules"];
        var existingRule = rules.FirstOrDefault(r => r.Name.Equals(rule.Name, StringComparison.OrdinalIgnoreCase));
        if (existingRule != null)
        {
            rules.Remove(existingRule);
        }
        rules.Add(rule);

        return Task.FromResult(true);
    }

    public Task<bool> RemoveNSGRuleAsync(string nsgResourceId, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("NSG resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        if (string.IsNullOrEmpty(ruleName))
        {
            throw new ArgumentException("Rule name cannot be null or empty.", nameof(ruleName));
        }

        if (!_securityRules.ContainsKey(nsgResourceId))
        {
            throw new ArgumentException($"NSG resource ID '{nsgResourceId}' not found.", nameof(nsgResourceId));
        }

        var rules = _securityRules[nsgResourceId]["SecurityRules"];
        var existingRule = rules.FirstOrDefault(r => r.Name.Equals(ruleName, StringComparison.OrdinalIgnoreCase));
        if (existingRule != null)
        {
            rules.Remove(existingRule);
            return Task.FromResult(true);
        }
        else
        {
            throw new ArgumentException($"Rule '{ruleName}' not found in NSG resource ID '{nsgResourceId}'.", nameof(ruleName));
        }
    }
}
