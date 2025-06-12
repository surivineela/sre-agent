using Azure.ResourceManager.Network;

namespace Agent.Plugins.Interface;
public interface INSGRulePlugin
{
    Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNSGRulesAsync(string nsgResourceId);

    Task<bool> CreateOrUpdateNSGRuleAsync(string nsgResourceId, SecurityRuleData rule);

    Task<bool> RemoveNSGRuleAsync(string nsgResourceId, string ruleName);
}
