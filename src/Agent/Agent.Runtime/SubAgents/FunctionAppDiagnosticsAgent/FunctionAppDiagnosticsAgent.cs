using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agent.Plugins.Implementation;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;
using Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent
{
    [DurableTask]
    class FunctionAppDiagnosticsAgent : GenericAgentOrchestrator<FunctionAppDiagnosticsAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, FunctionAppDiagnosticsAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<FunctionAppDiagnosticsAgent>();

            try
            {
                // Initial planning phase: generate plan
                List<ChatMessage> chatHistory = await context.CallFunctionAppDiagnosticsAgentPlanActivityAsync(agentInput);

                var monitoringMessage = $"Thank you for the confirmation, I will now attempt to diagnose issues with {agentInput.FunctionAppResourceId}";

                // Send a summary and start the execution
                chatHistory = await context.CallSendSummaryAndStartActivityAsync(
                     new GetNextActionInput
                     {
                         ChatMessages = chatHistory,
                         StepCounter = 0,
                         ToolSignatures = [],
                     });

                // Get only the diagnostic agent tool signatures
                var diagnosticToolSignatures = agentInput.ToolSignatures.TryGetValue(
                    FunctionAppDiagnosticsAgentFactory.FunctionAppDiagnosticsAgentKey, 
                    out var signatures) ? signatures : [];

                // Run the generic reasoning loop to get actions and process function calls until the plan is complete
                //var deciderChatHistory = await RunReasoningLoopAsync(
                //    context,
                //    chatHistory,
                //    diagnosticToolSignatures,
                //    log,
                //    new Guid());

                // Call the routing plan activity to determine the next agent to call
                var routingOutput = await context.CallFunctionAppDiagnosticsRoutingPlanActivityAsync((chatHistory, agentInput.FunctionAppResourceId));

                var configurationCheckToolSignatures = agentInput.ToolSignatures.TryGetValue(FunctionAppDiagnosticsAgentFactory.FunctionAppConfigurationCheckAgentKey,
                                                        out var configSignatures) ? configSignatures : [];
                var deploymentChecksToolSignatures = agentInput.ToolSignatures.TryGetValue(
                    FunctionAppDiagnosticsAgentFactory.FunctionAppDeploymentChecksAgentKey,
                    out var deploymentSignatures) ? deploymentSignatures : [];
                string response = string.Empty;
                // Use the routing output to determine the next steps
                switch (routingOutput.AgentType)
                {
                    case FunctionAppDiagnosticsAgentType.NotAFunctionApp:
                        // Not a function app, just return
                        log.LogInternalInformation("Resource is not a Function App");
                        response = "notAFunctionApp";
                        break;
                        
                    case FunctionAppDiagnosticsAgentType.FunctionAppConnectivityAgent:
                        // Call FunctionApp Connectivity Agent
                        log.LogInternalInformation("Routing to FunctionAppConnectivityAgent");
                        var connectivityToolSignatures = agentInput.ToolSignatures.TryGetValue(
                            FunctionAppDiagnosticsAgentFactory.FunctionAppConnectivityAgentKey, 
                            out var connectivitySignatures) ? connectivitySignatures : [];                        
                        var connectivityResult = await context.CallFunctionAppConnectivityAgentAsync(
                            new FunctionAppConnectivityAgentInput(
                                agentInput.FunctionAppResourceId, 
                                connectivityToolSignatures, 
                                agentInput.ThreadId));
                        response = connectivityResult;
                        break;
                        
                    case FunctionAppDiagnosticsAgentType.FunctionAppExecutionFailuresAgent:
                        // Call FunctionApp Execution Failures Agent with a NEW thread ID
                        log.LogInternalInformation("Routing to FunctionAppExecutionFailuresAgent");
                        var executionToolSignatures = agentInput.ToolSignatures.TryGetValue(
                            FunctionAppDiagnosticsAgentFactory.FunctionAppExecutionFailuresAgentKey, 
                            out var executionSignatures) ? executionSignatures : [];
                        
                        var executionResult = await context.CallFunctionAppExecutionFailuresAgentAsync(
                            new FunctionAppExecutionFailuresAgentInput(
                                agentInput.FunctionAppResourceId, 
                                executionToolSignatures,
                                agentInput.ThreadId));
                        response = executionResult;
                        break;
                        
                    default:
                        log.LogInternalWarning("Unknown agent type returned: {AgentType}", routingOutput.AgentType);
                        response = "unknown";
                        break;
                }
                var configResult2 = await context.CallFunctionAppConfigurationCheckAgentAsync(
                    new FunctionAppConfigurationCheck.FunctionAppConfigurationCheckAgentInput(
                        agentInput.FunctionAppResourceId,
                        configurationCheckToolSignatures,
                        agentInput.ThreadId));
                var deploymentResult = await context.CallFunctionAppDeploymentChecksAgentAsync(
                    new FunctionAppDeploymentChecksAgent.FunctionAppDeploymentChecksAgentInput(
                        agentInput.FunctionAppResourceId,
                        deploymentChecksToolSignatures,
                        agentInput.ThreadId));
                return response;
            }
            catch (Exception ex)
            {
                log.LogInternalError(ex, "An error occurred while running the FunctionAppDiagnosticAgent.");
                return "failure";
            }
        }
    }
}
