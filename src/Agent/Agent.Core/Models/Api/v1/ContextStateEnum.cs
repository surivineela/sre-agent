namespace Agent.Core.Models.Api.v1;

public enum ContextStateEnum
{
    Processing = 0,
    Waiting = 1,
    PendingApproval = 2,
    Idle = 3,
    Completed = 4,
}
