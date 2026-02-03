using System.Diagnostics;
using Adc.RemoteWorkspace.Protocol;
using Grpc.Core;

namespace Agent.Adc.RemoteWorkspace.Services;


public class ExecutionService : Execution.ExecutionBase
{
    private readonly ILogger<ExecutionService> _logger;
    private readonly ShellManager _shellManager;

    public ExecutionService(ILogger<ExecutionService> logger, ShellManager shellManager)
    {
        _logger = logger;
        _shellManager = shellManager;
    }

    public override async Task<ExecuteBashResponse> ExecuteBash(ExecuteBashRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received ExecuteBash request. Command: {Command}, Background: {Background}",
            request.Command, request.RunInBackground);

        try
        {
            if (request.RunInBackground)
            {
                var sessionId = await _shellManager.ExecuteBackgroundAsync(request.Command);
                var job = _shellManager.GetJob(sessionId);

                var msg = $"Background process started (PID: {job?.Pid}). Output file: {job?.LogPath}";
                _logger.LogInformation(msg);

                return new ExecuteBashResponse
                {
                    ExitCode = 0,
                    Stdout = msg,
                    SessionId = sessionId,
                };
            }
            else
            {
                var timeout = request.Timeout > 0 ? TimeSpan.FromMilliseconds(request.Timeout) : TimeSpan.FromMinutes(2);
                try
                {
                    var (exitCode, output) = await _shellManager.ExecuteForegroundAsync(request.Command, timeout);
                    return new ExecuteBashResponse
                    {
                        ExitCode = exitCode,
                        Stdout = output,
                        Stderr = "",
                        SessionId = "" // Foreground cmd doesn't really have a persisted session ID in this model
                    };
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Execution request timed out");
                    return new ExecuteBashResponse
                    {
                        TimedOut = true,
                        Error = "Command timed out"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteBash failed");
            return new ExecuteBashResponse { Error = ex.Message, ExitCode = -1 };
        }
    }

    public override async Task<GetBashOutputResponse> GetBashOutput(GetBashOutputRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received GetBashOutput request. TaskId: {TaskId}, Block: {Block}", request.TaskId, request.Block);

        try
        {
            var job = _shellManager.GetJob(request.TaskId);

            if (job == null)
            {
                _logger.LogWarning("Job not found for TaskId: {TaskId}", request.TaskId);
                return new GetBashOutputResponse
                {
                    RetrievalStatus = "not_ready",
                    TaskId = request.TaskId,
                    Status = "unknown",
                    Error = "Job not found"
                };
            }

            var logPath = job.LogPath;
            if (!File.Exists(logPath))
            {
                _logger.LogInformation("Log file pending for TaskId: {TaskId}", request.TaskId);
                return new GetBashOutputResponse
                {
                    RetrievalStatus = "not_ready",
                    TaskId = request.TaskId,
                    Status = "pending",
                    Output = ""
                };
            }

            bool isRunning = false;
            try
            {
                Process.GetProcessById(job.Pid);
                isRunning = true;
            }
            catch { isRunning = false; }

            if (request.Block && isRunning)
            {
                _logger.LogInformation("Blocking wait for TaskId: {TaskId}", request.TaskId);
                // Wait loop
                var timeout = request.Timeout > 0 ? request.Timeout : 30000;
                var start = DateTime.UtcNow;

                while (DateTime.UtcNow - start < TimeSpan.FromMilliseconds(timeout))
                {
                    try { Process.GetProcessById(job.Pid); } catch { isRunning = false; break; }
                    await Task.Delay(500);
                }

                if (isRunning)
                {
                    _logger.LogInformation("Blocking wait timed out for TaskId: {TaskId}", request.TaskId);
                    return new GetBashOutputResponse
                    {
                        RetrievalStatus = "timeout",
                        TaskId = request.TaskId,
                        Status = "running"
                    };
                }
            }

            var content = await File.ReadAllTextAsync(logPath);
            _logger.LogInformation("Retrieved output for TaskId: {TaskId}. Status: {Status}, Length: {Length}",
                request.TaskId, isRunning ? "running" : "completed", content.Length);

            return new GetBashOutputResponse
            {
                RetrievalStatus = "success",
                TaskId = request.TaskId,
                Status = isRunning ? "running" : "completed",
                Output = content,
                ExitCode = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetBashOutput failed");
            return new GetBashOutputResponse { Error = ex.Message, RetrievalStatus = "failed" };
        }
    }

    public override Task<KillTaskResponse> KillTask(KillTaskRequest request, ServerCallContext context)
    {
        try
        {
            var jobId = request.TaskId;

            _logger.LogInformation("Received KillTask request for JobId: {JobId}", jobId);

            _shellManager.KillJob(jobId);
            return Task.FromResult(new KillTaskResponse { Success = true, Message = $"Killed job {jobId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KillTask failed");
            return Task.FromResult(new KillTaskResponse { Success = false, Error = ex.Message });
        }
    }
}
