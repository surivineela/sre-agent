using Agents.Core.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Agents.Core.Helpers;

public sealed class AsyncOperationStatus<TDescriptor, TParameter, TProgressDetail>

{
    private readonly List<AsyncOperationProgress<TProgressDetail>> _progress = new();
    // TODO: add continuation to task to dispose this token source
    // TODO: cancel is not guaranteed, it could cancel but operation still succeeded
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public string ContextMessage { get; }

    public TDescriptor Descriptor;

    public DateTime StartTime { get; } = DateTime.Now;

    public IReadOnlyList<AsyncOperationProgress<TProgressDetail>> Events => _progress;

    public Task<string> Task;

    public AsyncOperationStatus(
        Kernel kernel,
        string contextMessage,
        TDescriptor descriptor,
        TParameter parameter,
        Func<Kernel, TDescriptor, TParameter, Action<TProgressDetail>, CancellationToken, Task<string>> func,
        Func<string, bool> funcShouldSendTeamsNotification)
    {
        ContextMessage = contextMessage;
        Descriptor = descriptor;
        Task = Execute(kernel, descriptor, parameter, func, funcShouldSendTeamsNotification);
    }

    private async Task<string> Execute(
        Kernel kernel,
        TDescriptor descriptor,
        TParameter parameter,
        Func<Kernel, TDescriptor, TParameter, Action<TProgressDetail>, CancellationToken, Task<string>> func,
        Func<string, bool> funcShouldSendTeamsNotification)
    {
        string result;
        bool shouldSendTeamsUpdate = true;
        try
        {
            result = await func(kernel, descriptor, parameter, AddProgress, _cancellationTokenSource.Token);
            shouldSendTeamsUpdate = funcShouldSendTeamsNotification(result);
        }
        catch (Exception ex)
        {
            result = ex.Message;
        }

        // Push back the conclusion to the main history
        await ChatHistoryPersistency.ChatHistoryTransition(
            async history =>
            {
                history.AddSystemMessage($"Background operation for: {ContextMessage}, has finished. here is the result: {result}");

                if (shouldSendTeamsUpdate)
                {
                    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
                    var mainChatResult = await chatCompletionService.GetChatMessageContentAsync(
                        history,
                        executionSettings: new()
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        },
                        kernel: kernel);

                    await GlobalStatic.TeamsConnector.PostMessageAsync(new TeamsMessage(content: mainChatResult.Content ?? string.Empty));
                    history.AddAssistantMessage(mainChatResult.Content ?? string.Empty);
                }

                return 0;
            });

        return result;
    }

    public void Cancel()
    {
        if (!Task.IsCompleted)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    private void AddProgress(TProgressDetail progress)
    {
        lock (_progress)
        {
            _progress.Add(new(DateTime.Now, progress));
        }
    }

    public AsyncOperationStatusSummary<TDescriptor, TProgressDetail> Summarize()
    {
        lock (_progress)
        {
            return new AsyncOperationStatusSummary<TDescriptor, TProgressDetail>(
                ContextMessage: ContextMessage,
                StartTime: StartTime,
                Descriptor: Descriptor,
                Progress: _progress.ToArray(),
                OverallStatus: Task.IsCompleted
                    ? (_cancellationTokenSource.IsCancellationRequested
                        ? "Cancelled"
                        : (Task.IsCompletedSuccessfully
                            ? "Finished successfully"
                            : "Finished but failed"))
                    : (_cancellationTokenSource.IsCancellationRequested
                        ? "Cancelling"
                        : "Still running"),
                Details: Task.IsCompletedSuccessfully
                    && !_cancellationTokenSource.IsCancellationRequested
                    ? Task.Result
                    : "");
        }
    }
}
