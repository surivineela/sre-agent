using Agent.Core.Interfaces;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Azure.ResourceManager.AppService.Models;

namespace Agent.Runtime.SubAgents.AppReliabilityAgent
{
    [DurableTask]
    public class AppReliabilityAgent : GenericAgentOrchestrator<AppReliabilityAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, AppReliabilityAgentInput agentInput)
        {
            try
            {
                var log = context.CreateReplaySafeLogger<AppReliabilityAgent>();

                // Initial planning phase: generate plan (e.g. list of apps to update)
                List<ChatMessage> chatHistory = await context.CallReliabilityPlanActivityAsync(agentInput.Input);

                var introMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(AppReliabilitySendIntroActivity)), agentInput);
                chatHistory.Add(introMessage);

                // Prompt the user for custom health check
                var customHealthCheckMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(AppReliabilityQueryHealthCheckActivity)), agentInput);
                chatHistory.Add(customHealthCheckMessage);

                // Prompt the user for custom auto heal rules
                var customAutoHealMessage = await context.CallActivityAsync<ChatMessage>(new TaskName(nameof(AppReliabilityQueryAutoHealActivity)), agentInput);
                chatHistory.Add(customAutoHealMessage);

                // Optionally, send a summary and start the execution (this activity could be similar to your SendSummaryAndStartActivity)
                chatHistory = await context.CallSendSummaryAndStartActivityAsync(
                   new GetNextActionInput
                   {
                       ChatMessages = chatHistory,
                       StepCounter = 0,
                       ToolSignatures = [],
                   });

                // Run the generic reasoning loop to get actions and process function calls until the plan is complete.
                chatHistory = await RunReasoningLoopAsync(
                    context,
                    chatHistory,
                    agentInput.ToolSignatures,
                    agentInput.Context,
                    log);

                return "success";
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }

    [DurableTask]
    public class AppReliabilitySendIntroActivity : TaskActivity<AppReliabilityAgentInput, ChatMessage>
    {
        private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

        public AppReliabilitySendIntroActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
        {
            _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
        }

        public override async Task<ChatMessage> RunAsync(TaskActivityContext context, AppReliabilityAgentInput agentInput)
        {
            StringBuilder introMessage = new StringBuilder("""
                I can update these applications to incorporate best reliability practices. I'll wait 30 seconds between each app and monitor its health during that time.  

                #### Application Updates  

                """);

            foreach (var app in agentInput.Input.AppsInViolation)
            {
                introMessage.AppendLine($"**{app.ResourceId}**:");
                introMessage.AppendLine($"Number Of Workers {app.NumberOfWorkers} -> 3");
                introMessage.AppendLine($"AlwaysOn {app.AlwaysOnEnabled} -> true");
                introMessage.AppendLine($"Autoheal {app.AutoHealEnabled} -> true");
                introMessage.AppendLine($"Healthcheck {app.HealthCheckEnabled} -> true");
                introMessage.AppendLine();
            }

            introMessage.AppendLine();
            introMessage.AppendLine("Would you like me to proceed as planned above? I can trigger an approval flow.");

            var newMessage = new ChatMessage(ChatRole.Assistant, introMessage.ToString());

            await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                agentInput.Context,
                context.InstanceId,
                newMessage);

            return newMessage;
        }
    }

    [DurableTask]
    public class AppReliabilityQueryHealthCheckActivity : TaskActivity<AppReliabilityAgentInput, ChatMessage>
    {
        private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;

        public AppReliabilityQueryHealthCheckActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
        {
            _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
        }

        public override async Task<ChatMessage> RunAsync(TaskActivityContext context, AppReliabilityAgentInput agentInput)
        {
            StringBuilder healthCheckAsk = new StringBuilder("""
                If you want to provide a custom health check path to update the healthcheckpath property for each of the apps, please provide it now in the form of '/<INSERT PATH>.
                Otherwise the default healthcheckpath property for each app to monitor will be '/health'.
                If you want to disable healthCheckPath, then I'll set healthCheckPath to null.

                 Based off your answers, I will make use the healthcheckpath for each of the apps that need to be updated using the UpdateHealthCheck tool
                """);

            var newMessage = new ChatMessage(ChatRole.Assistant, healthCheckAsk.ToString());

            await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                agentInput.Context,
                context.InstanceId,
                newMessage);

            return newMessage;
        }
    }

    [DurableTask]
    public class AppReliabilityQueryAutoHealActivity : TaskActivity<AppReliabilityAgentInput, ChatMessage>
    {
        private readonly IAgentOutboundCommunicationService _subAgentOutboundCommunicationService;
        
        public AppReliabilityQueryAutoHealActivity(IAgentOutboundCommunicationService subAgentOutboundCommunicationService)
        {
            _subAgentOutboundCommunicationService = subAgentOutboundCommunicationService;
        }

        public override async Task<ChatMessage> RunAsync(TaskActivityContext context, AppReliabilityAgentInput agentInput)
        {
            StringBuilder healthCheckAsk = new StringBuilder("""
                If you want to enable autoheal on your apps, here are the possible autoheal configurations you can update your apps' autoheal rules to when you want to enable it.
                Possible Auto heal triggers are based off the Azure.ResourceManager.AppService.Models.AutoHealTriggers. They are listed as:
                    - RequestsBasedTrigger requests: a rule based on total requests on a minimum interval of time 
                    - int privateBytesInKB: a rule based on private bytes
                    - List<StatusCodesBasedTrigger> statusCodes: a rule based on status codes
                    - SlowRequestsBasedTrigger slowRequests: a rule based on request execution time
                    - List<SlowRequestsBasedTrigger> slowRequestsWithPath: a rule based on multiple Slow Requests Rule with path
                    - List<StatusCodesRangeBasedTrigger> statusCodesRange: a rule based on status codes ranges
                Possible Auto heal actions are based off the Azure.ResourceManager.AppService.Models.AutoHealActions. They have an AutoHealActionType, an AutoHealCustomAction, and a minimum process execution time.
                    - AutoHealActionType's possible values: Recycle, LogEvent, CustomAction
                    - AutoHealCustomAction: defines an executable to be ran and parameters for the executable
                    - string minProcessExecutionTime: minimum time the process must execute before taking the action

                 Based off your answers, I will make an AutoHealRule object for each of the apps that need to be updated using the UpdateAutoHeal tool
                """);

            var newMessage = new ChatMessage(ChatRole.Assistant, healthCheckAsk.ToString());

            await _subAgentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(
                agentInput.Context,
                context.InstanceId,
                newMessage);

            return newMessage;
        }
    }
}
