// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Runtime.Helpers;
using Microsoft.DurableTask;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class CheckApprovalActivity : TaskActivity<CheckApprovalActivityInput, CheckApprovalActivityOutput>
{
    private readonly ILogger<CheckApprovalActivity> _logger;
    private readonly IToolsRepository _toolsRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ActionSettings _actionSettings;

    public CheckApprovalActivity(ILogger<CheckApprovalActivity> logger,
        IToolsRepository toolsRepository,
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IHostEnvironment hostEnvironment,
        ActionSettings actionSettings)
    {
        _logger = logger;
        _toolsRepository = toolsRepository;
        _threadRepository = threadRepository;
        _outboundCommunicationService = outboundCommunicationService;
        _hostEnvironment = hostEnvironment;
        _actionSettings = actionSettings;
    }

    public override async Task<CheckApprovalActivityOutput> RunAsync(TaskActivityContext context, CheckApprovalActivityInput input)
    {
        try
        {
            if (input.FunctionCall == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            var toolSignatures = input.ToolSignatures;
            var targetFunction = input.FunctionCall!.Name;
            // Get all tools and find matching tool
            var aiFunctions = _toolsRepository.GetAllTools(toolSignatures).Select(_toolsRepository.FindAiFunction);
            var matchingTool = aiFunctions.Single(x => x.ToolFunction.Name == targetFunction);

            // Check if requiers approval
            if (!ApprovalHelper.ToolRequiresApproval(matchingTool))
            {
                _logger.LogInternalInformation("[{ThreadId}] approval is not required for tool {FunctionName}", input.ThreadId, targetFunction);
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            if (_actionSettings.Mode == ActionMode.ReadOnly)
            {
                _logger.LogInternalInformation("[{ThreadId}] approval is not required for tool {FunctionName} in read-only mode", input.ThreadId, targetFunction);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            if (_actionSettings.Mode == ActionMode.Autonomous)
            {
                _logger.LogInternalInformation("[{ThreadId}] approval is auto approved for tool {FunctionName}", input.ThreadId, targetFunction);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.AutoApproved,
                };
            }

            // TODO: should the approval be unique to the specific function call (not just the args being passed)?
            // we can use input.FunctionCall.CallId to ensure it's scoped only to this particular call
            var approvalTitle = ApprovalHelper.GenerateUniqueApprovalTitle(
                input.ThreadId,
                context.InstanceId,
                targetFunction,
                input.FunctionCall.Arguments ?? new Dictionary<string, object?>());

            _logger.LogInternalInformation("Checking approval for threadId: {ThreadId}, function: {FunctionName}, title: {Title}, instanceId {instanceId}, arguments {arguments}", input.ThreadId, targetFunction, approvalTitle, context.InstanceId, input.FunctionCall.Arguments);

            var approval = await _threadRepository.GetApprovalAsync(Guid.Parse(input.ThreadId), approvalTitle);

            if (approval == null ||
                // oboToken expires
                (!_hostEnvironment.IsDevelopment() && ApprovalHelper.ApprovalExpired(approval, matchingTool)))
            {
                var description = ApprovalHelper.GetToolDefaultApprovalMessage(matchingTool);
                // Try get latest action with the function call name
                if (input.ActionId != Guid.Empty)
                {
                    var action = await _threadRepository.GetActionAsync(Guid.Parse(input.ThreadId), input.ActionId);
                    if (action != null)
                    {
                        description = action.Title;
                    }
                }

                // Create a new approval document
                var newApproval = new Approval(
                    Id: Guid.NewGuid(),
                    ThreadId: input.ThreadId,
                    Title: approvalTitle,
                    Description: description,
                    Status: ApprovalDecision.Pending,
                    CreatedTimestamp: DateTime.UtcNow,
                    DecisionTimestamp: null,
                    OrchestrationId: input.OrchestrationId,
                    AgentContextId: null,
                    DecisionUser: null,
                    OboToken: null);

                await _threadRepository.CreateApprovalAsync(newApproval);
                await _outboundCommunicationService.AppendAgentApprovalMessage(
                    Guid.Parse(input.ThreadId),
                    newApproval);

                _logger.LogInternalInformation("Created new approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status ToolApprovalStatus.Pending", newApproval.Id, input.ThreadId, newApproval.Title);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = newApproval.Id,
                    ApprovalStatus = ToolApprovalStatus.Pending,
                };
            }
            else
            {
                _logger.LogInternalInformation("Found existing approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status {Status}", approval.Id, input.ThreadId, approval.Title, approval.Status);
                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = approval.Id,
                    ApprovalStatus = ApprovalDocument.ToToolApprovalStatus(approval.Status),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("Error while checking approval: {Message}", ex.Message);
            return new CheckApprovalActivityOutput()
            {
                ApprovalStatus = ToolApprovalStatus.Pending,
            };
        }
    }
}
