using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Azure;
using Azure.Core;
using Azure.ResourceManager.Network;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;
public class NSGRulePlugin : INSGRulePlugin
{
    private readonly ILogger<NSGRulePlugin> _logger;
    private readonly IArmClientFactory _armClientFactory;
    public NSGRulePlugin(ILogger<NSGRulePlugin> logger, IArmClientFactory armClientFactory)
    {
        _logger = logger;
        _armClientFactory = armClientFactory;
    }

    public async Task<IDictionary<string, IReadOnlyList<SecurityRuleData>>> GetNSGRulesAsync(string nsgResourceId)
    {
        if (string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("Resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        _logger.LogInformation($"[{nameof(GetNSGRulesAsync)}] Invoked for NSG: {nsgResourceId}");
        var result = new Dictionary<string, IReadOnlyList<SecurityRuleData>>()
        {
            { "DefaultSecurityRules", Array.Empty<SecurityRuleData>()},
            { "SecurityRules", Array.Empty<SecurityRuleData>()}
        };

        try
        {
            var armClient = _armClientFactory.GetArmClient();

            // Get the NSG resource
            var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgResourceId));

            
            try
            {
                // Check if the NSG exists and get its data
                var nsgData = await nsgResource.GetAsync();
                _logger.LogInformation($"Found NSG {nsgResourceId} with {nsgData.Value.Data.SecurityRules.Count} security rules and {nsgData.Value.Data.DefaultSecurityRules.Count} default security rules");

                result["DefaultSecurityRules"] = nsgData.Value.Data.DefaultSecurityRules.ToList();
                result["SecurityRules"] = nsgData.Value.Data.SecurityRules.ToList();

                return result;

            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogWarning($"NSG resource with ID {nsgResourceId} not found.");
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in {nameof(GetNSGRulesAsync)} with resourceId {nsgResourceId}");
            return result;
        }
    }

    public async Task<bool> CreateOrUpdateNSGRuleAsync(string nsgResourceId, SecurityRuleData rule)
    {
        if(string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("NSG resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule), "Security rule cannot be null.");
        }

        if(string.IsNullOrWhiteSpace(rule.Name))
        {
            throw new ArgumentException("Rule name cannot be null or empty.", nameof(rule.Name));
        }
        
        _logger.LogInformation($"[{nameof(CreateOrUpdateNSGRuleAsync)}] Invoked for rule '{rule.Name}' on NSG: {nsgResourceId}");

        try
        {
            var armClient = _armClientFactory.GetArmClient();

            // Get the NSG resource
            var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgResourceId));

            // Check if the NSG exists
            await nsgResource.GetAsync();

            // Get the security rules collection and create/update the rule
            SecurityRuleCollection securityRules = nsgResource.GetSecurityRules();

            string operationType = "update";

            try
            {
                // Check if the rule exists
                await securityRules.GetAsync(rule.Name);
                _logger.LogInformation($"Updating existing security rule '{rule.Name}' in NSG {nsgResourceId}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation($"Security rule '{rule.Name}' not found in NSG {nsgResourceId}, creating new rule");
                operationType = "create";
            }

            // CreateOrUpdate handles both creating a new rule and updating an existing one
            await securityRules.CreateOrUpdateAsync(WaitUntil.Completed, rule.Name, rule);
            _logger.LogInformation($"Successfully {operationType}d security rule '{rule.Name}' in NSG {nsgResourceId}");

            return true;
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, $"Error in {nameof(CreateOrUpdateNSGRuleAsync)} with nsgResourceId {nsgResourceId}, rule {rule.Name}");
            return false;
        }
    }

    public async Task<bool> RemoveNSGRuleAsync(string nsgResourceId, string ruleName)
    {
        if (string.IsNullOrWhiteSpace(nsgResourceId))
        {
            throw new ArgumentException("NSG resource ID cannot be null or empty.", nameof(nsgResourceId));
        }

        if (string.IsNullOrWhiteSpace(ruleName))
        {
            throw new ArgumentException("Rule name cannot be null or empty.", nameof(ruleName));
        }

        _logger.LogInformation($"[{nameof(RemoveNSGRuleAsync)}] Invoked for rule '{ruleName}' on NSG: {nsgResourceId}");

        try
        {
            var armClient = _armClientFactory.GetArmClient();

            // Get the NSG resource
            var nsgResource = armClient.GetNetworkSecurityGroupResource(new ResourceIdentifier(nsgResourceId));

            // Check if the NSG exists
            await nsgResource.GetAsync();

            // Get the security rules collection
            SecurityRuleCollection securityRules = nsgResource.GetSecurityRules();

            try
            {
                // Check if the rule exists
                var existingRule = await securityRules.GetAsync(ruleName);

                // Delete the rule
                _logger.LogInformation($"Removing security rule '{ruleName}' from NSG {nsgResourceId}");
                
                var armOperation = await existingRule.Value.DeleteAsync(WaitUntil.Completed);
                
                _logger.LogInformation(armOperation.HasCompleted
                    ? $"Successfully removed security rule '{ruleName}' from NSG {nsgResourceId}"
                    : $"Failed to remove security rule '{ruleName}' from NSG {nsgResourceId}"
                    );
                
                return armOperation.HasCompleted;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Rule doesn't exist, nothing to remove
                _logger.LogInformation($"Security rule '{ruleName}' not found in NSG {nsgResourceId}, nothing to remove");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in {nameof(RemoveNSGRuleAsync)} with nsgResourceId {nsgResourceId}, rule {ruleName}");
            return false;
        }
    }
}
