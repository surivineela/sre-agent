// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Buffers;
using System.ClientModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Anthropic;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

namespace Agent.Framework;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

// based on https://github.com/dotnet/extensions/blob/34cdd3a2ddeea9e329356448719ea0d9b896c19c/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/ChatClientStructuredOutputExtensions.cs#L150
// adapted to take a Type parameter instead of using generics because our output types are not known at compile time
public static partial class ChatClientExtensions
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = AIJsonUtilities.DefaultOptions;

    private static readonly AIJsonSchemaCreateOptions _inferenceOptions = new()
    {
        IncludeSchemaKeyword = true,
        TransformOptions = new AIJsonSchemaTransformOptions
        {
            DisallowAdditionalProperties = true,
            RequireAllProperties = true,
            MoveDefaultKeywordToDescription = true,
        },
    };

    [GeneratedRegex("[^0-9A-Za-z_]", RegexOptions.Compiled)]
    private static partial Regex InvalidNameCharsRegex();

    private static readonly Regex _invalidNameCharsRegex = InvalidNameCharsRegex();

    /// <summary>
    /// Gets a chat response without structured output using streaming
    /// </summary>
    public static Task<ChatResponse> GetResponseAsync(
        this IChatClient client,
        IEnumerable<ChatMessage> messages,
        ChatOptions options,
        TextStreamContentHandler? outputTextStreamHandler,
        ReasoningStreamContentHandler? reasoningStreamHandler,
        CancellationToken cancellationToken = default
    )
    {
        return GetResponseWithRateLimitRetriesAsync(
            client: client,
            messages: messages,
            options: options,
            outputTextStreamHandler: outputTextStreamHandler,
            reasoningSummaryStreamHandler: reasoningStreamHandler,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets a chat response with structured output using streaming
    /// </summary>
    public static async Task<(ChatResponse response, object? result)> GetResponseAsync(
        this IChatClient client,
        IEnumerable<ChatMessage> messages,
        Type outputType,
        ChatOptions? options = null,
        JsonStreamContentHandler? outputJsonStreamHandler = null,
        ReasoningStreamContentHandler? reasoningStreamHandler = null,
        CancellationToken cancellationToken = default
    )
    {
        // Check for cancellation before starting
        cancellationToken.ThrowIfCancellationRequested();
        var schemaElement = AIJsonUtilities.CreateJsonSchema(
            type: outputType,
            serializerOptions: _jsonSerializerOptions,
            inferenceOptions: _inferenceOptions);

        bool isWrappedInObject;
        JsonElement schema;
        if (SchemaRepresentsObject(schemaElement))
        {
            // For object-representing schemas, we can use them as-is
            isWrappedInObject = false;
            schema = schemaElement;
        }
        else
        {
            // For non-object-representing schemas, we wrap them in an object schema, because all
            // the real LLM providers today require an object schema as the root. This is currently
            // true even for providers that support native structured output.
            isWrappedInObject = true;
            schema = JsonSerializer.SerializeToElement(new JsonObject
            {
                { "$schema", "https://json-schema.org/draft/2020-12/schema" },
                { "type", "object" },
                { "properties", new JsonObject { { "data", JsonElementToJsonNode(schemaElement) } } },
                { "additionalProperties", false },
                { "required", new JsonArray("data") },
            }, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonObject)));
        }

        options = options is not null ? options.Clone() : new();

        options.ResponseFormat = ChatResponseFormat.ForJsonSchema(
            schema,
            schemaName: SanitizeMemberName(outputType.Name),
            schemaDescription: outputType.GetCustomAttribute<DescriptionAttribute>()?.Description);

        // retry once in case the model returns something invalid (wrong schema, etc.)
        // for rate-limit it will try up to 120 seconds in worst case
        const int retryCount = 1;
        Exception? exception = null;

        for (var i = 0; i <= retryCount; i++)
        {
            var chatResponse = await GetResponseWithRateLimitRetriesAsync(
                client: client,
                messages: messages,
                options: options,
                outputTextStreamHandler: outputJsonStreamHandler,
                reasoningSummaryStreamHandler: reasoningStreamHandler,
                cancellationToken: cancellationToken);

            var firstTextContent = chatResponse.Messages.FirstOrDefault()?.Contents.OfType<TextContent>().FirstOrDefault();

            if (firstTextContent is null)
            {
                return (chatResponse, null);
            }

            var json = firstTextContent.Text;

            if (string.IsNullOrEmpty(json))
            {
                exception = new InvalidOperationException("The response did not contain any JSON output");
                continue;
            }

            if (isWrappedInObject)
            {
                if (JsonDocument.Parse(json).RootElement.TryGetProperty("data", out var data))
                {
                    json = data.GetRawText();
                }
                else
                {
                    exception = new InvalidOperationException("The response did not contain a valid JSON object with a 'data' property");
                    continue;
                }
            }

            object? deserializedResult;
            try
            {
                deserializedResult = DeserializeFirstTopLevelObject(json, _jsonSerializerOptions.GetTypeInfo(outputType));

                if (deserializedResult is null)
                {
                    exception = new InvalidOperationException("The deserialized result is null");
                    continue;
                }
            }
            catch (Exception ex)
            {
                exception = new InvalidOperationException("Failed to deserialize the response", ex);
                continue;
            }

            return (chatResponse, deserializedResult);
        }

        throw exception!;
    }

    private static async Task<ChatResponse> GetResponseWithRateLimitRetriesAsync(
        IChatClient client,
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IStreamContentHandler? outputTextStreamHandler,
        ReasoningStreamContentHandler? reasoningSummaryStreamHandler,
        CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;
        var deadline = start.AddSeconds(60);
        var attempt = 0;
        var nonRateLimitRetried = false;

        Debug.Assert(client is ReasoningChatClient);

        // try finding using chatclient. if set to null, we will fallback to decipher using the response
        var isResponsesApi = (client as ReasoningChatClient)?.UseResponsesApi;

        var clientMetadata = client.GetService<ChatClientMetadata>();
        bool isAnthropicModel = ChatOptionsExtensions.IsAnthropicModel(clientMetadata?.DefaultModelId, clientMetadata?.ProviderName);

        Debug.Assert(isResponsesApi is not null);

        while (true)
        {
            attempt++;
            try
            {
                // Check for cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // common state properties
                var response = default(ChatResponse);

                // chat completions state properties
                List<ChatResponseUpdate> chatResponseUpdates = [];
                var modelId = string.Empty;

                // responses api state properties
                var lastResponse = default(OpenAI.Responses.OpenAIResponse);

                // handle streaming updates
                await foreach (var update in client.GetStreamingResponseAsync(messages, options, cancellationToken))
                {
                    // distinguish responses api based on first update if not known yet
                    isResponsesApi ??= update.RawRepresentation is OpenAI.Responses.StreamingResponseCreatedUpdate;

                    if (isAnthropicModel)
                    {
                        // Capture model ID from updates
                        if (string.IsNullOrWhiteSpace(modelId) && !string.IsNullOrEmpty(update.ModelId))
                        {
                            modelId = update.ModelId;
                        }

                        chatResponseUpdates.Add(update);

                        // reasoning summary delta begin
                        foreach (var content in update.Contents)
                        {
                            if (reasoningSummaryStreamHandler is not null
                                && content is Microsoft.Extensions.AI.TextReasoningContent reasoningContent
                                && !string.IsNullOrEmpty(reasoningContent.Text))
                            {
                                await reasoningSummaryStreamHandler.AppendAsync(reasoningContent.Text);
                            }
                        }

                        // reasoning summary part end
                        if (reasoningSummaryStreamHandler is not null
                            && update.Contents.Count == 0)
                        {
                            await reasoningSummaryStreamHandler.CompleteAsync();
                        }

                        // output text begin
                        if (outputTextStreamHandler is not null
                            && !string.IsNullOrEmpty(update.Text))
                        {
                            await outputTextStreamHandler.AppendAsync(update.Text);
                        }

                        // output text end
                        if (outputTextStreamHandler is not null
                            && update.Contents.Count == 0)
                        {
                            await outputTextStreamHandler.CompleteAsync();
                        }

                    }
                    // keeps getting text delta in case of chat completions API
                    // simple implementation
                    else if (!isResponsesApi.Value)
                    {
                        var completionUpdate = update.RawRepresentation as OpenAI.Chat.StreamingChatCompletionUpdate;

                        // Capture model ID from updates
                        var tempModelId = completionUpdate?.Model;
                        if (!string.IsNullOrEmpty(tempModelId))
                        {
                            modelId = tempModelId;
                        }

                        chatResponseUpdates.Add(update);

                        // output text begin
                        if (outputTextStreamHandler is not null
                            && !string.IsNullOrEmpty(update.Text))
                        {
                            await outputTextStreamHandler.AppendAsync(update.Text);
                        }

                        // output text end
                        if (outputTextStreamHandler is not null
                            && completionUpdate?.ContentUpdate.Count == 0)
                        {
                            await outputTextStreamHandler.CompleteAsync();
                        }
                    }
                    else
                    {
                        // reasoning summary delta begin
                        foreach (var content in update.Contents)
                        {
                            if (reasoningSummaryStreamHandler is not null
                                && content is Microsoft.Extensions.AI.TextReasoningContent reasoningContent
                                && !string.IsNullOrEmpty(reasoningContent.Text))
                            {
                                await reasoningSummaryStreamHandler.AppendAsync(reasoningContent.Text);
                            }
                        }

                        // reasoning summary part end
                        if (reasoningSummaryStreamHandler is not null
                            && update.Contents.Count == 0)
                        {
                            await reasoningSummaryStreamHandler.CompleteAsync();
                        }

                        // output text begin
                        if (outputTextStreamHandler is not null
                            && !string.IsNullOrEmpty(update.Text))
                        {
                            await outputTextStreamHandler.AppendAsync(update.Text);
                        }

                        // output text end
                        if (outputTextStreamHandler is not null
                            && update.RawRepresentation is OpenAI.Responses.StreamingResponseOutputTextDoneUpdate)
                        {
                            await outputTextStreamHandler.CompleteAsync();
                        }

                        // chat updates don't preserve encrypted tokens..
                        // we will capture the final oai response event
                        if (update.RawRepresentation is OpenAI.Responses.StreamingResponseCompletedUpdate responseCompletedUpdate)
                        {
                            lastResponse = responseCompletedUpdate.Response;
                        }

                        // handle errors mid-stream
                        // convert to client result exceptions to retry the loop with delay
                        if (update.RawRepresentation is OpenAI.Responses.StreamingResponseErrorUpdate responseErrorUpdate)
                        {
                            throw new ClientResultException($"Got StreamingResponseErrorUpdate code {responseErrorUpdate.Code ?? ""}, message: {responseErrorUpdate.Message ?? ""}");
                        }
                    }
                }

                // construct final chatresponse
                if (!isResponsesApi.Value)
                {
                    // for chat completions we need to accumulate all updates
                    response = chatResponseUpdates.ToChatResponse();
                    response.ModelId = modelId;
                }
                else
                {
                    // for responses API the last update contains the whole response
                    Debug.Assert(lastResponse is not null);
                    response = lastResponse.AsChatResponse();
                }

                // close both streams at the end no matter what
                if (reasoningSummaryStreamHandler is not null)
                {
                    await reasoningSummaryStreamHandler.CompleteAsync();
                }
                if (outputTextStreamHandler is not null)
                {
                    await outputTextStreamHandler.CompleteAsync();
                }

                if (isAnthropicModel)
                {
                    // Deduplicate tool call messages by tool call id for each response.Messages.Contents
                    // because there's a bug in Anthropic SDK that may cause duplicate tool calls in streaming mode with parallel tool call enabled
                    // https://github.com/anthropics/anthropic-sdk-csharp/issues/53
                    foreach (var message in response.Messages)
                    {
                        var seenToolCallIds = new HashSet<string>();
                        message.Contents = message.Contents
                            .Where(content =>
                            {
                                if (content is FunctionCallContent toolCall)
                                {
                                    return seenToolCallIds.Add(toolCall.CallId);
                                }
                                return true;
                            })
                            .ToList();
                    }
                }

                return response;
            }
            catch (ClientResultException ex) when (IsRateLimit(ex))
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    throw;
                }

                var delay = TryParseRetryAfterSeconds(ex, out var seconds)
                    ? TimeSpan.FromSeconds(seconds)
                    : ComputeBackoff(attempt);

                if (delay > remaining)
                {
                    delay = remaining;
                }

                await Task.Delay(delay, cancellationToken);
                continue;
            }
            catch (ClientResultException)
            {
                if (!nonRateLimitRetried)
                {
                    nonRateLimitRetried = true;
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                    continue;
                }
                throw;
            }
            catch (HttpRequestException)
            {
                if (!nonRateLimitRetried)
                {
                    nonRateLimitRetried = true;
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                    continue;
                }
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User explicitly requested cancellation
                if (outputTextStreamHandler != null)
                {
                    await outputTextStreamHandler.IncompleteAsync();
                }
                throw;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (!nonRateLimitRetried)
                {
                    nonRateLimitRetried = true;
                    await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
                    continue;
                }
                throw;
            }
        }
    }

    private static bool SchemaRepresentsObject(JsonElement schemaElement)
    {
        if (schemaElement.ValueKind is JsonValueKind.Object)
        {
            foreach (var property in schemaElement.EnumerateObject())
            {
                if (property.NameEquals("type"u8))
                {
                    return property.Value.ValueKind == JsonValueKind.String
                        && property.Value.ValueEquals("object"u8);
                }
            }
        }

        return false;
    }

    private static JsonNode? JsonElementToJsonNode(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Array => JsonArray.Create(element),
            JsonValueKind.Object => JsonObject.Create(element),
            _ => JsonValue.Create(element)
        };
    }

    /// <summary>
    /// Removes characters from a .NET member name that shouldn't be used in an AI function name.
    /// </summary>
    /// <param name="memberName">The .NET member name that should be sanitized.</param>
    /// <returns>
    /// Replaces non-alphanumeric characters in the identifier with the underscore character.
    /// Primarily intended to remove characters produced by compiler-generated method name mangling.
    /// </returns>
    private static string SanitizeMemberName(string memberName) =>
        _invalidNameCharsRegex.Replace(memberName, "_");

    private static readonly JsonReaderOptions _allowMultipleValuesJsonReaderOptions = new()
    {
#if NET9_0_OR_GREATER
        AllowMultipleValues = true
#endif
    };

    // based on https://github.com/dotnet/extensions/blob/ed4aeac58f4646a2816987991d6abb9719f59de0/src/Libraries/Microsoft.Extensions.AI/ChatCompletion/ChatResponse%7BT%7D.cs#L94-L113
    private static object? DeserializeFirstTopLevelObject(string json, JsonTypeInfo typeInfo)
    {
        // We need to deserialize only the first top-level object as a workaround for a common LLM backend
        // issue. GPT 3.5 Turbo commonly returns multiple top-level objects after doing a function call.
        // See https://community.openai.com/t/2-json-objects-returned-when-using-function-calling-and-json-mode/574348
        var utf8ByteLength = Encoding.UTF8.GetByteCount(json);
        var buffer = ArrayPool<byte>.Shared.Rent(utf8ByteLength);
        try
        {
            var utf8SpanLength = Encoding.UTF8.GetBytes(json, 0, json.Length, buffer, 0);
            var utf8Span = new ReadOnlySpan<byte>(buffer, 0, utf8SpanLength);
            var reader = new Utf8JsonReader(utf8Span, _allowMultipleValuesJsonReaderOptions);
            return JsonSerializer.Deserialize(ref reader, typeInfo);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool IsRateLimit(System.ClientModel.ClientResultException ex)
    {
        try
        {
            if (ex.Status == 429)
            {
                return true;
            }
        }
        catch
        {
            // Fallback to message checks if Status is not available
        }

        var msg = ex.Message ?? string.Empty;
        return msg.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseRetryAfterSeconds(System.ClientModel.ClientResultException ex, out int seconds)
    {
        seconds = 0;
        var msg = ex.Message ?? string.Empty;
        var m = Regex.Match(msg, @"Try again in\s+(\d+)\s+seconds", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var s))
        {
            seconds = s;
            return true;
        }
        return false;
    }

    private static TimeSpan ComputeBackoff(int attempt)
    {
        var baseMs = Math.Min(1000 * (int)Math.Pow(2, Math.Max(0, attempt - 1)), 10_000);
        var jitter = (int)(baseMs * 0.2 * Random.Shared.NextDouble());
        return TimeSpan.FromMilliseconds(baseMs + jitter);
    }
}
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
