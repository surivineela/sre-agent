// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.V2;

/// <summary>
/// This class wraps another AIFunction instance that requires user approval
/// </summary>
public class ApprovalInterceptor : AIFunction
{
    private readonly AIFunction _innerFunction;
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly AgentContext _context;
    private readonly ILogger<ApprovalInterceptor> _logger;

    #region AITool properties
    public override string Name => _innerFunction.Name;
    public override string Description => _innerFunction.Description;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _innerFunction.AdditionalProperties;
    #endregion

    public ApprovalInterceptor(
        AgentContext context,
        AIFunction function,
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService outboundCommunicationService,
        ILogger<ApprovalInterceptor> logger)
    {
        _context = context;
        _innerFunction = function;
        _threadRepository = threadRepository;
        _outboundCommunicationService = outboundCommunicationService;
        _logger = logger;
    }

    #region AIFunction override
    public override JsonElement JsonSchema => _innerFunction.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => _innerFunction.JsonSerializerOptions;
    public override MethodInfo? UnderlyingMethod => _innerFunction.UnderlyingMethod;

    protected override Task<object?> InvokeCoreAsync(
        IEnumerable<KeyValuePair<string, object?>> arguments,
        CancellationToken cancellationToken)
    {
        return HandleApprovalAsync(arguments.ToDictionary(), cancellationToken);
    }
    #endregion

    private async Task<object?> HandleApprovalAsync(IDictionary<string, object?> argsDict, CancellationToken cancellationToken)
    {
        string operationName = _innerFunction.Name; // TODO: get better name for this

        _logger.LogInformation("[{ThreadId}] Checking approval for operation: {OperationName}",
            _context.ThreadId, operationName);

        var approvalTitle = ApprovalTitleHelper.GenerateUniqueApprovalTitle(
            _context.ThreadId.ToString(),
            _context.Id.ToString(),
            operationName,
            argsDict);

        var approval = await _threadRepository.GetApprovalAsync(_context.ThreadId, approvalTitle);

        if (approval == null)
        {
            // Create a new approval document
            var newApproval = new Approval(
                Id: Guid.NewGuid(),
                ThreadId: _context.ThreadId.ToString(),
                Title: approvalTitle,
                Description: operationName,
                Status: ApprovalDecision.Pending,
                CreatedTimestamp: DateTime.UtcNow,
                DecisionTimestamp: null,
                OrchestrationId: null,
                AgentContextId: _context.Id,
                DecisionUser: null,
                OboToken: null);

            await _threadRepository.CreateApprovalAsync(newApproval);

            var refreshedContext = await _threadRepository.GetAgentContextAsync(_context.Id, _context.ThreadId);

            if (refreshedContext != null)
            {
                var existingApprovalInfo = refreshedContext.ApprovalInformation?.PendingApprovals ?? [];
                existingApprovalInfo.Add(newApproval.Id);

                refreshedContext = refreshedContext with
                {
                    ApprovalInformation = new ApprovalInformation(
                        PendingApprovals: existingApprovalInfo)
                };

                await _threadRepository.UpdateAgentContextAsync(refreshedContext);
            }

            await _outboundCommunicationService.AppendAgentApprovalMessage(
                _context.ThreadId,
                newApproval);

            _logger.LogInformation("[{ThreadId}] Approval required for operation: {OperationName}",
                _context.ThreadId, operationName);

            throw new ApprovalRequiredException($"Approval is required for this action {operationName}");
        }
        else if (approval.Status == ApprovalDecision.Approved)
        {
            // approval received, invoke the inner function
            _logger.LogInformation("[{ThreadId}] Approval received for operation: {OperationName}",
                _context.ThreadId, operationName);

            return await _innerFunction.InvokeAsync(argsDict, cancellationToken);
        }
        else if (approval.Status == ApprovalDecision.Rejected)
        {
            _logger.LogInformation("[{ThreadId}] Approval rejected for operation: {OperationName}",
                _context.ThreadId, operationName);

            throw new ApprovalRejectedException($"User rejected the action {operationName}");
        }
        else
        {
            _logger.LogInformation("[{ThreadId}] Approval required for operation: {OperationName}",
                _context.ThreadId, operationName);

            throw new ApprovalRequiredException($"Approval is required for this action {operationName}");
        }
    }
}
