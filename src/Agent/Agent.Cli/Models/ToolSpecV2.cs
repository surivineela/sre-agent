// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Data.DataModels;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Base tool specification for YAML configurations (V2).
    /// Contains common tool properties used by the CLI for creating and managing tool configurations.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(KustoToolSpecV2), ToolDocumentModel.KustoToolType)]
    [JsonDerivedType(typeof(LinkToolSpecV2), ToolDocumentModel.LinkToolType)]
    [JsonDerivedType(typeof(PythonToolSpecV2), ToolDocumentModel.PythonToolType)]
    [JsonDerivedType(typeof(HttpClientToolSpecV2), ToolDocumentModel.HttpClientToolType)]
    public class ToolSpecV2
    {
        private string? _type;

        /// <summary>
        /// Gets or sets the tool type.
        /// When getting, if not explicitly set, automatically infers the type from the runtime class.
        /// This allows YAML deserialization to set the value while still providing automatic type inference during serialization.
        /// </summary>
        [YamlMember(Alias = "type", Order = 0)]
        [JsonIgnore] // Ignore during JSON deserialization to avoid conflict with JsonPolymorphic discriminator
        public string? Type
        {
            get => _type ?? (this switch
            {
                KustoToolSpecV2 => ToolDocumentModel.KustoToolType,  // "KustoTool"
                LinkToolSpecV2 => ToolDocumentModel.LinkToolType,    // "LinkTool"
                PythonToolSpecV2 => ToolDocumentModel.PythonToolType, // "PythonTool"
                HttpClientToolSpecV2 => ToolDocumentModel.HttpClientToolType, // "HttpClientTool"
                _ => _type
            });
            set => _type = value;
        }

        [YamlMember(Alias = "connector", Order = 1)]
        public string? Connector { get; set; }

        [YamlMember(Alias = "toolMode", Order = 2)]
        public ToolMode ToolMode { get; set; } = ToolMode.Auto;

        [YamlMember(Alias = "description", Order = 3, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Description { get; set; }

        [YamlMember(Alias = "parameters", Order = 100, DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
        public List<ToolParameterV2>? Parameters { get; set; }
    }

    /// <summary>
    /// Parameter specification for tool parameters.
    /// </summary>
    public class ToolParameterV2
    {
        [YamlMember(Alias = "name")]
        public string? Name { get; set; }

        [YamlMember(Alias = "type")]
        public string? Type { get; set; }

        [YamlMember(Alias = "description", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Description { get; set; }

        [YamlMember(Alias = "mapTo")]
        public string? MapTo { get; set; }

        [YamlMember(Alias = "required")]
        public bool? Required { get; set; }

        [YamlMember(Alias = "target")]
        public string? Target { get; set; }

        [YamlMember(Alias = "value")]
        public object? Value { get; set; }
    }
}
