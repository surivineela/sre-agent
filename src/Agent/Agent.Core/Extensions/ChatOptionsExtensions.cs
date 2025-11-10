// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ClientModel.Primitives;
using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Agent.Core.Extensions;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public static class ChatOptionsExtensions
{
    public const string ResponseFormatKey = "response_format";
    public const string TextResponseFormat = "text";
    public const string JsonResponseFormat = "json";

    public const string ParallelToolCallKey = "AllowParallelToolCalls";

    public const string ReasoningEffortKey = "reasoning_effort";
    public const string MinimalReasoningEffort = "minimal";
    public const string LowReasoningEffort = "low";
    public const string MediumReasoningEffort = "medium";
    public const string HighReasoningEffort = "high";
    public const string DefaultReasoningEffort = MediumReasoningEffort;

    public const string VerbosityKey = "verbosity";
    public const string DefaultVerbosityLevel = "low";

    // Reference: https://github.com/dotnet/extensions/blob/06ad44909b435bf2c686ecdab908497db05325b8/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIClientExtensions.cs#L27C5-L28C57
    public const string StrictJsonKey = "strictJsonSchema";

    // Reference: https://github.com/dotnet/extensions/blob/afccabd16e08f388f37f1119a127f166f9b03ad3/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L504C13-L570C1
    public static ChatOptions WithRawRepresentationFactory(
        this ChatOptions options,
        IChatClient chatClient)
    {
        options.AdditionalProperties ??= new AdditionalPropertiesDictionary();

        // set strict json deserialization which will be enforced by openai
        options.AdditionalProperties.TryAdd(StrictJsonKey, false);

        // handle properties for reasoning models
        // update options to reflect in logging
        var clientMetadata = chatClient.GetService<ChatClientMetadata>();
        if (clientMetadata?.DefaultModelId is not null
            && clientMetadata.DefaultModelId.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
            && !clientMetadata.DefaultModelId.Contains("gpt-5-chat", StringComparison.OrdinalIgnoreCase))
        {
            // temperature not supported in reasoning models
            options.Temperature = 1;

            // use default reasoning effort if not specified in existing properties
            options.AdditionalProperties.TryAdd(ReasoningEffortKey, DefaultReasoningEffort);

            // set to default verbosity level
            options.AdditionalProperties.TryAdd(VerbosityKey, DefaultVerbosityLevel);
        }

        options.RawRepresentationFactory = (_) =>
        {
            var completionOptions = new ChatCompletionOptions();

            if (options.AdditionalProperties is { Count: > 0 } additionalProperties)
            {
                // output format: text or json
                if (additionalProperties.TryGetValue(ResponseFormatKey, out string? responseFormat))
                {
                    if (string.Equals(TextResponseFormat, responseFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateTextFormat();
                    }
                    else if (string.Equals(JsonResponseFormat, responseFormat, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ResponseFormat = OpenAI.Chat.ChatResponseFormat.CreateJsonObjectFormat();
                    }
                }

                // allow parallel tool calls: only supported in function invoking client
                if (additionalProperties.TryGetValue(ParallelToolCallKey, out bool allowParallelToolCalls))
                {
                    completionOptions.AllowParallelToolCalls = allowParallelToolCalls;
                }

                // reasoning effort: only supported for gpt-5 or other reasoning models
                if (additionalProperties.TryGetValue(ReasoningEffortKey, out string? reasoningEffort)
                    && clientMetadata?.DefaultModelId is not null
                    && clientMetadata.DefaultModelId.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
                    && !clientMetadata.DefaultModelId.Contains("gpt-5-chat", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(LowReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
                    }
                    else if (string.Equals(HighReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ReasoningEffortLevel = ChatReasoningEffortLevel.High;
                    }
                    else if (string.Equals(MediumReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ReasoningEffortLevel = ChatReasoningEffortLevel.Medium;
                    }
                    else if (string.Equals(MinimalReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        // minimal reasoning is new, SDK hasn't yet updated to add a default for it
                        completionOptions.ReasoningEffortLevel = new(MinimalReasoningEffort);
                    }
                    else
                    {
                        // default reasoning effort if not matching anything
                        completionOptions.ReasoningEffortLevel = new(DefaultReasoningEffort);
                    }
                }

                // output verbosity: only supported for gpt-5 model series
                // Reference: https://cookbook.openai.com/examples/gpt-5/gpt-5_new_params_and_tools
                if (additionalProperties.TryGetValue(VerbosityKey, out string? verbosity)
                    && !string.IsNullOrEmpty(verbosity)
                    && clientMetadata?.DefaultModelId is not null
                    && clientMetadata.DefaultModelId.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
                    && !clientMetadata.DefaultModelId.Contains("gpt-5-chat", StringComparison.OrdinalIgnoreCase))
                {
                    completionOptions = AugmentData(completionOptions, VerbosityKey, verbosity);
                }

                // log probabilities for each output token
                // useful to get model confidence on the answer
                // in future we may use it to decide when to run the critic
                if (additionalProperties.TryGetValue(nameof(completionOptions.IncludeLogProbabilities), out bool includeLogProbabilities))
                {
                    completionOptions.IncludeLogProbabilities = includeLogProbabilities;
                }

                // bias specific tokens - favor (100), disfavor (-100)
                // eg: to force a yes / no answer, or to avoid some words out of our domain
                if (additionalProperties.TryGetValue(nameof(completionOptions.LogitBiases), out IDictionary<int, int>? logitBiases))
                {
                    foreach (KeyValuePair<int, int> kvp in logitBiases!)
                    {
                        completionOptions.LogitBiases[kvp.Key] = kvp.Value;
                    }
                }

                // the countInt specifies we will get the log probs for top N tokens beyond which were selected
                // Reference the use in: https://arxiv.org/pdf/2402.10200 (Chain-of-Thought Reasoning without Prompting)
                if (additionalProperties.TryGetValue(nameof(completionOptions.TopLogProbabilityCount), out int topLogProbabilityCountInt))
                {
                    completionOptions.TopLogProbabilityCount = topLogProbabilityCountInt;
                }
            }

            return completionOptions;
        };

        return options;
    }

    public static ChatOptions WithTools(this ChatOptions options,
        IChatClient chatClient,
        List<AITool>? selectedTools)
    {
        options.WithRawRepresentationFactory(chatClient); // if model is gpt-5, this will set the temperature to 1 and ReasoningEffortKey to DefaultReasoningEffort
        options.Tools = selectedTools;
        options.ToolMode = ChatToolMode.Auto;
        return options;
    }

    // workaround for missing values in SDK
    private static ChatCompletionOptions AugmentData(
        ChatCompletionOptions options,
        string key,
        object value)
    {
        // ChatCompletionOptions IJsonModel does not implement create correctly
        // It creates new object from binaryData input instead of merging with existing
        // So we need to create full json from existing options and add property on top
        var baseJson = ModelReaderWriter.Write(options, ModelReaderWriterOptions.Json);
        using var doc = JsonDocument.Parse(baseJson);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();

            // Copy existing fields
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                prop.WriteTo(writer);
            }

            writer.WritePropertyName(key);

            JsonSerializer.Serialize(writer, value);

            writer.WriteEndObject();
        }
        var augmentedJson = new BinaryData(ms.ToArray());

        // Recreate with IJsonModel
        var baseOptions = (IJsonModel<ChatCompletionOptions>)options;
        options = baseOptions.Create(augmentedJson, ModelReaderWriterOptions.Json) ?? options;

        return options;
    }
}
