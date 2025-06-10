using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Tests.Common.Mocks.FunctionCalling;

/// <summary>
/// AIFunction wrapper that intercepts function calls to check for replay cache hits
/// before falling back to the original function implementation.
/// </summary>
internal class ReplayAIFunctionWrapper : AIFunction
{
    private readonly AIFunction _originalFunction;
    private readonly ReplayToolCore _replayCore;
    private readonly string _functionName;

    public ReplayAIFunctionWrapper(AIFunction originalFunction, ReplayToolCore replayCore, string functionName)
    {
        _originalFunction = originalFunction ?? throw new ArgumentNullException(nameof(originalFunction));
        _replayCore = replayCore ?? throw new ArgumentNullException(nameof(replayCore));
        _functionName = functionName ?? throw new ArgumentNullException(nameof(functionName));
    }

    public override string Name => _originalFunction.Name;
    public override string Description => _originalFunction.Description;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _originalFunction.AdditionalProperties;
    public override JsonElement JsonSchema => _originalFunction.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => _originalFunction.JsonSerializerOptions;
    public override MethodInfo? UnderlyingMethod => _originalFunction.UnderlyingMethod;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_replayCore.FunctionNamesSkippedForReplay.Contains(_functionName, StringComparer.OrdinalIgnoreCase))
            {
                // We specifically know that this function should not be replayed.
                return await _originalFunction.InvokeAsync(arguments, cancellationToken);
            }
        }
        catch (Exception e)
        {
            throw;
        }

        // Convert arguments to dictionary for replay matching
        var argDict = arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Try to find a matching replay entry
        string inputArgsJson = _replayCore.SerializeArguments(argDict);
        var matchingEntry = _replayCore.FindReplayMatch(_functionName, inputArgsJson);

        if (matchingEntry != null)
        {
            // Return cached result from replay
            return JsonSerializer.Deserialize<object>(matchingEntry.FunctionResultJson, _replayCore.SerializerOptions);
        }

        _replayCore.FunctionCallsWithReplayFailure.Add(new ReplayEntry
        {
            FunctionName = _functionName,
            FunctionArgumentsJson = inputArgsJson,
        });

        throw new ReplayFailureException("""
            Tool replay system failure - could not match the tool call to a recorded result.
            Agent operation cannot proceed and must terminate.
            You are running in an evaluation environment that cannot execute the tool with the specified params.
            Report the failure and halt your work.
            Do not attempt to work around this failure.
            """);
    }
}
