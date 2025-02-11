using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using OperationalAgentCore;
using OperationalAgentRuntime.Skills.DisableBasicAuth;
using System.Net;
using OperationalAgentRuntimeSK.LongRunningProcess;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Runtime.CompilerServices;
using OperationalAgentCore.Models;

namespace OperationalAgentRuntimeSK
{
    public class DurableFunctionEntrypoint
    {
        private readonly ILogger<Entrypoint> _logger;
        private readonly Kernel _kernel;
        private readonly TeamsConnector _teamsConnector;
        private readonly GitHubSettings _githubSettings;
        public bool SystemMessageAdded = false;
        private readonly AgentMode _agentMode; 

        public DurableFunctionEntrypoint(ILogger<Entrypoint> logger, IConfiguration config, Kernel kernel, TeamsConnector teamsConnector)
        {
            _logger = logger;
            _kernel = kernel;
            _teamsConnector = teamsConnector;
            var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
            string agentModeStr = config["AgentMode"] ?? string.Empty;
            _agentMode = Enum.TryParse<AgentMode>(agentModeStr, out var mode) ? mode : AgentMode.SREAgent;
            _githubSettings = azureSettings.Github;

            Interlocked.CompareExchange(ref GlobalStatic.TeamsConnector, teamsConnector, null);
        }

        [Function("DurableFunctionEntrypoint")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context, InputMessage message)
        {
            var outputs = new List<string>
            {
                await context.CallActivityAsync<string>(nameof(ProcessMessageAsync), message)
            };

            return outputs;
        }

        [Function(nameof(BestPracticesScanner_Timer))]
        public static async Task BestPracticesScanner_Timer(
            [TimerTrigger("*/30 * * * * *")] TimerInfo timer,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(BestPracticesScanner_Timer));

            string instanceId = "RunBestPracticesScanner_instance";

