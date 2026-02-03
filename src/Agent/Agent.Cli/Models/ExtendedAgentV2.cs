// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for agent configurations.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// </summary>
    public class ExtendedAgentV2 : ResourceModel
    {
        public ExtendedAgentV2()
        {
            ApiVersion = YamlApiVersion.V2;
            Kind = ResourceKind.ExtendedAgentV2;
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Agent specification properties.
        /// </summary>
        [YamlMember(Alias = "spec", Order = 1)]
        public ExtendedAgentSpecV2 Spec { get; set; } = new();

        /// <summary>
        /// Normalizes string properties to ensure clean YAML literal block formatting.
        /// Removes trailing whitespace from instructions and other text fields.
        /// </summary>
        public override void Normalize()
        {
            if (Spec != null)
            {
                Spec.Instructions = NormalizeString(Spec.Instructions);
                Spec.HandoffDescription = NormalizeString(Spec.HandoffDescription);
                Spec.CustomReflectionNote = NormalizeString(Spec.CustomReflectionNote);
            }
        }

        /// <summary>
        /// Parses a YAML string into an ExtendedAgentV2 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedAgentV2 object</returns>
        public static ExtendedAgentV2 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<ExtendedAgentV2>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedAgentV2 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed ExtendedAgentV2 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<ExtendedAgentV2?> LoadYamlAsync(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    return null;
                }

                var yamlContent = await File.ReadAllTextAsync(fileName);
                return ParseYaml(yamlContent);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Agent specification for YAML configurations (V2).
    /// Contains agent properties used by the CLI for creating and managing agent configurations.
    /// </summary>
    public class ExtendedAgentSpecV2
    {
        [YamlMember(Alias = "instructions", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Instructions { get; set; }

        [YamlMember(Alias = "handoffDescription")]
        public string? HandoffDescription { get; set; }

        private List<string> _handoffs = new();
        [YamlMember(Alias = "handoffs")]
        public List<string>? Handoffs
        {
            get => _handoffs;
            set => _handoffs = value ?? new List<string>();
        }

        private List<string> _tools = new();
        [YamlMember(Alias = "tools")]
        public List<string>? Tools
        {
            get => _tools;
            set => _tools = value ?? new List<string>();
        }

        [YamlMember(Alias = "allowParallelToolCalls")]
        public bool? AllowParallelToolCalls { get; set; } = true;

        [YamlMember(Alias = "maxReflectionCount")]
        public int? MaxReflectionCount { get; set; }

        [YamlMember(Alias = "criticPromptPath")]
        public string? CriticPromptPath { get; set; }

        [YamlMember(Alias = "criticOnHandoff")]
        public bool? CriticOnHandoff { get; set; }

        [YamlMember(Alias = "customReflectionNote")]
        public string? CustomReflectionNote { get; set; }

        [YamlMember(Alias = "commonPrompts")]
        public List<string>? CommonPrompts { get; set; }

        [YamlMember(Alias = "temperature")]
        public float? Temperature { get; set; }

        [YamlMember(Alias = "outputType")]
        public string? OutputType { get; set; }

        [YamlMember(Alias = "enableVanillaMode")]
        public bool EnableVanillaMode { get; set; }

        [YamlMember(Alias = "enableSkills")]
        public bool? EnableSkills { get; set; }

        [YamlMember(Alias = "addSystemSkills")]
        public bool? AddSystemSkills { get; set; }

        [YamlMember(Alias = "allowedSkills")]
        public List<string>? AllowedSkills { get; set; }

        /// <summary>
        /// Hook configurations for this agent, organized by event type.
        /// Key is the event type name (e.g., "Stop"), value is list of hook definitions.
        /// </summary>
        [YamlMember(Alias = "hooks")]
        public Dictionary<string, List<HookDefinitionModel>>? Hooks { get; set; }
    }

    /// <summary>
    /// Hook definition model for CLI YAML configurations.
    /// </summary>
    public class HookDefinitionModel
    {
        /// <summary>
        /// The type of hook execution. Required.
        /// Valid values: "prompt" (LLM-based evaluation) or "command" (shell command).
        /// </summary>
        [YamlMember(Alias = "type")]
        public string? Type { get; set; }

        /// <summary>
        /// For prompt hooks: the prompt text to send to the LLM.
        /// Use $ARGUMENTS as a placeholder for the hook input context.
        /// </summary>
        [YamlMember(Alias = "prompt", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Prompt { get; set; }

        /// <summary>
        /// For command hooks: the shell command to execute.
        /// The command receives hook context as JSON via stdin.
        /// Mutually exclusive with Script.
        /// </summary>
        [YamlMember(Alias = "command")]
        public string? Command { get; set; }

        /// <summary>
        /// For command hooks: a multi-line bash script to execute.
        /// The script receives hook context as JSON via stdin.
        /// Mutually exclusive with Command.
        /// </summary>
        [YamlMember(Alias = "script", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Script { get; set; }

        /// <summary>
        /// Pattern to match tool names for PostToolUse hooks.
        /// Supports regex patterns (e.g., "Edit|Write", "Bash.*").
        /// Use "*" to match all tools. Empty or null will NOT match any tools.
        /// Required for PostToolUse hooks.
        /// </summary>
        [YamlMember(Alias = "matcher")]
        public string? Matcher { get; set; }

        /// <summary>
        /// Timeout in seconds for hook execution. Default is 30 seconds.
        /// </summary>
        [YamlMember(Alias = "timeout")]
        public int Timeout { get; set; } = 30;

        /// <summary>
        /// Model scenario or deployment name for prompt hooks.
        /// Scenario names (environment-agnostic):
        ///   - ReasoningHeavy: Complex, multi-step reasoning
        ///   - ReasoningFast: Good reasoning with low latency (default for hooks)
        ///   - GeneralPurpose: Mixed tasks with balanced accuracy
        ///   - SmallFast: Lowest cost/latency with acceptable accuracy
        ///   - LongContext: Handling long documents
        ///   - Eval: Grading, review, rubric-based assessment
        /// Or use a deployment name (e.g., "gpt-4.1") for direct model access.
        /// If not specified, defaults to ReasoningFast.
        /// </summary>
        [YamlMember(Alias = "model")]
        public string? Model { get; set; }

        /// <summary>
        /// How to handle hook execution failures (command hooks only).
        /// - Allow (default): On failure, proceed as if no hook was configured.
        /// - Block: On failure, block the action and report the error.
        /// Note: This only affects infrastructure failures (timeout, crash), not hook decisions.
        /// </summary>
        [YamlMember(Alias = "failMode")]
        public HookFailMode FailMode { get; set; } = HookFailMode.Allow;
    }
}
