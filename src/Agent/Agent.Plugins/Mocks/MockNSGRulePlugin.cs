using Agent.Plugins.Definitions;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;

namespace Agent.Plugins.Mocks;
public class MockNSGRulePlugin : INSGRulePlugin
{
    public Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNSGRulesAsync(string nsgResourceId)
    {
        if (string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("NSG resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        var rules = new List<SecurityRuleData>
        {
            new SecurityRuleData
            {
                Name = "AllowInboundHttp",
                Description = "Allow inbound HTTP traffic",
                Protocol = SecurityRuleProtocol.Tcp,
                SourcePortRanges = { "*" },
                DestinationPortRanges = { "80" },
                SourceAddressPrefixes = { "*" },
                DestinationAddressPrefixes = { "*" },
                Access = SecurityRuleAccess.Allow,
                Priority = 100,
                Direction = SecurityRuleDirection.Inbound
            },
            new SecurityRuleData
            {
                Name = "AllowOutboundHttp",
                Description = "Allow aoutbound HTTP traffic",
                Protocol = SecurityRuleProtocol.Tcp,
                SourcePortRanges = { "*" },
                DestinationPortRanges = { "80" },
                SourceAddressPrefixes = { "*" },
                DestinationAddressPrefixes = { "*" },
                Access = SecurityRuleAccess.Allow,
                Priority = 100,
                Direction = SecurityRuleDirection.Outbound
            }
        };
        var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>
        {
            { "DefaultSecurityRules", rules },
            { "SecurityRules", rules }
        };
        return Task.FromResult<IDictionary<string, IReadOnlyList<SecurityRuleData>>>(result);
    }

    public Task<bool> CreateOrUpdateNSGRuleAsync(string nsgResourceId, SecurityRuleData rule)
    {
        if(string.IsNullOrWhiteSpace(nsgResourceId))
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

        return rule.Name.Contains("Fail", StringComparison.OrdinalIgnoreCase) ? Task.FromResult(false)
            : Task.FromResult(true);
    }

    public Task<bool> RemoveNSGRuleAsync(string nsgResourceId, string ruleName)
    {
        if(string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("NSG resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        if (string.IsNullOrEmpty(ruleName))
        {
            throw new ArgumentException("Rule name cannot be null or empty.", nameof(ruleName));
        }

        return ruleName.Contains("Fail", StringComparison.OrdinalIgnoreCase) ? Task.FromResult(false)
            : Task.FromResult(true);
    }
}
