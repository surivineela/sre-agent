using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Agent.Core.Models.Api.v1;

public enum ReasoningMessageRoleEnum
{
    User = 0,
    Assistant = 1,
    System = 2,
    Tool = 3
}
