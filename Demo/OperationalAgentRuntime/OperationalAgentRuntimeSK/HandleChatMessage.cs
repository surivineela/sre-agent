using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OperationalAgentCore;

namespace OperationalAgentRuntimeSK
{
    public class Entrypoint
    {
        private readonly ILogger<Entrypoint> _logger;
        private readonly Kernel _kernel;
        private readonly TeamsConnector _teamsConnector;

        public Entrypoint(ILogger<Entrypoint> logger, Kernel kernel, TeamsConnector teamsConnector)
        {
            _logger = logger;
            _kernel = kernel;
            _teamsConnector = teamsConnector;

            Interlocked.CompareExchange(ref GlobalStatic.TeamsConnector, teamsConnector, null);
        }

        [Function("Entrypoint")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            return await ChatHistoryPersistency.ChatHistoryTransition(
                async chatHistory =>
                {
                    // Load tracked app states before adding user message
                    var trackedStates = TrackedActionHelper.GetActions(type: ActionType.AppStateTracking)
                        .OrderByDescending(a => a.Timestamp)
                        .DistinctBy(a => a.Metadata["name"])
                        .ToList();

                    if (trackedStates.Any())
                    {
                        chatHistory.AddSystemMessage($"Current tracked app services ({trackedStates.Count}):\n" +
                            string.Join("\n", trackedStates.Select(t =>
                                $"- {t.Metadata["name"]} ({t.Metadata["state"]}) in {t.Metadata["location"]}")));
                    }

                    chatHistory.AddUserMessage(requestBody);
                    Console.WriteLine("User > " + requestBody);

                    var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                    var result = await chatCompletionService.GetChatMessageContentAsync(
                        chatHistory,
                        executionSettings: new()
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        },
                        kernel: _kernel);

                    Console.WriteLine("Assistant > " + result);
                    chatHistory.AddMessage(result.Role, result.Content ?? string.Empty);

                    // Send to Teams
                    await _teamsConnector.PostMessageAsync(new TeamsMessage(content: result.Content!));

                    return new OkObjectResult(result);
                });
        }
    }
}
