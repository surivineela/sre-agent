// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Runtime.Helpers;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Action = Agent.Core.Models.Api.v1.Action;

namespace Agent.Runtime.SubAgents.Core
{
    public record RecordActionInput(
        Guid ActionId,
        Guid ThreadId,
        List<ChatMessage> ChatMessages,
        FunctionCallContent? FunctionCall,
        IReadOnlyList<string> ToolSignatures,
        ActionStatus Status
        );

    [DurableTask]
    public class RecordActionActivity : TaskActivity<RecordActionInput, Action?>
    {
        private readonly ILogger<RecordActionActivity> _logger;
        private readonly IChatClient _chatClient;
        private readonly IToolsRepository _toolsRepository;
        private readonly IThreadRepository _threadRepository;
        private readonly IAgentOutboundCommunicationService _outboundCommunicationService;

        public RecordActionActivity(ILogger<RecordActionActivity> logger,
            IChatClient chatClient,
            IToolsRepository toolsRepository,
            IThreadRepository threadRepository,
            IAgentOutboundCommunicationService outboundCommunicationService)
        {
            _logger = logger;
            _chatClient = chatClient;
            _toolsRepository = toolsRepository;
            _threadRepository = threadRepository;
            _outboundCommunicationService = outboundCommunicationService;
        }

        public override async Task<Action?> RunAsync(TaskActivityContext context, RecordActionInput input)
        {
            var toolSignatures = input.ToolSignatures;
            var targetFunction = input.FunctionCall!.Name;
            // Get all tools and find matching tool
            var aiFunctions = _toolsRepository.GetAllTools(toolSignatures).Select(_toolsRepository.FindAiFunction);
            var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == targetFunction);

            if (!ActionHelper.ToolShouldBeRecorded(matchingTool))
            {
                _logger.LogInternalInformation("[{ThreadId}] Function call {FunctionCallName} is not recorded", input.ThreadId, targetFunction);
                return null;
            }

            Guid actionId;
            string title;
            ActionStatus status = input.Status;
            if (ApprovalHelper.ToolRequiresApproval(matchingTool) && status == ActionStatus.Pending)
            {
                status = ActionStatus.PendingApproval;
            }

            if (input.ActionId != Guid.Empty)
            {
                actionId = input.ActionId;

                var existingAction = await _threadRepository.GetActionAsync(input.ThreadId, actionId);
                if (existingAction == null)
                {
                    _logger.LogInternalWarning("[{ThreadId}] Invalid action id {ActionId}. No existing action is found. Skipping record action.", input.ThreadId, actionId);
                    return null;
                }

                if (status == existingAction.Status)
                {
                    _logger.LogInternalInformation("[{ThreadId}] Action status for action id {ActionId} does not change. Skipping record action.", input.ThreadId, actionId);
                    return existingAction;
                }

                title = existingAction.Title;
            }
            else
            {
                actionId = Guid.NewGuid();
                // use model to generate a descriptive title
                var chatOptions = new ChatOptions
                {
                    Tools = [matchingTool.ToolFunction],
                    ToolMode = ChatToolMode.None,
                    Temperature = 0.3f,
                };

                //// if last message is a tool call, remove it to avoid bad request
                while (input.ChatMessages.Last().Contents.OfType<FunctionCallContent>().Count() > 0)
                {
                    input.ChatMessages.RemoveAt(input.ChatMessages.Count - 1);
                }

                input.ChatMessages.Add(
                    new ChatMessage(ChatRole.User, $"Generate a clear, descriptive title to describe the action being taken. The title should be plain text without markdown syntax" +
                    $"The function call content text will wrapped within #####FUNCTIONCALL#####." +
                    $"\n\n\n #####FUNCTIONCALL#####\n {JsonSerializer.Serialize(input.FunctionCall)}\n#####FUNCTIONCALL#####")
                );

                var response = await _chatClient.GetResponseAsync(input.ChatMessages, chatOptions);
                title = response.Text;
            }

            _logger.LogInternalInformation("[{ThreadId}] Recording action {ActionId} for function call {FunctionCallName}. Title: {Title}. Status {ActionStatus}.", input.ThreadId, actionId, targetFunction, title, status);

            var action = new Action(
                Id: actionId,
                Title: title,
                ToolName: targetFunction,
                TimeStamp: DateTime.UtcNow,
                Status: status,
                Severity: ActionSeverity.Critical
            );

            await _outboundCommunicationService.NotifyActionAsync(input.ThreadId, action);
            await _threadRepository.AddOrUpdateActionAsync(input.ThreadId, action);

            return action;
        }
    }

    public record GetActionDetailsInput(
        Guid ThreadId,
        Guid ActionId);

    [DurableTask]
    public class GetActionDetailsActivity : TaskActivity<GetActionDetailsInput, Action>
    {
        private readonly IRecordActionsPlugin _recordActionsPlugin;

        public GetActionDetailsActivity(IRecordActionsPlugin recordActionsPlugin)
        {
            _recordActionsPlugin = recordActionsPlugin ?? throw new ArgumentNullException(nameof(recordActionsPlugin));
        }

        public override async Task<Action> RunAsync(TaskActivityContext context, GetActionDetailsInput input)
        {
            // Get the action details using the plugin
            var action = await _recordActionsPlugin.GetAction(
                input.ThreadId,
                input.ActionId);

            return action;
        }
    }
}

