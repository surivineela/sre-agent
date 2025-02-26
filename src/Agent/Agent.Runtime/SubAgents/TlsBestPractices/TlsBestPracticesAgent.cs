using System.Text;
using Agent.Plugins;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.TlsBestPractices
{
    public static class DurableHelper
    {
        // For some reason, the helper is internal

        public static DurableTaskRegistry AddAllGeneratedTasks(DurableTaskRegistry builder)
        {
            return builder.AddAllGeneratedTasks();
        }
    }

    public class TlsBestPracticesInput
    {
        public string DesiredVersion { get; set; }

        public List<Core.Models.TlsStatus> AppsInViolation { get; set; }
    }

    public class NextActionInput
    {
        public string OperationId { get; set; }
        public List<ChatMessage> ChatMessages { get; set; }
        public int StepCounter { get; set; }
    }

    [DurableTask]
    public class TlsBestPracticesAgent : TaskOrchestrator<TlsBestPracticesInput, string>
    {
        public TlsBestPracticesAgent()
        {

        }

        public async override Task<string> RunAsync(TaskOrchestrationContext context, TlsBestPracticesInput input)
        {
            ILogger logger = context.CreateReplaySafeLogger<TlsBestPracticesAgent>();
            
            List<ChatMessage> chatHistory = await context.CallTlsPlanActivityAsync(input);

            // TODO - generate HTML table and send intro message
            // TODO - do approval flow

            chatHistory = await context.CallSendSummaryAndStartActivityAsync(new NextActionInput { ChatMessages = chatHistory });

            int stepCount = 0;
            bool done = false;

            while (done == false)
            {
                stepCount++;

                var nextActionInput = new NextActionInput
                {
                    //OperationId = context.InstanceId,
                    ChatMessages = chatHistory,
                    StepCounter = stepCount
                };

                var nextAction = await context.CallGetNextActionActivityAsync(nextActionInput);
                chatHistory.Add(nextAction);

                // parallel function calls is currently disabled so this is safe
                var functionCall = nextAction.Contents.OfType<FunctionCallContent>().Single();

                if (functionCall.Name == nameof(ControlFlowPluginDefinition.MarkPlanComplete))
                {
                    done = true;
                    var resultContent = new FunctionResultContent(functionCall.CallId, "Plan marked as complete.");
                    chatHistory.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
                }
                else if (functionCall.Name == nameof(ControlFlowPluginDefinition.Wait))
                {
                    var resultContent = new FunctionResultContent(functionCall.CallId, "Wait is complete.");
                    chatHistory.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
                }
                else if (functionCall.Name == nameof(ControlFlowPluginDefinition.NotifyUser))
                {
                    var resultContent = new FunctionResultContent(functionCall.CallId, "User notified.");
                    chatHistory.Add(new ChatMessage(ChatRole.Tool, [resultContent]));
                }
                else
                {
                    var executionResult = await context.CallExecuteNextActionActivityAsync(nextActionInput);
                    chatHistory.Add(executionResult);
                }
            }

            return "success";
        }
    }

    public class TlsBestPracticesAgentTools
    {
        public List<AIFunction> Functions { get; set; } = new List<AIFunction>();

        public TlsBestPracticesAgentTools(
            IMetricsPlugin metricsPlugin,
            IArmPlugin armPlugin
            )
        {
            var metricsPluginDefinition = new MetricsPluginDefinition(metricsPlugin);
            Functions.Add(AIFunctionFactory.Create(metricsPluginDefinition.GetSuccessfulRequestVolumeAsync));

            var armPluginDefinition = new ArmPluginDefinition(armPlugin);
            Functions.Add(AIFunctionFactory.Create(armPluginDefinition.SetMinimumTlsVersion));

            var controlFlowPluginDefinition = new ControlFlowPluginDefinition();
            Functions.Add(AIFunctionFactory.Create(controlFlowPluginDefinition.Wait));
            Functions.Add(AIFunctionFactory.Create(controlFlowPluginDefinition.MarkPlanComplete));
            Functions.Add(AIFunctionFactory.Create(controlFlowPluginDefinition.NotifyUser));
        }
    }

    [DurableTask]
    public class GetNextActionActivity : TaskActivity<NextActionInput, ChatMessage>
    {
        private readonly IChatClient _chatClient;
        private readonly TlsBestPracticesAgentTools _tools;
        private readonly ILogger<GetNextActionActivity> _logger;

        public GetNextActionActivity(
            [FromKeyedServices("no-function-invocation")] IChatClient chatClient, 
            TlsBestPracticesAgentTools tools, 
            ILogger<GetNextActionActivity> logger)
        {
            _chatClient = chatClient;
            _tools = tools;
            _logger = logger;
        }

        public async override Task<ChatMessage> RunAsync(TaskActivityContext context, NextActionInput input)
        {
            var chatHistory = input.ChatMessages;
            var chatOptions = new ChatOptions 
            { 
                Tools = new List<AITool>(_tools.Functions), 
                ToolMode = ChatToolMode.RequireAny,
                AdditionalProperties = new ()
                {
                    ["AllowParallelToolCalls"] = false,
                }
            };
            var response = await _chatClient.GetResponseAsync(chatHistory, chatOptions);

            //var sb = new StringBuilder();
            //sb.AppendLine($"Function call {functionCallContent.Name}({functionCallContent.CallId}) invoked with arguments:");

            //foreach (var arg in functionCallContent.Arguments)
            //{
            //    sb.AppendLine($"  {arg.Key}: {arg.Value}");
            //}

            //logger.LogInformation(sb.ToString());

            return response.Message;
        }
    }

    [DurableTask]
    public class ExecuteNextActionActivity : TaskActivity<NextActionInput, ChatMessage>
    {
        private readonly IChatClient _chatClient;
        private readonly TlsBestPracticesAgentTools _tools;
        private readonly ILogger<ExecuteNextActionActivity> _logger;
        public ExecuteNextActionActivity(
            IChatClient chatClient,
            TlsBestPracticesAgentTools tools,
            ILogger<ExecuteNextActionActivity> logger)
        {
            _chatClient = chatClient;
            _tools = tools;
            _logger = logger;
        }
        public async override Task<ChatMessage> RunAsync(TaskActivityContext context, NextActionInput input)
        {
            var action = input.ChatMessages.Last();
            var call = action.Contents.Single() as FunctionCallContent;

            var matchingTool = _tools.Functions.Single(x => x.Name == call.Name);
            var invokeResult = await matchingTool.InvokeAsync(call.Arguments);
            var resultContent = new FunctionResultContent(call.CallId, invokeResult);

            return new ChatMessage(ChatRole.Tool, [resultContent]);
        }
    }

        [DurableTask]
    public class TlsPlanActivity : TaskActivity<TlsBestPracticesInput, List<ChatMessage>>
    {
        private readonly IChatClient chatClient;

        public TlsPlanActivity(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }

        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, TlsBestPracticesInput input)
        {
            var existingAppsDetails = string.Join(Environment.NewLine,
                input.AppsInViolation.Select(x => $"{x.ResourceId} has a current minimum TLS version of {x.MinimumTlsVersion}"));

            var path = Path.Combine("SubAgents", "TlsBestPractices", "TlsBestPracticesPlan.txt");
            var systemPrompt = File.ReadAllText(path).Replace("{{desiredVersion}}", input.DesiredVersion);
            var userMessage = $"Here are the apps that need updating: {existingAppsDetails}";

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
                ];

            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.Message);

            return messages;
        }
    }

    [DurableTask]
    public class SendSummaryAndStartActivity : TaskActivity<NextActionInput, List<ChatMessage>>
    {
        private readonly IChatClient chatClient;
        private readonly ILogger<SendSummaryAndStartActivity> logger;

        public SendSummaryAndStartActivity(IChatClient chatClient, ILogger<SendSummaryAndStartActivity> logger)
        {
            this.chatClient = chatClient;
            this.logger = logger;
        }

        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, NextActionInput input)
        {
            var chatMessages = input.ChatMessages;

            chatMessages.Add(new ChatMessage(ChatRole.User, """
                Now that the plan is complete, can you send me a 3 sentence summary of the steps you'll take?
                """
            ));

            var response = await chatClient.GetResponseAsync(chatMessages);
            chatMessages.Add(response.Message);

            // TODO
            //await PostTlsMessageToTeams(new TeamsMessage(response.Message.Text), client, executionContext);

            chatMessages.Add(new ChatMessage(ChatRole.User, "Great, you can start executing the plan now."));

            return chatMessages;
        }
    }
        
}
