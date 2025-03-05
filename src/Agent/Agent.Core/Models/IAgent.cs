using Microsoft.SemanticKernel.ChatCompletion;

namespace Agent.Core.Models
{
    public interface IAgent
    {
        string Name { get; }
        Task<string> Ask(string question, ChatHistory? externalHistory = null);
        IAsyncEnumerable<string> StreamResponseAsync(
            string message,
            ChatHistory history,
            CancellationToken cancellationToken = default);
    }
}
