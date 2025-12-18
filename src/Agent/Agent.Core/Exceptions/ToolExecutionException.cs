using System;

namespace Agent.Core.Exceptions;

public class ToolExecutionException : Exception
{
    public ToolExecutionException(string message) : base(message)
    {
    }

    public ToolExecutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ToolExecutionUnauthorizedException : ToolExecutionException
{
    public ToolExecutionUnauthorizedException(string message) : base(message)
    {
    }

    public ToolExecutionUnauthorizedException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public string? CustomDescription { get; set; }

}
