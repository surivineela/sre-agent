// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Attributes;
using Azure.ResourceManager.Network;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions;
public class NSGRulePluginDefinition
{
    private readonly INSGRulePlugin _nsgRulePlugin;
    public NSGRulePluginDefinition(INSGRulePlugin nsgRulePlugin)
    {
        _nsgRulePlugin = nsgRulePlugin;
    }

    [KernelFunction("get_nsg_rules")]
    [Description("Retrieves the rules for a given NSG, both security and default security rules. Use this to understand the current network access permissions and identify any potential issues. Note: DefaultSecurityRules can only be updated/removed by the network administrator and they override rules configured in SecurityRules.")]
    public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNSGRules(
        [Description("Azure resource ID of the NSG resource")] string nsgResourceId)
    {
        return await _nsgRulePlugin.GetNSGRulesAsync(nsgResourceId);
    }

    [RequiresApproval]
    [KernelFunction("create_or_update_nsg_rule")]
    [Description("Creates a new NSG rule or updates an existing one to modify network access permissions. Use this to fix connectivity issues by allowing necessary traffic or blocking unwanted traffic.")]
    public async Task<bool> CreateOrUpdateNSGRuleAsync(
        [Description("Azure resource ID of the NSG to update")] string nsgResourceId,
        [Description("The security rule data object containing all rule configuration")] SecurityRuleData rule)
    {
        return await _nsgRulePlugin.CreateOrUpdateNSGRuleAsync(nsgResourceId, rule);
    }

    [RequiresApproval]
    [KernelFunction("remove_nsg_rule")]
    [Description("Removes an existing NSG rule. Use this to eliminate overly restrictive or unnecessary security rules.")]
    public async Task<bool> RemoveNSGRuleAsync(
        [Description("Azure resource ID of the NSG containing the rule")] string nsgResourceId,
        [Description("Name of the security rule to remove")] string ruleName)
    {
        return await _nsgRulePlugin.RemoveNSGRuleAsync(nsgResourceId, ruleName);
    }
}
