using System.Text.Json;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace Agent.Logging;

public static class LogHelper
{
    public static string GetSerializedArguments(this FunctionCallContent callContent)
    {
        if (callContent.RawRepresentation is not null
            && callContent.RawRepresentation is ChatToolCall toolCall
            && toolCall is not null)
        {
            return toolCall.FunctionArguments.ToString();
        }
        else
        {
            return JsonSerializer.Serialize(callContent.Arguments, AIJsonUtilities.DefaultOptions);
        }
    }
}
