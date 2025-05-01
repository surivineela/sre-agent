// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
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

    public CheckApprovalActivity(ILogger<CheckApprovalActivity> logger,
        IToolsRepository toolsRepository,
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _toolsRepository = toolsRepository;
        _threadRepository = threadRepository;
        _outboundCommunicationService = outboundCommunicationService;
        _hostEnvironment = hostEnvironment;
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
            var attribute = matchingTool.ToolFunction.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();
            if (attribute == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            var approvalTitle = ApprovalTitleHelper.GenerateUniqueApprovalTitle(
                input.ThreadId,
                context.InstanceId,
                targetFunction,
                input.FunctionCall.Arguments ?? new Dictionary<string, object?>());

            var approval = await _threadRepository.GetApprovalAsync(Guid.Parse(input.ThreadId), approvalTitle);

            if (approval == null ||
                // oboToken expires
                // TODO: get rid of hostEnvironment check. Make it something like actionMode: OBO/Agent check
                (!_hostEnvironment.IsDevelopment() && approval.Status == ApprovalDecision.Approved && string.IsNullOrEmpty(approval.OboToken)))
            {
                var description = attribute.DisplayMessage ?? string.Empty;
                // Try get latest action with the function call name
                var action = await _threadRepository.GetLatestToolCallAction(Guid.Parse(input.ThreadId), targetFunction);
                if (action != null)
                {
                    description = action.Title;
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

                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = newApproval.Id,
                    ApprovalStatus = ToolApprovalStatus.Pending,
                };
            }
            else
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = approval.Id,
                    ApprovalStatus = ApprovalDocument.ToToolApprovalStatus(approval.Status),
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error while checking approval: {Message}", ex.Message);
            return new CheckApprovalActivityOutput()
            {
                ApprovalStatus = ToolApprovalStatus.Pending,
            };
        }
    }

    private string GenerateUniqueApprovalTitle(string threadId, string orchstrationId, string operationName, IDictionary<string, object?> arguments)
    {
        // model may hallucinate these IDs causing unstable hash for same action
        if (arguments.ContainsKey("threadId"))
        {
            arguments.Remove("threadId");
        }

        if (arguments.ContainsKey("approvalId"))
        {
            arguments.Remove("approvalId");
        }

        // calculate SHA256 hash of the arguments
        var orderedArgs = new OrderedDictionary<string, object?>(arguments);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderedArgs)));
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        var truncatedHash = hashString.Substring(0, Math.Min(16, hashString.Length));

        return $"{threadId}-{orchstrationId}-{operationName}-{truncatedHash}";
    }
}
