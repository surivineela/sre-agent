// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public sealed class Trajectory
{
    public int CriticCount { get; private set; } = 0;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private const int MaxResultWords = 200;

    private StringBuilder TrajectoryBuilder { get; } = new();

    public void Append(ChatResponse modelResponse)
    {
        foreach (var msg in modelResponse.Messages)
        {
            TrajectoryBuilder.AppendLine($"Role: {msg.Role}");
            TrajectoryBuilder.AppendLine();

            foreach (var content in msg.Contents)
            {
                if (content is TextContent textContent)
                {
                    TrajectoryBuilder.AppendLine(textContent.Text);
                    TrajectoryBuilder.AppendLine();
                }
                else if (content is FunctionCallContent functionCallContent)
                {
                    TrajectoryBuilder.AppendLine($"Function Call: {functionCallContent.Name}");
                    TrajectoryBuilder.AppendLine($"Parameters: {(functionCallContent.RawRepresentation as OpenAI.Chat.ChatToolCall)!.FunctionArguments.ToString()}");
                    TrajectoryBuilder.AppendLine();
                }
                // don't expect this in general as tool calls are handled manually
                // however for parallel tool call we use functionInvokingChatClient, which will inline the results
                else if (content is FunctionResultContent functionResultContent)
                {
                    Append(functionResultContent);
                }
                else
                {
                    throw new Exception($"Unknown content type: {content.GetType()}");
                }
            }
        }

        TrajectoryBuilder.AppendLine();
    }

    public void Append(FunctionResultContent functionResult)
    {
        var resultString = ResultToString(functionResult);
        TrajectoryBuilder.AppendLine($"Function Call Result:\n{resultString}");
        TrajectoryBuilder.AppendLine();
    }

    public static string ResultToString(FunctionResultContent functionResult)
    {
        if (functionResult.Result is null)
        {
            return "null";
        }
        else
        {
            var resultObj = functionResult.Result;

            var resultString = (resultObj is string str) ? str : JsonSerializer.Serialize(resultObj, _jsonOptions);

            return TextVolumeHelpers.ApplyWordTruncation(
                input: resultString,
                maxWords: MaxResultWords,
                addTruncationMessage: false);
        }
    }

    public void AppendCriticFeedback(string feedback)
    {
        TrajectoryBuilder.AppendLine($"This is the critic feedback received by the agent in the past run. It contains a verified summary of the past actions taken by the agent.\n\nRole: Critic.");
        TrajectoryBuilder.AppendLine();
        TrajectoryBuilder.AppendLine(feedback);
        TrajectoryBuilder.AppendLine($"Now the agent has taken the following steps since then to address the feedback.\n\n");
    }

    public string Close()
    {
        var trajectory = TrajectoryBuilder.ToString();
        TrajectoryBuilder.Clear();
        CriticCount++;
        return trajectory;
    }
}
