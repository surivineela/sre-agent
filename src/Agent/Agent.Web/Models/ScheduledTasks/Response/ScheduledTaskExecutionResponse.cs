// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ScheduledTasks.Response;

public class ScheduledTaskExecutionResponse
{
    public DateTime ExecutionTime { get; set; }
    public string? ThreadId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? ExecutionMetadata { get; set; }
}

public class ScheduledTaskExecutionWithTaskInfoResponse
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public DateTime ExecutionTime { get; set; }
    public string? ThreadId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? ExecutionMetadata { get; set; }
}

public class ExecuteTaskNowResponse
{
    public string Message { get; set; } = string.Empty;
    public ScheduledTaskExecutionResponse Execution { get; set; } = new();
}
