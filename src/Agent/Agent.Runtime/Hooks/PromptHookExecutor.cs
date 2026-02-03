// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Hooks;

/// <summary>
/// Executes prompt-based hooks by sending context to an LLM for evaluation.
/// The LLM responds with JSON: {"ok": true/false, "reason": "..."}
/// When the context contains a large execution summary, it is saved to a file
/// and tools are provided to the LLM for reading the transcript.
/// </summary>
public class PromptHookExecutor : IHookExecutor
{
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IHookFileTools _hookFileTools;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PromptHookExecutor> _logger;
    private const string ArgumentsPlaceholder = "$ARGUMENTS";

    private static readonly JsonSerializerOptions s_serializeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonSerializerOptions s_deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HookType SupportedType => HookType.Prompt;

    public PromptHookExecutor(
        IChatClientProvider chatClientProvider,
        IHookFileTools hookFileTools,
        ILoggerFactory loggerFactory,
        ILogger<PromptHookExecutor> logger)
    {
        _chatClientProvider = chatClientProvider ?? throw new ArgumentNullException(nameof(chatClientProvider));
        _hookFileTools = hookFileTools ?? throw new ArgumentNullException(nameof(hookFileTools));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HookResult> ExecuteAsync(HookDefinition hook, HookContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hook.Prompt))
        {
            _logger.LogInternalWarning("Prompt hook has no prompt defined, allowing action");
            return HookResult.Success();
        }

        string? transcriptPath = null;

