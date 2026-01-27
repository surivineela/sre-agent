// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ClientModel.Primitives;
using System.Text.Json;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI.Responses;

namespace Agent.Framework;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public static class ChatOptionsExtensions
{
    public const string ResponseFormatKey = "response_format";
    public const string TextResponseFormat = "text";
    public const string JsonResponseFormat = "json";

    public const string ParallelToolCallKey = "AllowParallelToolCalls";

    public const string DefaultReasoningEffort = ReasoningConstants.LowReasoningEffort;

    public const string StoreKey = "store";

    public const string IncludeLogProbabilitiesKey = "include_logprobs";

    // OAI Reference: https://platform.openai.com/docs/api-reference/responses/create#responses_create-top_logprobs
    // AOAI Reference: https://learn.microsoft.com/en-us/azure/ai-foundry/openai/latest#request-body-10
    public const string TopLogProbabilityCountKey = "top_logprobs";

    public const string OutputTextVerbosityKey = "verbosity";
    public const string LowTextVerbosity = "low";
    public const string MediumTextVerbosity = "medium";
    public const string HighTextVerbosity = "high";
    public const string DefaultTextVerbosity = LowTextVerbosity;

    public const string ReasoningSummaryVerbosityKey = "reasoning_verbosity";
    public const string AutoReasoningVerbosity = "auto";
    public const string ConciseReasoningVerbosity = "concise";
    public const string DetailedReasoningVerbosity = "detailed";
    public const string DefaultReasoningVerbosity = AutoReasoningVerbosity;

    // OAI Reference: https://platform.openai.com/docs/api-reference/responses/create#responses_create-include
    // AOAI Reference: https://learn.microsoft.com/en-us/azure/ai-foundry/openai/latest#request-body-10
    public const string IncludeKey = "include";
    public const string ReasoningEncryptedContentValue = "reasoning.encrypted_content";
    public const string MessageOutputLogprobsValue = "message.output_text.logprobs";

    public const string ServiceTierKey = "service_tier";

    // Reference: https://github.com/dotnet/extensions/blob/06ad44909b435bf2c686ecdab908497db05325b8/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIClientExtensions.cs#L27C5-L28C57
    public const string StrictJsonKey = "strictJsonSchema";

    public static ChatOptions WithRawRepresentationFactory(
        this ChatOptions options,
        IChatClient chatClient,
        ReasoningChatClientOptions reasoningOptions)
    {
        if (reasoningOptions is AnthropicReasoningChatClientOptions anthropicOptions)
        {
            // for Anthropic models, set up a custom RawRepresentationFactory
            options.RawRepresentationFactory = (_) =>
            {
                return CreateAnthropicMessageCreateParams(anthropicOptions, options);
            };

            return options;
        }

        bool configureForResponsesApi = reasoningOptions is OpenAIReasoningChatClientOptions { UseResponsesApi: true };
        // configure Extensions.AI.Options & its AdditionalProperties
        options.AdditionalProperties ??= [];

        // parallel tool calling
        if (options.AdditionalProperties.TryGetValue(ParallelToolCallKey, out bool allowParallelToolCalls))
        {
            options.AllowMultipleToolCalls = allowParallelToolCalls;
        }

        // TODO [Vishesh]: TEST WITH MCP AND SET TO TRUE
        // OTHER OPTION >>> ONLY TRUE FOR STRUCTURED RESPONSES
        // set strict json deserialization which will be enforced by openai
        options.AdditionalProperties.TryAdd(StrictJsonKey, false);

        // modifications needed for reasoning models
        var clientMetadata = chatClient.GetService<ChatClientMetadata>();
        var isReasoningModel = IsReasoningModel(clientMetadata?.DefaultModelId);
        bool isAnthropicModel = IsAnthropicModel(clientMetadata?.DefaultModelId, clientMetadata?.ProviderName);
        if (isReasoningModel)
        {
            // temperature not supported in reasoning models
            options.Temperature = null;

            // use default reasoning effort if not specified in existing properties
            options.AdditionalProperties.TryAdd(FrameworkConstants.ReasoningEffortKey, DefaultReasoningEffort);

            // set to default verbosity level
            options.AdditionalProperties.TryAdd(OutputTextVerbosityKey, DefaultTextVerbosity);

            // logprobs are not supported with reasoning models
            options.AdditionalProperties.Remove(IncludeLogProbabilitiesKey);
            options.AdditionalProperties.Remove(TopLogProbabilityCountKey);
        }

        // configure Extensions.AI.Options further for responses API supported features
        if (configureForResponsesApi)
        {
            // turn off response storage for stateless mode in responses API
            // Reference: https://platform.openai.com/docs/guides/migrate-to-responses#4-decide-when-to-use-statefulness
            options.AdditionalProperties.TryAdd(StoreKey, false);

            var reasoningIncludeList = new List<string>();

            if (options.AdditionalProperties.TryGetValue(IncludeLogProbabilitiesKey, out bool includeLogProbs)
                && includeLogProbs)
            {
                reasoningIncludeList.Add(MessageOutputLogprobsValue);
            }

            // properties only supported in reasoning models
            if (isReasoningModel)
            {
                // default reasoning summary verbosity
                options.AdditionalProperties.TryAdd(ReasoningSummaryVerbosityKey, DefaultReasoningVerbosity);

                // include reasoning tokens (encrypted)
                // Reference: https://platform.openai.com/docs/guides/migrate-to-responses#4-decide-when-to-use-statefulness
                reasoningIncludeList.Add(ReasoningEncryptedContentValue);
            }

            if (reasoningIncludeList.Count > 0)
            {
                options.AdditionalProperties.TryAdd(IncludeKey, reasoningIncludeList);
            }
        }

        if (configureForResponsesApi)
        {
            // configure OpenAI.ResponseCreationOptions based on above AdditionalProperties
            // Reference: https://github.com/dotnet/extensions/blob/8271d4e7a3fba362915660eb81c92f39ab3364f6/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIResponseChatClient.cs#L331C13-L366C14
            options.RawRepresentationFactory = (_) => CreateResponseCreationOptions(options.AdditionalProperties, isReasoningModel);
        }
        else
        {
            // configure OpenAI.ChatCompletionOptions based on above AdditionalProperties
            // Reference: https://github.com/dotnet/extensions/blob/afccabd16e08f388f37f1119a127f166f9b03ad3/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L504C13-L570C1
            options.RawRepresentationFactory = (_) => CreateChatCompletionOptions(options.AdditionalProperties, isReasoningModel);
        }

        return options;
    }

