using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Cli.Services;
using Agent.Cli.Helpers;
using Agent.Data.Tools;
using YamlDotNet.Serialization;

namespace Agent.Cli.Validations
{
    public static class ToolValidation
    {
        public static bool ValidateTool(string name, string type, out List<string> errors)
        {
            errors = new List<string>();
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
            errors = new List<string>();
            
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var toolData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
                
                if (toolData == null)
                {
                    errors.Add("Invalid YAML format");
                    return false;
                }

                // Get tool type for specific validation
                if (!toolData.TryGetValue("type", out var typeObj) || typeObj?.ToString() is not string toolType)
                {
                    errors.Add("Tool type is required");
                    return false;
                }

                // Type-specific validation using proper C# classes
                switch (toolType.ToLowerInvariant())
                {
                    case "kustotool":
                        return ValidateKustoTool(yamlContent, errors);
                    // Add other tool type validations as needed
                    default:
                        // Generic validation for unknown types - just ensure basic structure
                        if (!toolData.ContainsKey("name"))
                            errors.Add("Tool name is required");
                        if (!toolData.ContainsKey("description"))
                            errors.Add("Tool description is required");
                        break;
                }

                return errors.Count == 0;
            }
            catch (Exception ex)
            {
                errors.Add($"YAML parsing error: {ex.Message}");
                return false;
            }
        }

        private static bool ValidateKustoTool(string yamlContent, List<string> errors)
        {
            try
            {
                var deserializer = new DeserializerBuilder().Build();
                var kustoTool = deserializer.Deserialize<KustoToolDefinition>(yamlContent);
                
                if (kustoTool == null)
                {
                    errors.Add("Failed to parse KustoTool YAML");
                    return false;
                }

                // Use the built-in validation from the KustoToolDefinition class
                kustoTool.Validate();
                
                return true;
            }
            catch (ArgumentException ex)
            {
                // Validation errors from KustoToolDefinition.Validate()
                errors.Add(ex.Message);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                // Invalid mode or other operational errors
                errors.Add(ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                // YAML deserialization errors
                errors.Add($"KustoTool validation error: {ex.Message}");
                return false;
            }
        }
    }
}
