// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Converters
{
    /// <summary>
    /// Converter for transforming tool configurations from V1 to V2 format.
    /// Handles migration of KustoToolV1, LinkToolV1, and ExtendedToolListV1 to their V2 equivalents.
    /// </summary>
    public static class ExtendedToolConverter
    {
        /// <summary>
        /// Converts an ExtendedToolListV1 to a list of ExtendedToolV2 objects.
        /// Each tool in the list is converted based on its type (KustoTool or LinkTool).
        /// </summary>
        /// <param name="toolList">The V1 tool list to convert</param>
        /// <returns>List of ExtendedToolV2 objects</returns>
        public static List<Models.ExtendedToolV2> ConvertToV2(Models.ExtendedToolListV1 toolList)
        {
            if (toolList == null) throw new ArgumentNullException(nameof(toolList));

            var result = new List<Models.ExtendedToolV2>();

            if (toolList.Spec?.Tools == null)
            {
                return result;
            }

            foreach (var toolItem in toolList.Spec.Tools)
            {
                var convertedTool = ConvertToolItemToV2(toolItem);
                if (convertedTool != null)
                {
                    result.Add(convertedTool);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts a single ExtendedToolItemV1 to ExtendedToolV2.
        /// Determines the tool type and creates the appropriate spec.
        /// </summary>
        /// <param name="item">The tool item to convert</param>
        /// <returns>ExtendedToolV2 with the appropriate spec type</returns>
        private static Models.ExtendedToolV2? ConvertToolItemToV2(Models.ExtendedToolItemV1 item)
        {
            if (item == null) return null;

            var tool = new Models.ExtendedToolV2
            {
                Metadata = new Models.ResourceMetadataModel
                {
                    Name = item.Name ?? item.Metadata?.Name,
                    Owner = item.Metadata?.Owner,
                    Tags = item.Metadata?.Tags
                }
            };

            // Determine tool type and create appropriate spec
            if (string.Equals(item.Type, Models.ToolName.KustoTool, StringComparison.OrdinalIgnoreCase))
            {
                tool.Spec = new Models.KustoToolSpecV2
                {
                    Type = item.Type,
                    Connector = item.Connector,
                    Description = item.Description,
                    Database = item.Database,
                    Query = item.Query,
                    Parameters = item.Parameters?.Select(ConvertParameter).ToList()
                };
            }
            else if (string.Equals(item.Type, Models.ToolName.LinkTool, StringComparison.OrdinalIgnoreCase))
            {
                tool.Spec = new Models.LinkToolSpecV2
                {
                    Type = item.Type,
                    Connector = item.Connector,
                    Description = item.Description,
                    Template = item.Template,
                    Parameters = item.Parameters?.Select(ConvertParameter).ToList()
                };
            }
            else
            {
                // Default to base ToolSpecV2 for unknown types
                tool.Spec = new Models.ToolSpecV2
                {
                    Type = item.Type,
                    Connector = item.Connector,
                    Description = item.Description,
                    Parameters = item.Parameters?.Select(ConvertParameter).ToList()
                };
            }

            return tool;
        }
        /// <summary>
        /// Converts KustoToolV1 to ExtendedToolV2 with KustoToolSpecV2.
        /// </summary>
        /// <param name="v1">The V1 Kusto tool to convert</param>
        /// <returns>ExtendedToolV2 with KustoToolSpecV2</returns>
        public static Models.ExtendedToolV2 ConvertToV2(Models.KustoToolV1 v1)
        {
            if (v1 == null) throw new ArgumentNullException(nameof(v1));

            return new Models.ExtendedToolV2
            {
                Metadata = new Models.ResourceMetadataModel
                {
                    Name = v1.Name ?? v1.Metadata?.Name,
                    Owner = v1.Metadata?.Owner,
                    Tags = v1.Metadata?.Tags
                },
                Spec = new Models.KustoToolSpecV2
                {
                    Type = v1.Type,
                    Connector = v1.Connector,
                    Description = v1.Description,
                    Database = v1.Database,
                    Query = v1.Query,
                    Parameters = v1.Parameters?.Select(ConvertParameter).ToList()
                }
            };
        }

        /// <summary>
        /// Converts LinkToolV1 to ExtendedToolV2 with LinkToolSpecV2.
        /// </summary>
        /// <param name="v1">The V1 Link tool to convert</param>
        /// <returns>ExtendedToolV2 with LinkToolSpecV2</returns>
        public static Models.ExtendedToolV2 ConvertToV2(Models.LinkToolV1 v1)
        {
            if (v1 == null) throw new ArgumentNullException(nameof(v1));

            return new Models.ExtendedToolV2
            {
                Metadata = new Models.ResourceMetadataModel
                {
                    Name = v1.Name ?? v1.Metadata?.Name,
                    Owner = v1.Metadata?.Owner,
                    Tags = v1.Metadata?.Tags
                },
                Spec = new Models.LinkToolSpecV2
                {
                    Type = v1.Type,
                    Connector = v1.Connector,
                    Description = v1.Description,
                    Template = v1.Template,
                    Parameters = v1.Parameters?.Select(ConvertParameter).ToList()
                }
            };
        }

        /// <summary>
        /// Converts a ToolParameterV1 to ToolParameterV2.
        /// </summary>
        /// <param name="v1">The V1 parameter to convert</param>
        /// <returns>ToolParameterV2 with all properties mapped</returns>
        private static Models.ToolParameterV2 ConvertParameter(Models.ToolParameterV1 v1)
        {
            return new Models.ToolParameterV2
            {
                Name = v1.Name,
                Type = v1.Type,
                Description = v1.Description,
                MapTo = v1.MapTo,
                Required = v1.Required,
                Target = v1.Target,
                Value = v1.Value
            };
        }
    }
}
