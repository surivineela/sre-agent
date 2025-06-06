namespace Agent.Tests.Common.Mocks.FunctionCalling;

public class ReplayFailureException : Exception
{
    public ReplayFailureException(string message) : base(message)
    {
    }
    public ReplayFailureException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
