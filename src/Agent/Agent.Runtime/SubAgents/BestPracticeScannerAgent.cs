using Agent.Core.Models;
using Castle.Core.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;

namespace Agent.Runtime.SubAgents
{
    public class BestPracticeScannerAgent : SubAgent
    {
        protected override string SystemPrompt { get; } = $@"You are an agent that will explore an unexplored graph database.";
        private GraphDBQueryAgent _graphDBQueryAgent { get; }
        private ILogger<BestPracticeScannerAgent> _logger { get; }

        private const string ScanningMessage = "Scanning for best practices...";

        private static List<string> BestPractices = [
            "Use managed identities for Azure resources",
            "Don't use 'test' in the name",
            "Resource names should be under 20 characters",
            "Don't use random characters in resource names; at least make sure it's a reproducible hash"
        ];

        public BestPracticeScannerAgent(GraphDBQueryAgent graphDBQueryAgent, IChatClient chatClient, ILogger<BestPracticeScannerAgent> logger) : base("BestPracticeScannerAgent", chatClient)
        {
            _graphDBQueryAgent = graphDBQueryAgent;
            _logger = logger;
        }

        public override IList<AITool> Tools()
        {
            return [
                AIFunctionFactory.Create(_graphDBQueryAgent.Ask),
                AIFunctionFactory.Create(Scan)
            ];
        }

        /// <summary>
        /// Figure out which best practices were not met. If any weren't met, pass a message to the user.
        /// We should spin up a BestPracticeAgent subchat, but notify the user in the main chat.
        /// </summary>
        /// <param name="additionalBestPractices">Additional best practices to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Message about unmet best practices, or null if all practices are met</returns>
        [Description("Scans for best practices. ")]
        public async Task<string?> Scan(
            [Description("Keep empty unless the user specifies additional things to check")] List<string> additionalBestPractices,
            [Description("Keep null")] CancellationToken? cancellationToken = null)
        {
            _logger.LogInformation(ScanningMessage);

            List<string> unmetConditionAnswers = new();
            bool allPracticesMet = true;

            // Scan for best practices
            const string prefix = "Verify all resources match this condition: ";
            foreach (string bestPractice in BestPractices.Concat(additionalBestPractices))
            {
                string question = prefix + bestPractice;
                string answer = await _graphDBQueryAgent.Ask(question);

                string? response = (await _chatClient.GetResponseAsync([new(ChatRole.User, $"Was this condition met: {bestPractice}\n Answer with only 'true' or 'false' based on this text: {answer}")])).Message.Text;
                bool succeeded = bool.TryParse(response, out bool result);
                if (!result)
                {
                    unmetConditionAnswers.Add(answer);
                }

                allPracticesMet &= result;
            }

            if (!allPracticesMet)
            {
                string? message = (await _chatClient.GetResponseAsync([new(ChatRole.User, "Explain all the best practices which weren't met: " + string.Join('\n', unmetConditionAnswers))])).Message.Text;
                _logger.LogCritical(message);
                return message;
            }

            return null;
        }
    }
}