            // Check if an instance with the specified ID is already running  
            var existingInstance = await client.GetInstanceAsync(instanceId);
            if (existingInstance == null || existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Completed ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Failed ||
                existingInstance.RuntimeStatus == OrchestrationRuntimeStatus.Terminated)
            {
                StartOrchestrationOptions options = new StartOrchestrationOptions(instanceId);
                instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(BestPracticesScanner.RunBestPracticesScanner), options);
                logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);
            }
            else
            {
                logger.LogInformation($"Orchestration with ID = '{instanceId}' is already running.");
            }
        }

        public record IncomingApprovalRecord(string id, string approverName, bool isApproved);

        [Function(nameof(HttpApproveWorkflow))]
        public async Task<HttpResponseData> HttpApproveWorkflow(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "approve/{approvalId}")]
                HttpRequestData req,
                string approvalId,
                [FromBody] IncomingApprovalRecord approvalRecord,
                FunctionContext executionContext)
        {
            var (approvalDescriptor, approvalStatus) = GlobalStatic.ApprovalStatus.FirstOrDefault(v => v.Value.OperationId == approvalId);
            string resourceEntityName = _agentMode == AgentMode.ICM ? "incident" : "resource";
            if (approvalStatus.ProcessedTime != null)
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Already processed approval for ID = {approvalId}");
                return response;
            }
            else
            {
                approvalStatus = approvalStatus with
                {
                    DecisionMaker = approvalRecord.approverName,
                    ProcessedTime = DateTime.Now,
                    ApprovedTime = approvalRecord.isApproved ? DateTime.Now : null, // Consuming code uses presence of this field to determine approval
                };

                GlobalStatic.ApprovalStatus[approvalDescriptor] = approvalStatus;

                await ChatHistoryPersistency.ChatHistoryTransition(
                   async chatHistory =>
                   {
                       if (approvalRecord.isApproved)
                       {
                           await _teamsConnector.PostMessageAsync(new TeamsMessage($"✅ **Approved**: Operation **{approvalDescriptor.OperationName}** (ID: {approvalStatus.OperationId}) for {resourceEntityName} `{approvalDescriptor.ResourceId}` was approved by **{approvalStatus.DecisionMaker}** at {approvalStatus.ApprovedTime:yyyy-MM-dd HH:mm:ss}"));
                           chatHistory.AddSystemMessage($"Operation ID {approvalStatus.OperationId} of {approvalDescriptor.OperationName} is approved by {approvalStatus.DecisionMaker} for {resourceEntityName} id: {approvalDescriptor.ResourceId} , at {approvalStatus.ApprovedTime}. Now let's start operation execution");
                       }
                       else
                       {
                           await _teamsConnector.PostMessageAsync(new TeamsMessage($"❌ **Rejected**: Operation **{approvalDescriptor.OperationName}** (ID: {approvalStatus.OperationId}) for {resourceEntityName} {approvalDescriptor.ResourceId} was rejected by **{approvalStatus.DecisionMaker}** at {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
                           chatHistory.AddSystemMessage($"Approval for operation ID {approvalStatus.OperationId} of {approvalDescriptor.OperationName} is rejected by {approvalStatus.DecisionMaker} for {resourceEntityName} id: {approvalDescriptor.ResourceId}, at {DateTime.Now}. We will not proceed with operation execution.");
                       }

                       var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                       var result = await chatCompletionService.GetChatMessageContentAsync(
                           chatHistory,
                           executionSettings: new()
                           {
                               FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                           },
                           kernel: _kernel);

                       chatHistory.AddAssistantMessage(result.Content ?? string.Empty);
                       await _teamsConnector.PostMessageAsync(new TeamsMessage(content: result.Content ?? string.Empty));

                       return 0;
                   });

                // Return a simple status message  
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Successfully processed approval decision of workflow with ID = {approvalId}");
                return response;
            }
        }

        [Function(nameof(ProcessMessageAsync))]
        public async Task<string> ProcessMessageAsync(
            [ActivityTrigger] InputMessage message,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(ProcessMessageAsync));

            return await ChatHistoryPersistency.ChatHistoryTransition(
                async chatHistory =>
                {
                    if (message != null &&
                        !string.IsNullOrEmpty(message.User))
                    {
                        if ((message.User.Contains("balam", StringComparison.OrdinalIgnoreCase) || message.User.Contains("bilal alam", StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!SystemMessageAdded)
                            {
                                SystemMessageAdded = true;
                                chatHistory.AddSystemMessage($"{message.User} (Technical Fellow) has joined. As a senior technical leader: 1) Great their presence if not already done 2) Provide a concise technical executive summary 3) Address the question. Note: These messages will always include the username '{message.User}', if it doesn't these rules don't apply");
                            }
                            else
                            {
                                message.Content = "User: Bilal Alam - " + message.Content;
                            }
                        }
                        else if (message.User.Equals("icm_automation", StringComparison.OrdinalIgnoreCase))
                        {
                            message.Content = "source : icm_automation - " + message.Content;
                        }
                    }

                    if (message != null)
                    {
                        chatHistory.AddUserMessage(message.Content);
                    }

                    // Load tracked app states before adding user message
                    var trackedStates = TrackedActionHelper.GetActions(type: ActionType.AppStateTracking)
                        .OrderByDescending(a => a.Timestamp)
                        .DistinctBy(a => a.Metadata["name"])
                        .ToList();

                    _logger.LogInformation("User > " + message);
                    FunctionChoiceBehaviorOptions options = new() { AllowParallelCalls = true };

                    // not in core yet, so added manually
                    _kernel.Plugins.AddFromObject(new PlanManagementPlugin(client, logger));

                    var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                    var result = await chatCompletionService.GetChatMessageContentAsync(
                        chatHistory,
                        executionSettings: new()
                        {
                            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                        },
                        kernel: _kernel);

                    IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(result);

                    foreach (FunctionCallContent functionCall in functionCalls)
                    {
                        FunctionResultContent resultContent = await functionCall.InvokeAsync(_kernel);

                        chatHistory.Add(resultContent.ToChatMessage());
                    }

                    Console.WriteLine("Assistant > " + result);
                    chatHistory.AddMessage(result.Role, result.Content ?? string.Empty);

                    var teamsContent = result.Content.Replace("\"", "");
                    // Send to Teams
                    await _teamsConnector.PostMessageAsync(new TeamsMessage(content: teamsContent));

                    return result.ToString();
                });
        }

        [Function("DurableFunctionEntrypoint_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("Function1_HttpStart");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<InputMessage>(requestBody);

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(DurableFunctionEntrypoint), data);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        [Function(nameof(GitHubOAuthCallback))]
        public async Task<HttpResponseData> GitHubOAuthCallback(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "github-callback")]
            HttpRequestData req,
            [DurableClient] DurableTaskClient client)
        {
            try
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                var code = queryParams["code"];
                var state = queryParams["state"];

                if (string.IsNullOrEmpty(code))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await errorResponse.WriteStringAsync("Missing code parameter");
                    return errorResponse;
                }

                // Exchange code for access token
                // var tokenResult = await ExchangeCodeForToken(code);
                await GitHubTokenManager.SaveTokenAsync("test");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await _teamsConnector.PostMessageAsync(new TeamsMessage("Github Authenticated Successfully, I'll start monitoring the repository!"));
                await ChatHistoryPersistency.ChatHistoryTransition(
                    async history =>
                    {
                        history.AddAssistantMessage("Authorization with Github complete I now have access to the repos via code analyzer plugin. I should start my next steps now");
                        return 0;
                    });
                await response.WriteAsJsonAsync(new
                {
                    message = "Successfully connected Github to Operations Agent",//tokenResult.AccessToken,
                });

                string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                    nameof(DurableFunctionEntrypoint), new InputMessage()
                    {
                        Content = "Please continue with the action to monitor the github repository or open an issue"
                    });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing GitHub OAuth callback");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred");
                return errorResponse;
            }
        }

        [Function(nameof(ResetGithubAuthFlow))]
        public async Task<HttpResponseData> ResetGithubAuthFlow(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "resetgh")]
            HttpRequestData req,
            FunctionContext executionContext)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            await GitHubTokenManager.DeleteTokenAsync();
            await response.WriteAsJsonAsync(new
            {
                message = "Reset successful",
            });

            return response;
        }

        private async Task<TokenProcessResult> ExchangeCodeForToken(string code)
        {
            using var client = new HttpClient();
            var response = await client.PostAsync(
                "https://github.com/login/oauth/access_token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
            {"client_id", _githubSettings.ClientId},
            {"client_secret", _githubSettings.ClientSecret},
            {"code", code}
                }));

            var content = await response.Content.ReadAsStringAsync();
            var values = System.Web.HttpUtility.ParseQueryString(content);

            // store to account that the token flow is done
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "ghtoken.txt");
            File.WriteAllText(filePath, content);

            return new TokenProcessResult()
            {
                AccessToken = values["access_token"],
                ExpiresIn = int.Parse(values["expires_in"] ?? "0")
            };
        }

        /*
         * TeamsIncoming_HttpStart proxies to DurableFunctionEntrypoint,
         * but with an intermediate step that attempts to ignore irrelevant messages.
         * 
         * It can be bypassed by calling DurableFunctionEntrypoint directly 
         * or by starting message with 'agent'. A different way to solve this
         * problem is by requiring the end user to tag the agent directly
         * if we create a Teams app identity rather than generally
         * monitoring the chat.
         */
        [Function("TeamsIncoming_HttpStart")]
        public static async Task<HttpResponseData> TeamsIncoming_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("Function1_HttpStart");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonConvert.DeserializeObject<InputMessage>(requestBody);

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(TeamsIncomingEntrypoint), data);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        [Function(nameof(TeamsIncomingEntrypoint))]
        public static async Task<List<string>> TeamsIncomingEntrypoint(
            [OrchestrationTrigger] TaskOrchestrationContext context, InputMessage message)
        {
            var shouldSkip = await context.CallActivityAsync<bool>(nameof(CheckWhetherWeShouldSkipMessage), message);
            if (shouldSkip)
            {
                return ["Agent did not react to this message because it did not seem relevant. Start message with 'agent' to bypass."];
            }
            var outputs = new List<string>
            {
                await context.CallActivityAsync<string>(nameof(ProcessMessageAsync), message)
            };

            return outputs;
        }

        [Function(nameof(CheckWhetherWeShouldSkipMessage))]
        public async Task<bool> CheckWhetherWeShouldSkipMessage(
            [ActivityTrigger] InputMessage message,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            // hardcoded fallback
            if (message.Content.StartsWith("agent", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return await ChatHistoryPersistency.ChatHistoryTransition(
                   async chatHistory =>
                   {
                       var subsetChatHistory = new ChatHistory();
                       subsetChatHistory.AddSystemMessage("""
                            You are an Azure App Service Operations Expert participating in a group chat.
                            Members of the group may consult you for ad-hoc investigations, to get status of
                            your prior work, etc. Members of the group may also discuss other unrelated topics
                            with one another. Given recent chat messages, interpret the new message and decide if
                            the message is relevant for / directed to you (Azure App Service Operations Expert), or
                            if you should remain silent.

                            Return a single string "relevant" or "notrelevant".
                        """);

                       // These chat histories should probably be just the Teams messages
                       // rather than metadata chat messages we add
                       subsetChatHistory.AddUserMessage(System.Text.Json.JsonSerializer.Serialize(
                           chatHistory.Where(c => c.Role != AuthorRole.System).TakeLast(Math.Min(10, chatHistory.Count))));

                       subsetChatHistory.AddUserMessage($"New message from user {message.User}: '{message.Content}'");

                       // May be better to use simple chat completion instead of kernel-provided tools
                       var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
                       var result = await chatCompletionService.GetChatMessageContentAsync(
                           subsetChatHistory,
                           executionSettings: new()
                           {
                               FunctionChoiceBehavior = FunctionChoiceBehavior.None()
                           },
                           kernel: _kernel);

                       // If LLM hallucinates or doesn't give clear answer,
                       // let's be safe and assume message is relevant
                       return result.Content == "notrelevant";
                   });
        }

        [Function(nameof(TestSendToTeams))]
        public async Task<HttpResponseData> TestSendToTeams(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(TestSendToTeams));

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            await this._teamsConnector.PostMessageAsync(new TeamsMessage(requestBody));

            return req.CreateResponse(HttpStatusCode.OK);
        }
    }

    public class TokenProcessResult
    {
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }
}