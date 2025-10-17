// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.Tools;
using YamlDotNet.Serialization;

namespace Agent.Cli.Validations
{
    public static class ToolValidation
    {
        public static bool ValidateTool(string name, string type, out List<string> errors)
        {
            errors = [];
            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Tool name must not be empty.");
            if (name != null && name.Any(char.IsWhiteSpace))
                errors.Add("Tool name must not contain whitespace.");
            if (string.IsNullOrWhiteSpace(type))
                errors.Add("Tool type must not be empty.");
            // Add more tool-specific validation as needed
            return errors.Count == 0;
        }

        /// <summary>
        /// Validates a tool YAML content using proper C# objects
        /// </summary>
        public static bool ValidateToolYaml(string yamlContent, out List<string> errors)
        {
            errors = [];

            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var toolData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

                if (toolData == null)
                {
                    errors.Add("Invalid YAML format");
                    return false;
                }

                // Required fields validation
                if (!ValidateRequiredField(toolData, "name", errors)) return false;
                if (!ValidateRequiredField(toolData, "type", errors)) return false;
                if (!ValidateRequiredField(toolData, "description", errors)) return false;

                var toolType = toolData["type"].ToString()!.ToLowerInvariant();

                // Type-specific validation
                switch (toolType)
                {
                    case "kustotool":
                        return ValidateKustoTool(toolData, yamlContent, errors);
                    default:
                        // Generic validation for unknown types
                        break;
                }

                // Validate parameters if present
                if (toolData.TryGetValue("parameters", out var parametersObj))
                {
                    ValidateParameters(parametersObj, errors);
                }

                return errors.Count == 0;
            }
            catch (Exception ex)
            {
                errors.Add($"YAML parsing error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates required field presence and non-empty value
        /// </summary>
        private static bool ValidateRequiredField(Dictionary<string, object> data, string fieldName, List<string> errors)
        {
            if (!data.TryGetValue(fieldName, out var value) ||
                value == null ||
                string.IsNullOrWhiteSpace(value.ToString()))
            {
                errors.Add($"Field '{fieldName}' is required and cannot be empty");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Enhanced KustoTool validation with required field checking
        /// </summary>
        private static bool ValidateKustoTool(Dictionary<string, object> toolData, string yamlContent, List<string> errors)
        {
            // Required fields for KustoTool
            if (!ValidateRequiredField(toolData, "connector", errors)) return false;
            if (!ValidateRequiredField(toolData, "database", errors)) return false;
            if (!ValidateRequiredField(toolData, "query", errors)) return false;

            // Validate mode if present
            if (toolData.TryGetValue("mode", out var modeObj))
            {
                var mode = modeObj.ToString()!.ToLowerInvariant();
                if (mode != "query" && mode != "command")
                {
                    errors.Add("Mode must be either 'query' or 'command'");
                    return false;
                }
            }

            // Use existing KustoToolDefinition validation
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var kustoTool = deserializer.Deserialize<KustoToolDefinition>(yamlContent);
                kustoTool?.Validate();
                return true;
            }
            catch (ArgumentException ex)
            {
                errors.Add(ex.Message);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                errors.Add($"KustoTool validation error: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Validates tool parameters structure and content
        /// </summary>
        private static void ValidateParameters(object parametersObj, List<string> errors)
        {
            if (parametersObj is not IEnumerable<object> parameters) return;

            foreach (var paramObj in parameters)
            {
                if (paramObj is not Dictionary<string, object> param) continue;

                // Required parameter fields
                if (!ValidateRequiredField(param, "name", errors)) continue;
                if (!ValidateRequiredField(param, "type", errors)) continue;
                if (!ValidateRequiredField(param, "description", errors)) continue;

                // Validate type field
                if (param.TryGetValue("type", out var typeObj))
                {
                    var type = typeObj.ToString()!.ToLowerInvariant();
                    var validTypes = new[] { "string", "int", "bool", "float", "double" };
                    if (!validTypes.Contains(type))
                    {
                        errors.Add($"Parameter type '{type}' is not valid. Must be one of: {string.Join(", ", validTypes)}");
                    }
                }

                // Validate map_to field if present
                if (param.TryGetValue("map_to", out var mapToObj))
                {
                    var mapTo = mapToObj.ToString()!.ToLowerInvariant();
                    var validMapTo = new[] { "args", "context", "body" };
                    if (!validMapTo.Contains(mapTo))
                    {
                        errors.Add($"Parameter map_to '{mapTo}' is not valid. Must be one of: {string.Join(", ", validMapTo)}");
                    }
                }

                // Validate required field if present
                if (param.TryGetValue("required", out var requiredObj))
                {
                    if (requiredObj is not bool)
                    {
                        errors.Add("Parameter 'required' field must be a boolean value");
                    }
                }
            }
        }
    }
}
