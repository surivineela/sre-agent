namespace Agent.Core.Models.Api.v1;

public record FunctionInvocation(
    string FunctionName,
    Dictionary<string, string> Arguments);