    public static bool IsReasoningModel(string? modelId)
    {
        return !string.IsNullOrEmpty(modelId)
            && modelId.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
            && !modelId.Contains("gpt-5-chat", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAnthropicModel(string? modelId, string? providerName)
    {
        return providerName?.Equals("Anthropic", StringComparison.OrdinalIgnoreCase) == true
            || (!string.IsNullOrEmpty(modelId)
                && (modelId.Contains("claude", StringComparison.OrdinalIgnoreCase)
                    || modelId.Contains("anthropic", StringComparison.OrdinalIgnoreCase)));
    }

    // This is a temporary hack to enable extended thinking for Anthropic models because the Official SDK's IChatClient adaptor doesn't support it yet.
    // Other parameters(allow parallel tool calls, temperature, topK, topP, outputFormat) are already populated in https://github.com/anthropics/anthropic-sdk-csharp/blob/v11.0.0/src/Anthropic/Services/Beta/Messages/AnthropicBetaClientExtensions.cs
    private static MessageCreateParams CreateAnthropicMessageCreateParams(AnthropicReasoningChatClientOptions reasoningOptions, ChatOptions chatOptions)
    {
        var messageParams = new MessageCreateParams()
        {
            MaxTokens = reasoningOptions.MaxOutputTokens,
            Messages = [],
            Model = reasoningOptions.ModelId,
        };

        // When ToolMode is None, explicitly set ToolChoice to null to avoid Anthropic API error:
        // "tools are required when tool choice is specified"
        // The Anthropic SDK's ChatClient adapter may set ToolChoice even when Tools is null/empty
        if (chatOptions.ToolMode == ChatToolMode.None)
        {
            messageParams = messageParams with
            {
                ToolChoice = null,
                Tools = null,
            };
        }

        if (reasoningOptions.ExtendedThinkingEnabled)
        {
            messageParams = messageParams with
            {
                Thinking = new BetaThinkingConfigEnabled()
                {
                    BudgetTokens = reasoningOptions.ThinkingTokenBudget,
                },
                // If temperature is not 1, we will get a 400 error saying temperature may only be set to 1 when thinking is enabled.
                Temperature = 1.0,
            };
        }

        return messageParams;
    }

    private static ChatCompletionOptions CreateChatCompletionOptions(AdditionalPropertiesDictionary optionProperties, bool isReasoningModel)
    {
        var completionOptions = new ChatCompletionOptions();

        if (optionProperties.Count > 0)
        {
            // output format: text or json
            if (optionProperties.TryGetValue(ResponseFormatKey, out string? responseFormat)
                && !string.IsNullOrEmpty(responseFormat))
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

            // reasoning effort and verbosity: for reasoning models (gpt-5 series)
            if (isReasoningModel)
            {
                // reasoning effort
                if (optionProperties.TryGetValue(FrameworkConstants.ReasoningEffortKey, out string? reasoningEffort)
                    && !string.IsNullOrEmpty(reasoningEffort))
                {
                    if (string.Equals(ReasoningConstants.LowReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ReasoningEffortLevel = ChatReasoningEffortLevel.Low;
                    }
                    else if (string.Equals(ReasoningConstants.HighReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ReasoningEffortLevel = ChatReasoningEffortLevel.High;
                    }
                    else if (string.Equals(ReasoningConstants.MediumReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        completionOptions.ReasoningEffortLevel = ChatReasoningEffortLevel.Medium;
                    }
                    else if (string.Equals(ReasoningConstants.MinimalReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        // minimal reasoning is new, SDK hasn't yet updated to add a default for it
                        completionOptions.ReasoningEffortLevel = new(ReasoningConstants.MinimalReasoningEffort);
                    }
                }
                else
                {
                    // default reasoning effort if not matching anything
                    completionOptions.ReasoningEffortLevel = new(DefaultReasoningEffort);
                }

                // output text verbosity: only supported for gpt-5 model series
                // Reference: https://cookbook.openai.com/examples/gpt-5/gpt-5_new_params_and_tools
                if (optionProperties.TryGetValue(OutputTextVerbosityKey, out string? verbosity)
                    && !string.IsNullOrEmpty(verbosity))
                {
                    completionOptions = completionOptions.AugmentJsonModel(OutputTextVerbosityKey, verbosity);
                }
            }

            // log probabilities for each output token
            // useful to get model confidence on the answer
            // in future we may use it to decide when to run the critic
            if (optionProperties.TryGetValue(IncludeLogProbabilitiesKey, out bool includeLogProbs))
            {
                completionOptions.IncludeLogProbabilities = includeLogProbs;
            }

            // bias specific tokens - favor (100), disfavor (-100)
            // eg: to force a yes / no answer, or to avoid some words out of our domain
            if (optionProperties.TryGetValue(nameof(completionOptions.LogitBiases), out IDictionary<int, int>? logitBiases)
                && logitBiases?.Count > 0)
            {
                foreach (var kvp in logitBiases)
                {
                    completionOptions.LogitBiases[kvp.Key] = kvp.Value;
                }
            }

            // the countInt specifies we will get the log probs for top N tokens beyond which were selected
            // Reference the use in: https://arxiv.org/pdf/2402.10200 (Chain-of-Thought Reasoning without Prompting)
            if (optionProperties.TryGetValue(TopLogProbabilityCountKey, out int topLogProbs))
            {
                completionOptions.IncludeLogProbabilities = true;
                completionOptions.TopLogProbabilityCount = topLogProbs;
            }
        }

        return completionOptions;
    }

    private static CreateResponseOptions CreateResponseCreationOptions(AdditionalPropertiesDictionary optionProperties, bool isReasoningModel)
    {
        var responseOptions = new CreateResponseOptions()
        {
            TextOptions = new()
        };

        if (optionProperties.Count > 0)
        {
            // output format: text or json
            if (optionProperties.TryGetValue(ResponseFormatKey, out string? responseFormat)
                && !string.IsNullOrEmpty(responseFormat))
            {
                if (string.Equals(TextResponseFormat, responseFormat, StringComparison.OrdinalIgnoreCase))
                {
                    responseOptions.TextOptions.TextFormat = ResponseTextFormat.CreateTextFormat();
                }
                else if (string.Equals(JsonResponseFormat, responseFormat, StringComparison.OrdinalIgnoreCase))
                {
                    responseOptions.TextOptions.TextFormat = ResponseTextFormat.CreateJsonObjectFormat();
                }
            }

            // whether to store conversation on the server
            // only set to true for stateful responses (replay conversation id)
            // should ALWAYS be false for us as we use stateless mode
            // reference: https://platform.openai.com/docs/guides/conversation-state?api-mode=responses
            if (optionProperties.TryGetValue(StoreKey, out bool responseStore))
            {
                responseOptions.StoredOutputEnabled = responseStore;
            }

            // service tier.. only works when calling OpenAI endpoint directly
            // NOT supported by Azure Open AI
            // References:
            // supported values:
            // default: normal latency
            // flex: slow, background processing
            // Reference: https://platform.openai.com/docs/guides/flex-processing?api-mode=responses
            // priority: fast responses with consumption model
            // Reference: https://openai.com/api-priority-processing/
            // scale: fast responses with pre-purchased scale tier bundles (dedicated, reserved capacity)
            // Reference: https://openai.com/api-scale-tier/
            if (optionProperties.TryGetValue(ServiceTierKey, out string? serviceTier)
                && !string.IsNullOrEmpty(serviceTier))
            {
                responseOptions.ServiceTier = new(serviceTier);
            }

            // reasoning effort and verbosity: for reasoning models (gpt-5 series)
            if (isReasoningModel)
            {
                responseOptions.ReasoningOptions ??= new();

                // reasoning effort
                if (optionProperties.TryGetValue(FrameworkConstants.ReasoningEffortKey, out string? reasoningEffort)
                    && !string.IsNullOrEmpty(reasoningEffort))
                {
                    if (string.Equals(ReasoningConstants.MinimalReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        responseOptions.ReasoningOptions.ReasoningEffortLevel = new(ReasoningConstants.MinimalReasoningEffort);
                    }
                    else if (string.Equals(ReasoningConstants.LowReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        responseOptions.ReasoningOptions.ReasoningEffortLevel = ResponseReasoningEffortLevel.Low;
                    }
                    else if (string.Equals(ReasoningConstants.MediumReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        responseOptions.ReasoningOptions.ReasoningEffortLevel = ResponseReasoningEffortLevel.Medium;
                    }
                    else if (string.Equals(ReasoningConstants.HighReasoningEffort, reasoningEffort, StringComparison.OrdinalIgnoreCase))
                    {
                        responseOptions.ReasoningOptions.ReasoningEffortLevel = ResponseReasoningEffortLevel.High;
                    }
                }
                else
                {
                    // default reasoning effort if not matching anything
                    responseOptions.ReasoningOptions.ReasoningEffortLevel = new(DefaultReasoningEffort);
                }

                // output text verbosity: only supported for gpt-5 model series
                // Reference: https://cookbook.openai.com/examples/gpt-5/gpt-5_new_params_and_tools
                if (optionProperties.TryGetValue(OutputTextVerbosityKey, out string? outputTextVerbosity)
                    && !string.IsNullOrEmpty(outputTextVerbosity))
                {
                    responseOptions.TextOptions = responseOptions.TextOptions.AugmentJsonModel(OutputTextVerbosityKey, outputTextVerbosity);
                }

                // reasoning summary verbosity
                if (optionProperties.TryGetValue(ReasoningSummaryVerbosityKey, out string? reasoningSummaryVerbosity)
                    && !string.IsNullOrEmpty(reasoningSummaryVerbosity))
                {
                    if (string.Equals(AutoReasoningVerbosity, reasoningSummaryVerbosity, StringComparison.OrdinalIgnoreCase))
                    {
                        responseOptions.ReasoningOptions.ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Auto;
                    }
                    else if (string.Equals(DetailedReasoningVerbosity, reasoningSummaryVerbosity, StringComparison.OrdinalIgnoreCase))
                    {
                        responseOptions.ReasoningOptions.ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Detailed;
                    }
                    else if (string.Equals(ConciseReasoningVerbosity, reasoningSummaryVerbosity, StringComparison.OrdinalIgnoreCase))
                    {
                        responseOptions.ReasoningOptions.ReasoningSummaryVerbosity = ResponseReasoningSummaryVerbosity.Concise;
                    }
                }
            }

            // include
            if (optionProperties.TryGetValue(IncludeKey, out object? includeFields)
                && includeFields is not null)
            {
                responseOptions = responseOptions.AugmentJsonModel(IncludeKey, includeFields);
            }

            // the topLogProbs specifies we will get the log probs for top N tokens beyond which were selected
            // Reference the use in: https://arxiv.org/pdf/2402.10200 (Chain-of-Thought Reasoning without Prompting)
            if (optionProperties.TryGetValue(TopLogProbabilityCountKey, out int topLogProbs))
            {
                responseOptions = responseOptions.AugmentJsonModel(TopLogProbabilityCountKey, topLogProbs);
            }

            // logit bias not supported in Responses API
        }

        return responseOptions;
    }

    // workaround for missing values in SDK
    // Key-Value should follow official OpenAI API Reference
    // Responses API: https://platform.openai.com/docs/api-reference/responses/create
    // Chat Completions API: https://platform.openai.com/docs/api-reference/chat/create
    private static T AugmentJsonModel<T>(
        this T options,
        string key,
        object value)
        where T : IJsonModel<T>
    {
        // IJsonModel implementations in OpenAI .NET SDK do not implement `Create` correctly
        // They create new object from binaryData input instead of merging with existing
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
        var baseOptions = (IJsonModel<T>)options;
        options = baseOptions.Create(augmentedJson, ModelReaderWriterOptions.Json) ?? options;

        return options;
    }
}
