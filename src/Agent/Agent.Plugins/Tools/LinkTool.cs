// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Data.Tools;
using Agent.Framework;

namespace Agent.Plugins.Tools;

[ToolType("LinkTool")]
public partial class LinkToolType : IYamlToolAware
{
    private LinkToolDefinition? _definition;

    public LinkToolType()
    {
    }

    public void SetToolDefinition(YamlToolDefinitionBase definition)
    {
        _definition = (LinkToolDefinition)definition;
    }

    public async Task<string> Run(Dictionary<string, string> args, Guid? threadId = null)
    {
        if (_definition == null)
        {
            throw new InvalidOperationException("Tool definition was not set.");
        }

        if (string.IsNullOrWhiteSpace(_definition.Template))
        {
            throw new ArgumentException("Template is not defined in the LinkToolDefinition.");
        }

        var result = _definition.Template;

        // Find all placeholders in the format {{key}}
        var matches = PlaceholderRegex().Matches(result);

        foreach (Match match in matches)
        {
            var placeholder = match.Groups[0].Value;   // e.g. {{fromDate}} or {{threadId}} or {{agent_endpoint}} or {{agent_name}}
            var key = match.Groups[1].Value;           // e.g. fromDate or threadId or agent_endpoint or agent_name
            string? valueToReplace = null;

            // Special handling for threadId placeholder
            if (key.Equals("threadId", StringComparison.OrdinalIgnoreCase))
            {
                valueToReplace = threadId?.ToString();
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
            else if (args.TryGetValue(key, out var rawValue))
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
