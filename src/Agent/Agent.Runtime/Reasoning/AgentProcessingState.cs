namespace Agent.Runtime.Reasoning;

public enum AgentProcessingState
{
    Unknown,
    Processing,
    UserInputRequired,
    HandOff_Continue,
    HandOff_OutOfScope,
    CompletedSuccessfully,
    RequestFailed,
}
