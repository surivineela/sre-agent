using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Evals.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace Agent.Evals;
public class EvalInput
{
    public ChatConfiguration ChatConfiguration { get; private set; }
    public TestContext TestContext { get; private set; }
    public string LlmDeploymentName { get; private set; } 
    public string GroundedContext { get; set; }
    public string ExampleResponse { get; set; }

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

    public async Task<List<EvaluationResults>> EvaluateAgentResponsesAsync(IEnumerable<ChatMessage> chatMessages)
    {
        var results = new List<EvaluationResults>();
        var messagesSoFar = new List<ChatMessage>();

        foreach (var msg in chatMessages)
        {
            messagesSoFar.Add(msg);
            var response = msg.GetChatResponseForUser();

            if (response != null)
            {
                var result = await response.EvaluateAsync(
                    this.TestContext,
                    this.ChatConfiguration,
                    messagesSoFar,
                    this.GroundedContext,
                    this.ExampleResponse,
                    this.LlmDeploymentName);

                results.Add(result);
            }
        }

        return results;
    }
}
