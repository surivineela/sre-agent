// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.Tools;
using Agent.Framework;
using Agent.Plugins.Tools;

namespace Agent.Plugins.Link.Tools
{
    // [ToolTypeAttribute("LinkTool")] //  LinkTool is not supported
    public class LinkToolType : IYamlToolAware
    {
        private LinkToolDefinition? _definition;

        public LinkToolType(

            )
        {
        }

        public void SetToolDefinition(YamlToolDefinitionBase definition)
        {
            _definition = (LinkToolDefinition)definition;
        }

        public async Task<string> Run(Dictionary<string, string> args)
        {
            if (_definition == null)
                throw new InvalidOperationException("Tool definition was not set.");

            if (string.IsNullOrWhiteSpace(_definition.Template))
                throw new ArgumentException("Template is not defined in the LinkToolDefinition.");

            string result = _definition.Template;

            // Find all placeholders in the format {{key}}
            var matches = System.Text.RegularExpressions.Regex.Matches(result, @"{{(.*?)}}");

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string placeholder = match.Groups[0].Value;   // e.g. {{fromDate}}
                string key = match.Groups[1].Value;           // e.g. fromDate

                if (!args.TryGetValue(key, out var rawValue))
                {
                    throw new ArgumentException($"Missing required argument: '{key}' for placeholder '{placeholder}'");
                }

                string encodedValue = Uri.EscapeDataString(rawValue.Trim());
                result = result.Replace(placeholder, encodedValue);
            }

            return await Task.FromResult(result);
        }
    }
}
