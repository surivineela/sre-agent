// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Evals.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Agent.Evals;
public class EvalInput
{
    public ChatConfiguration ChatConfiguration { get; private set; }
    public TestContext TestContext { get; private set; }
    public string LlmDeploymentName { get; private set; }
    public string GroundedContext { get; set; } = string.Empty;
    public string ExampleResponse { get; set; } = string.Empty;

    public EvalInput(
        ChatConfiguration chatConfiguration,
        TestContext testContext,
        string llmDeploymentName
        )
    {
        ChatConfiguration = chatConfiguration;
        TestContext = testContext;
        LlmDeploymentName = llmDeploymentName;
    }

    public async Task<List<EvaluationResults>> EvaluateAgentResponsesAsync(IEnumerable<ChatMessage> chatMessages, CancellationToken cancellationToken = default)
    {
        var results = new List<EvaluationResults>();
        var messagesSoFar = new List<ChatMessage>();

        foreach (var msg in chatMessages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            messagesSoFar.Add(msg);
            var response = msg.GetChatResponseForUser();

            if (response != null)
            {
                var result = await response.EvaluateAsync(
                    TestContext,
                    ChatConfiguration,
                    messagesSoFar,
                    GroundedContext,
                    ExampleResponse,
                    LlmDeploymentName);

                if (result != null)
                {
                    results.Add(result);
                }
            }
        }

        return results;
    }
}
