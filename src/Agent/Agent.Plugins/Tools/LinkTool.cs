// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Tools;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Tools;

/// <summary>
/// Factory for creating LinkTool instances.
/// </summary>
[ToolType("LinkTool")]
public class LinkToolExecutorFactory : IYamlToolExecutorFactory
{
    public IYamlToolExecutor Create(YamlToolDefinitionBase definition, IServiceProvider serviceProvider)
    {
        var linkDefinition = (LinkToolDefinition)definition;
        return new LinkTool(linkDefinition);
    }
}

/// <summary>
/// Link tool implementation that extends YamlToolExecutor.
/// Uses factory pattern for instantiation and provides clean ExecuteAsync interface.
/// </summary>
public partial class LinkTool : YamlToolExecutor<LinkToolDefinition>
{
    public LinkTool(LinkToolDefinition definition) : base(definition)
    {
    }

    public override async Task<object?> ExecuteAsync(string threadId, AIFunctionArguments parameters)
    {
        if (string.IsNullOrWhiteSpace(ToolDefinition.Template))
        {
            throw new ArgumentException("Template is not defined in the LinkToolDefinition.");
        }

        // Convert to dictionary for easier lookup (base class helper filters out internal params)
        var paramsDict = ConvertToStringDictionary(parameters);

        var result = ToolDefinition.Template;

        // Find all placeholders in the format {{key}}
        var matches = PlaceholderRegex().Matches(result);

        foreach (Match match in matches)
        {
            var placeholder = match.Groups[0].Value;
            var key = match.Groups[1].Value;
            string? valueToReplace = null;

            // Special handling for threadId placeholder
            if (key.Equals("threadId", StringComparison.OrdinalIgnoreCase))
            {
                valueToReplace = threadId;
            }
            // Special handling for agent_endpoint placeholder
            else if (key.Equals("agent_endpoint", StringComparison.OrdinalIgnoreCase))
            {
                valueToReplace = Environment.GetEnvironmentVariable("AGENT_ENDPOINT");
            }
            // Special handling for agent_name placeholder
            else if (key.Equals("agent_name", StringComparison.OrdinalIgnoreCase))
            {
                valueToReplace = Environment.GetEnvironmentVariable("AGENT_NAME");
            }
            // Regular argument handling
            else if (paramsDict.TryGetValue(key, out var rawValue))
            {
                valueToReplace = rawValue.Trim();
            }
            else
            {
                throw new ArgumentException($"Missing required argument: '{key}' for placeholder '{placeholder}'");
            }

            // Replace placeholder with URL-encoded value if we have a value
            if (valueToReplace != null)
            {
                var encodedValue = Uri.EscapeDataString(valueToReplace);
                result = result.Replace(placeholder, encodedValue);
            }
        }

        return await Task.FromResult(result);
    }

    [GeneratedRegex(@"{{(.*?)}}")]
    private static partial Regex PlaceholderRegex();
}
