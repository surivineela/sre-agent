namespace Agent.Web.Models.v1;

public enum ActionStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public record Action(
    Guid Id,
    string Title,
    DateTime TimeStamp,
    ActionStatus Status
);
