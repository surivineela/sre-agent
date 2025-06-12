using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Extensions;
using Castle.Core.Logging;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using Agent.Plugins.Interface;

namespace Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent
{
    [DurableTask]
    public class FunctionAppDiagnosticsAgentPlanActivity(IChatClient chatClient) : TaskActivity<FunctionAppDiagnosticsAgentInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient = chatClient;

        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, FunctionAppDiagnosticsAgentInput input)
        {
            var functionAppDetails = $@"Investigate and diagnose issues with my function app: {input.FunctionAppResourceId}";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "FunctionAppDiagnosticsAgent", "FunctionAppDiagnosticsAgentPlan.txt");
            string? systemPrompt;
            try
            {
                systemPrompt = await File.ReadAllTextAsync(path);
            }
            catch (Exception)
            {
                // Handle exception, e.g., log the error or set a default value for systemPrompt
                systemPrompt = "Default system prompt message for Function App Diagnostic Agent.";
            }

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, functionAppDetails)
            ];

            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());
            return messages;
        }

        [DurableTask]
        public class FunctionAppDiagnosticsRoutingPlanActivity(
            IChatClient chatClient,
            ILogger<FunctionAppDiagnosticsAgentPlanActivity.FunctionAppDiagnosticsRoutingPlanActivity> logger,
            IFunctionAppExecutionFailuresPlugin functionAppExecutionFailuresPlugin) : TaskActivity<(List<ChatMessage> ChatHistory, string FunctionAppResourceId), FunctionAppDiagnosticsRoutingOutput>
        {
            private readonly IChatClient chatClient = chatClient;
            private readonly ILogger<FunctionAppDiagnosticsRoutingPlanActivity> _logger = logger;
            private readonly IFunctionAppExecutionFailuresPlugin _functionAppExecutionFailuresPlugin = functionAppExecutionFailuresPlugin;

            public override async Task<FunctionAppDiagnosticsRoutingOutput> RunAsync(TaskActivityContext context, (List<ChatMessage> ChatHistory, string FunctionAppResourceId) input)
            {
                var chatHistory = input.ChatHistory;
                var functionAppResourceId = input.FunctionAppResourceId;
                
                try
                {
                    // Check if the resource is a Function App
                    bool isFunctionApp = await _functionAppExecutionFailuresPlugin.IsFunctionApp(functionAppResourceId);
                    
                    if (!isFunctionApp)
                    {
                        // Not a function app
                        _logger.LogInternalInformation("Resource is not a Function App");
                        return new FunctionAppDiagnosticsRoutingOutput(true, FunctionAppDiagnosticsAgentType.NotAFunctionApp);
                    }
                    
                    // Check if the Function App has host runtime errors
                    bool hasHostRuntimeErrors = await _functionAppExecutionFailuresPlugin.HasHostRuntimeErrors(functionAppResourceId);
                    
                    if (hasHostRuntimeErrors)
                    {
                        // Route to connectivity agent for host runtime errors
                        _logger.LogInternalInformation("Host runtime errors detected. Routing to FunctionAppConnectivityAgent");
                        return new FunctionAppDiagnosticsRoutingOutput(true, FunctionAppDiagnosticsAgentType.FunctionAppConnectivityAgent);
                    }
                    else
                    {
                        // Route to execution failures agent for function-level errors
                        _logger.LogInternalInformation("No host runtime errors detected. Routing to FunctionAppExecutionFailuresAgent");
                        return new FunctionAppDiagnosticsRoutingOutput(true, FunctionAppDiagnosticsAgentType.FunctionAppExecutionFailuresAgent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error checking function app status for {functionAppResourceId}");
                    
                    // Fall back to original behavior if there's an error
                    // Get the final agent response
                    string finalAgentResponse = chatHistory.LastOrDefault(m => m.Role == ChatRole.Assistant)?.Text ?? string.Empty;

                    // Initialize result variables
                    string result = "";
                    bool resultParsed = false;
                    var agentType = FunctionAppDiagnosticsAgentType.FunctionAppExecutionFailuresAgent; // Default

                    try
                    {
                        // Extract the JSON string from the response
                        string jsonResponse = finalAgentResponse;
                        // Check if we need to extract a JSON substring from a larger text
                        int jsonStartIndex = finalAgentResponse.IndexOf('{');
                        int jsonEndIndex = finalAgentResponse.LastIndexOf('}');

                        if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
                        {
                            jsonResponse = finalAgentResponse.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                        }

                        var agentResult = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                        // If the result indicates a valid agent type, call the appropriate agent
                        if (agentResult.TryGetProperty("result", out var resultElement))
                        {
                            result = resultElement.GetString() ?? string.Empty;
                            resultParsed = true;
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        // If JSON deserialization fails, use LLM to analyze the chat history
                        try
                        {
                            // Format chat history for LLM analysis
                            var historyString = string.Join("\n\n", chatHistory.Select(m => $"{m.Role}: {m.Text}"));

                            // Create prompt to extract the result
                            var systemPrompt = await File.ReadAllTextAsync(
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "FunctionAppDiagnosticsAgent", "FunctionAppDiagnosticsExtractResultPrompt.txt"));

                            // Prepare messages for LLM
                            List<ChatMessage> extractionMessages = [
                                new ChatMessage(ChatRole.System, systemPrompt),
                                new ChatMessage(ChatRole.User, historyString)
                            ];

                            // Get LLM response to extract JSON
                            var response = await this.chatClient.GetResponseAsync(extractionMessages);
                            string extractedResponse = response.Messages.LastOrDefault()?.Text ?? "";

                            // Try to parse the extracted JSON
                            int jsonStartIndex = extractedResponse.IndexOf('{');
                            int jsonEndIndex = extractedResponse.LastIndexOf('}');

                            if (jsonStartIndex >= 0 && jsonEndIndex > jsonStartIndex)
                            {
                                string jsonResponse = extractedResponse.Substring(jsonStartIndex, jsonEndIndex - jsonStartIndex + 1);
                                var extractedResult = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                                if (extractedResult.TryGetProperty("result", out var resultElement))
                                {
                                    result = resultElement.GetString() ?? string.Empty;
                                    resultParsed = true;
                                    _logger.LogInternalInformation($"Successfully extracted result: {result}");
                                }
                            }
                        }
                        catch (Exception extractEx)
                        {
                            _logger.LogInternalError(extractEx, "Failed to extract result using LLM analysis");
                        }
                    }

                    // Map the string result to enum value
                    if (resultParsed)
                    {
                        switch (result)
                        {
                            case "NotAFunctionApp":
                                agentType = FunctionAppDiagnosticsAgentType.NotAFunctionApp;
                                break;
                            case "FunctionAppConnectivityAgent":
                                agentType = FunctionAppDiagnosticsAgentType.FunctionAppConnectivityAgent;
                                break;
                            case "FunctionAppExecutionFailuresAgent":
                                agentType = FunctionAppDiagnosticsAgentType.FunctionAppExecutionFailuresAgent;
                                break;
                            default:
                                resultParsed = false; // Unknown result is treated as parsing failure
                                agentType = FunctionAppDiagnosticsAgentType.FunctionAppExecutionFailuresAgent; // Default
                                _logger.LogInternalWarning($"Unknown agent type returned: {result}. Defaulting to FunctionAppExecutionFailuresAgent");
                                break;
                        }
                    }

                    return new FunctionAppDiagnosticsRoutingOutput(resultParsed, agentType);
                }
            }
        }
    }
}