        try
        {
            // If execution summary exists, save it to a file and replace with path
            if (!string.IsNullOrEmpty(context.ExecutionSummary))
            {
                transcriptPath = await _hookFileTools.SaveTranscriptAsync(context.ThreadId, context.ExecutionSummary);
                context.ExecutionSummary = transcriptPath;
                _logger.LogInternalDebug("Saved execution summary to {TranscriptPath}", transcriptPath);
            }

            var chatClient = GetChatClient(hook.Model, transcriptPath != null);
            var prompt = BuildPrompt(hook.Prompt, context);

            _logger.LogInternalDebug("Executing prompt hook for {EventType}", context.HookEventName);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(hook.Timeout));

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, GetSystemPrompt(transcriptPath != null)),
                new(ChatRole.User, prompt)
            };

            // Create chat options with tools if transcript exists
            ChatOptions? chatOptions = null;
            if (transcriptPath != null)
            {
                var tools = CreateHookTools();
                chatOptions = new ChatOptions
                {
                    Tools = tools,
                    AllowMultipleToolCalls = true
                };
            }

            // Execute - function invocation is handled automatically by the wrapped client
            var response = await chatClient.GetResponseAsync(messages, chatOptions, cts.Token);
            var responseText = response.Text?.Trim() ?? string.Empty;

            _logger.LogInternalDebug("Hook response: {Response}", responseText);

            return ParseResponse(responseText);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalWarning("Prompt hook timed out after {Timeout}s, allowing action", hook.Timeout);
            return HookResult.Error($"Hook timed out after {hook.Timeout} seconds");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error executing prompt hook");
            return HookResult.Error(ex.Message);
        }
        finally
        {
            // Cleanup transcript file
            if (transcriptPath != null)
            {
                await _hookFileTools.DeleteTranscriptAsync(transcriptPath);
            }
        }
    }

    /// <summary>
    /// Creates the tools available to the hook LLM for reading transcript files.
    /// </summary>
    private List<AITool> CreateHookTools()
    {
        // Try to use the properly-attributed methods on HookFileTools if available
        // The [Description] attributes provide comprehensive documentation for the LLM
        if (_hookFileTools is HookFileTools hookFileTools)
        {
            return new List<AITool>
            {
                AIFunctionFactory.Create(hookFileTools.ReadFile),
                AIFunctionFactory.Create(hookFileTools.GrepSearch)
            };
        }

        // Fallback for testing: create tools from interface methods
        // Note: These won't have the Description attributes but will still work
        return new List<AITool>
        {
            AIFunctionFactory.Create(_hookFileTools.ReadFileQuietAsync, "ReadFile"),
            AIFunctionFactory.Create(_hookFileTools.GrepSearchQuietAsync, "GrepSearch")
        };
    }

    private IChatClient GetChatClient(string? modelName, bool withFunctionInvocation)
    {
        IChatClient baseChatClient;

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            // First, try to parse as a ModelScenarioType
            if (Enum.TryParse<ModelScenarioType>(modelName, ignoreCase: true, out var scenarioType))
            {
                _logger.LogInternalDebug("Using model scenario {Scenario} for hook", scenarioType);
                baseChatClient = _chatClientProvider.GetBestModelByScenario(scenarioType);
            }
            else
            {
                // Otherwise, try as a direct deployment name
                try
                {
                    baseChatClient = _chatClientProvider.GetModelByKey<IChatClient>(modelName);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(
                        "Model '{Model}' not found ({Error}), falling back to ReasoningFastModel",
                        modelName,
                        ex.Message);
                    baseChatClient = _chatClientProvider.ReasoningFastModel;
                }
            }
        }
        else
        {
            // Default to fast model for hook evaluation
            baseChatClient = _chatClientProvider.ReasoningFastModel;
        }

        // Wrap with function invocation if tools are being used
        if (withFunctionInvocation)
        {
            return baseChatClient
                .AsBuilder()
                .UseFunctionInvocation(_loggerFactory, options =>
                {
                    options.IncludeDetailedErrors = true;
                })
                .Build();
        }

        return baseChatClient;
    }

    private static string BuildPrompt(string promptTemplate, HookContext context)
    {
        var contextJson = JsonSerializer.Serialize(context, context.GetType(), s_serializeOptions);

        if (promptTemplate.Contains(ArgumentsPlaceholder))
        {
            return promptTemplate.Replace(ArgumentsPlaceholder, contextJson);
        }

        // If no placeholder, append context to the end
        return $"{promptTemplate}\n\nContext:\n{contextJson}";
    }

    private static string GetSystemPrompt(bool hasTools)
    {
        var basePrompt = """
            You are a hook executor. Your role is to evaluate context data according to instructions provided in the user message and respond with a JSON decision.

            The user message will contain:
            1. Specific evaluation instructions from the agent author
            2. Context data (JSON) about the current hook event

            ## Required Response Format
            You MUST respond with a valid JSON object:
            - To allow the action: {"ok": true}
            - To block the action: {"ok": false, "reason": "Your explanation here"}

            The reason field is required when ok is false. Respond ONLY with the JSON object.

            ## Context Schema

            ### Common Fields (all hook types)
            - **hook_event_name**: The hook type being evaluated ("Stop" or "PostToolUse")
            - **agent_name**: Name of the agent being evaluated
            - **current_turn**: Current turn number in the agent's execution loop
            - **max_turns**: Maximum turns allowed for this execution
            - **execution_summary**: Conversation/execution summary (may be a file path for large transcripts)

            ### Stop Hook Fields (when hook_event_name = "Stop")
            Stop hooks trigger when the agent is about to complete and return a final response.

            - **final_output**: The response the agent is about to return
            - **stop_hook_active**: True if a stop hook has already rejected in this execution
            - **stop_rejection_count**: Number of times stop hooks have rejected so far

            ### PostToolUse Hook Fields (when hook_event_name = "PostToolUse")
            PostToolUse hooks trigger after a tool executes.

            - **tool_name**: Name of the executed tool (e.g., "Edit", "Bash", "Write")
            - **tool_input**: Arguments passed to the tool (JSON object)
            - **tool_result**: Output from the tool execution
            - **tool_succeeded**: Boolean indicating if execution succeeded without exception
            """;

        if (hasTools)
        {
            return basePrompt + """


            ## Transcript Access
            IMPORTANT: The execution_summary field contains a FILE PATH, not the content itself.
            You have tools to read and search the transcript:
            - ReadFile: Read specific line ranges from the transcript
            - GrepSearch: Search for patterns in the transcript

            Start by reading the first ~100 lines to understand context, then search or read more as needed.
            """;
        }

        return basePrompt;
    }

    private HookResult ParseResponse(string responseText)
    {
        try
        {
            // Try to extract JSON from the response (handle markdown code blocks)
            var jsonText = ExtractJson(responseText);

            var result = JsonSerializer.Deserialize<HookResult>(jsonText, s_deserializeOptions);

            if (result != null)
            {
                return result;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogInternalWarning(ex, "Failed to parse hook response as JSON: {Response}", responseText);
        }

        // If parsing fails, default to allowing the action
        _logger.LogInternalWarning("Could not parse hook response, allowing action by default");
        return HookResult.Error("Failed to parse hook response");
    }

    private static string ExtractJson(string text)
    {
        // Remove markdown code block markers if present
        var trimmed = text.Trim();

        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed[3..];
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }
}
