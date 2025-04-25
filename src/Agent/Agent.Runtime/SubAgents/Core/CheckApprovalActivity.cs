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
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.Core;

[DurableTask]
public class CheckApprovalActivity : TaskActivity<CheckApprovalActivityInput, CheckApprovalActivityOutput>
{
    private readonly ILogger<CheckApprovalActivity> _logger;
    private readonly IToolsRepository _toolsRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;

    public CheckApprovalActivity(ILogger<CheckApprovalActivity> logger, IToolsRepository toolsRepository, IThreadRepository threadRepository, IAgentOutboundCommunicationService outboundCommunicationService)
    {
        _logger = logger;
        _toolsRepository = toolsRepository;
        _threadRepository = threadRepository;
        _outboundCommunicationService = outboundCommunicationService;
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

            var approvalTitle = GenerateUniqueApprovalTitle(
                input.ThreadId,
                context.InstanceId,
                targetFunction,
                input.FunctionCall.Arguments ?? new Dictionary<string, object?>());

            var approval = await _threadRepository.GetApprovalAsync(Guid.Parse(input.ThreadId), approvalTitle);

            if (approval == null)
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
                    OrchestrationId: input.OrchestrationId,
                    Title: approvalTitle,
                    Description: description,
                    Status: ApprovalDecision.Pending,
                    CreatedTimestamp: DateTime.UtcNow,
                    DecisionTimestamp: null,
                    DecisionUser: null,
                    OboToken: null);

                await _threadRepository.CreateApprovalAsync(newApproval);
                await _outboundCommunicationService.AppendAgentApprovalMessage(
                    Guid.Parse(input.ThreadId),
                    newApproval);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = newApproval.Id.ToString(),
                    ApprovalStatus = ToolApprovalStatus.Pending,
                };
            }
            else
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = approval.Id.ToString(),
                    ApprovalStatus = ApprovalDocument.ToToolApprovalStatus(approval.Status),
                    OboToken = approval.OboToken,
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
        // calculate SHA256 hash of the arguments
        var orderedArgs = new OrderedDictionary<string, object?>(arguments);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(orderedArgs)));
        var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        var truncatedHash = hashString.Substring(0, Math.Min(16, hashString.Length));

        return $"{threadId}-{orchstrationId}-{operationName}-{truncatedHash}";
    }
}
