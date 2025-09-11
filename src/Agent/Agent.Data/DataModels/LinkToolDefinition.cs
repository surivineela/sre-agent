// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using YamlDotNet.Serialization;

namespace Agent.Data.Tools
{
    /// <summary>
    /// YAML tool definition for dynamic link generation via placeholder substitution.
    /// </summary>
    public class LinkToolDefinition : YamlToolDefinitionBase
    {
        [YamlMember(Alias = "template")]
        public string Template { get; set; } = string.Empty;

        /// <summary>
        /// Validates the link tool configuration.
        /// </summary>
        public override void Validate()
        {
            if (string.IsNullOrWhiteSpace(Template))
                throw new ArgumentException("Link tool must define a non-empty 'template'.");
        }
    }
}
