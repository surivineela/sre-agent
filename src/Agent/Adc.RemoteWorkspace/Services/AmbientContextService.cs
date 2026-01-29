// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Adc.RemoteWorkspace.Protocol;
using Agent.Common.Services;
using Agent.Plugins.Services;
using Grpc.Core;

namespace Agent.Adc.RemoteWorkspace.Services;

/// <summary>
/// gRPC service implementation for ambient context operations.
/// Delegates to LocalWorkspaceContext for actual file system operations.
/// </summary>
public class AmbientContextService : AmbientContext.AmbientContextBase
{
    private readonly ILogger<AmbientContextService> _logger;
    private readonly IWorkspaceContext _workspaceContext;

    public AmbientContextService(ILogger<AmbientContextService> logger, ISandboxPaths sandboxPaths)
    {
        _logger = logger;
        _workspaceContext = new LocalWorkspaceContext(logger, sandboxPaths);
    }

    public override async Task<GetInstructionsContextResponse> GetInstructionsContext(
        GetInstructionsContextRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("GetInstructionsContext called");

        try
        {
            var result = await _workspaceContext.GetInstructionsContextAsync(context.CancellationToken);
            return new GetInstructionsContextResponse { Context = result };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetInstructionsContext failed");
            return new GetInstructionsContextResponse
            {
                Error = $"Failed to get instructions context: {ex.Message}"
            };
        }
    }

    public override async Task<GetEnvironmentContextResponse> GetEnvironmentContext(
        GetEnvironmentContextRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("GetEnvironmentContext called");

        try
        {
            var result = await _workspaceContext.GetEnvironmentContextAsync(context.CancellationToken);
            return new GetEnvironmentContextResponse { Context = result };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEnvironmentContext failed");
            return new GetEnvironmentContextResponse
            {
                Error = $"Failed to get environment context: {ex.Message}"
            };
        }
    }

    public override async Task<GetPreQueryContextResponse> GetPreQueryContext(
        GetPreQueryContextRequest request,
        ServerCallContext context)
    {
        _logger.LogInformation("GetPreQueryContext called, terminal_state length: {Length}, todo_list count: {Count}",
            request.TerminalState?.Length ?? 0,
            request.TodoList?.Count ?? 0);

        try
        {
            // Convert proto TodoItem to WorkspaceTodoItemDto
            IReadOnlyList<WorkspaceTodoItemDto>? todoList = null;
            if (request.TodoList != null && request.TodoList.Count > 0)
            {
                todoList = request.TodoList
                    .Select(t => new WorkspaceTodoItemDto(t.Id, t.Title, t.Description, t.Status))
                    .ToList();
            }

            var terminalState = string.IsNullOrEmpty(request.TerminalState) ? null : request.TerminalState;

            var result = await _workspaceContext.GetPreUserQueryContextAsync(
                terminalState,
                todoList,
                context.CancellationToken);

            return new GetPreQueryContextResponse { Context = result };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPreQueryContext failed");
            return new GetPreQueryContextResponse
            {
                Error = $"Failed to get pre-query context: {ex.Message}"
            };
        }
    }
}
