using System.ComponentModel;

namespace OperationalAgentCore;

public sealed record AsyncOperationProgress<TProgressDetail>(
    [Description("The time when this progress update is posted.")]
    DateTime Time,
    [Description("The detail information about the progress.")]
    TProgressDetail Detail);

public sealed record AsyncOperationStatusSummary<TDescriptor, TProgressDetail>(
    [Description("The contextual message, describing the async operation")]
    string ContextMessage,
    [Description("The start time of this background operation")]
    DateTime StartTime,
    [Description("The descriptor of this background operation, it contains all the input to the operation")]
    TDescriptor Descriptor,
    [Description("The list of progress events of this background operation")]
    IReadOnlyList<AsyncOperationProgress<TProgressDetail>> Progress,
    [Description("The overall status of this operation, it indicates if the operation is finished or still in progress")]
    string OverallStatus,
    [Description("Extra detail of overall status")]
    string Details);

public sealed record AsyncOperationStartResult<TDescriptor, TProgressDetail>(
    [Description("If this operation was started successfully. Value is false if the operation was already started.")]
    bool Created,
    [Description("The summary of the operation. If the operation was already started, this is the summary of previously started operation.")]
    AsyncOperationStatusSummary<TDescriptor, TProgressDetail> Summary);