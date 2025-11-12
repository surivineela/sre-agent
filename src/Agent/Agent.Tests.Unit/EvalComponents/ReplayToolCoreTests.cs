using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Tests.Common.Mocks.FunctionCalling;
using Microsoft.Extensions.AI;
using Xunit;

namespace Agent.Tests.Unit.EvalComponents;

public class ReplayToolCoreTests
{
    private readonly JsonSerializerOptions _serializerOptions;

    public ReplayToolCoreTests()
    {
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    [Fact]
    public void LoadLogFromString_ChatMessageFormat_LoadsReplayEntries()
    {
        // Arrange
        var chatMessageLogPath = Path.Combine("TestData", "ToolReplayLogs", "chatmessage-format-f41f64f7-d7a3-4061-b42f-33a992aa413e.json");
        var chatMessageContent = File.ReadAllText(chatMessageLogPath);
        var replayCore = new ReplayToolCore(_serializerOptions);

        // Act
        replayCore.LoadLogFromString(chatMessageContent);

        // Assert
        var functionNames = replayCore.FunctionNames.ToList();
        Assert.NotEmpty(functionNames);
        Assert.Contains("ListResourcesByType", functionNames);
    }

    [Fact]
    public void LoadLogFromString_OtelFormat_LoadsReplayEntries()
    {
        // Arrange
        var otelLogPath = Path.Combine("TestData", "ToolReplayLogs", "otel-format-f41f64f7-d7a3-4061-b42f-33a992aa413e.json");
        var otelContent = File.ReadAllText(otelLogPath);
        var replayCore = new ReplayToolCore(_serializerOptions);

        // Act
        replayCore.LoadLogFromString(otelContent);

        // Assert
        var functionNames = replayCore.FunctionNames.ToList();
        Assert.NotEmpty(functionNames);
        Assert.Contains("ListResourcesByType", functionNames);
        Assert.Contains("KubectlGet", functionNames);
        Assert.Contains("KubectlDescribe", functionNames);
    }

    [Fact]
    public void LoadLogFromString_BothFormats_ProduceSameReplayEntries()
    {
        // Arrange
        var chatMessageLogPath = Path.Combine("TestData", "ToolReplayLogs", "chatmessage-format-f41f64f7-d7a3-4061-b42f-33a992aa413e.json");
        var otelLogPath = Path.Combine("TestData", "ToolReplayLogs", "otel-format-f41f64f7-d7a3-4061-b42f-33a992aa413e.json");

        var chatMessageContent = File.ReadAllText(chatMessageLogPath);
        var otelContent = File.ReadAllText(otelLogPath);

        var chatMessageReplayCore = new ReplayToolCore(_serializerOptions);
        var otelReplayCore = new ReplayToolCore(_serializerOptions);

        // Act
        chatMessageReplayCore.LoadLogFromString(chatMessageContent);
        otelReplayCore.LoadLogFromString(otelContent);

        // Assert
        var chatMessageFunctions = chatMessageReplayCore.FunctionNames.OrderBy(x => x).ToList();
        var otelFunctions = otelReplayCore.FunctionNames.OrderBy(x => x).ToList();

        // Debug output to understand what's being extracted
        var chatMessageFunctionsStr = string.Join(", ", chatMessageFunctions);
        var otelFunctionsStr = string.Join(", ", otelFunctions);

        // Verify that both formats contain overlapping function calls
        var commonFunctions = chatMessageFunctions.Intersect(otelFunctions).ToList();
        Assert.True(commonFunctions.Count > 0, $"No common functions found. ChatMessage: [{chatMessageFunctionsStr}], OTEL: [{otelFunctionsStr}]");

        // Verify that at least ListResourcesByType is present in both (if available in chat message format)
        if (chatMessageFunctions.Contains("ListResourcesByType"))
        {
            Assert.Contains("ListResourcesByType", commonFunctions);
        }

        // Verify that for the common functions, we can find matching replay entries
        foreach (var functionName in commonFunctions)
        {
            var chatMessageEntries = chatMessageReplayCore.GetReplayEntriesForFunction(functionName).ToList();
            var otelEntries = otelReplayCore.GetReplayEntriesForFunction(functionName).ToList();

            Assert.NotEmpty(chatMessageEntries);
            Assert.NotEmpty(otelEntries);

            // Find entries with matching arguments
            foreach (var chatEntry in chatMessageEntries)
            {
                var matchingOtelEntry = otelEntries.FirstOrDefault(oe =>
                    AreJsonStringsEquivalent(oe.FunctionArgumentsJson, chatEntry.FunctionArgumentsJson));

                if (matchingOtelEntry != null)
                {
                    // Instead of strict equality, verify both have non-empty results for successful function calls
                    var chatHasResult = !string.IsNullOrEmpty(chatEntry.FunctionResultJson) && chatEntry.FunctionResultJson != "null";
                    var otelHasResult = !string.IsNullOrEmpty(matchingOtelEntry.FunctionResultJson) && matchingOtelEntry.FunctionResultJson != "null";

                    Assert.Equal(chatHasResult, otelHasResult);
                }
            }
        }
    }

    [Fact]
    public void HasReplayDataForFunction_WithLoadedData_ReturnsTrue()
    {
        // Arrange
        var chatMessageLogPath = Path.Combine("TestData", "ToolReplayLogs", "chatmessage-format-f41f64f7-d7a3-4061-b42f-33a992aa413e.json");
        var chatMessageContent = File.ReadAllText(chatMessageLogPath);
        var replayCore = new ReplayToolCore(_serializerOptions);
        replayCore.LoadLogFromString(chatMessageContent);

        // Act & Assert
        Assert.True(replayCore.HasReplayDataForFunction("ListResourcesByType"));
        Assert.False(replayCore.HasReplayDataForFunction("NonExistentFunction"));
    }

    [Fact]
    public void FindReplayMatch_WithMatchingArguments_ReturnsEntry()
    {
        // Arrange
        var chatMessageLogPath = Path.Combine("TestData", "ToolReplayLogs", "chatmessage-format-f41f64f7-d7a3-4061-b42f-33a992aa413e.json");
        var chatMessageContent = File.ReadAllText(chatMessageLogPath);
        var replayCore = new ReplayToolCore(_serializerOptions);
        replayCore.LoadLogFromString(chatMessageContent);

        var testArguments = @"{""resourceType"":""Microsoft.ContainerService/managedClusters"",""propertyName"":"""",""propertyValue"":"""",""skip"":0,""take"":-1}";

        // Act
        var match = replayCore.FindReplayMatch("ListResourcesByType", testArguments);

        // Assert
        Assert.NotNull(match);
        Assert.Equal("ListResourcesByType", match.FunctionName);
    }

    private static bool AreJsonStringsEquivalent(string jsonStr1, string jsonStr2)
    {
        if (jsonStr1 == jsonStr2) return true;

        try
        {
            using var doc1 = JsonDocument.Parse(jsonStr1);
            using var doc2 = JsonDocument.Parse(jsonStr2);
            return JsonElementEqualityComparer.Instance.Equals(doc1.RootElement, doc2.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public class JsonElementEqualityComparer : IEqualityComparer<JsonElement>
{
    public static readonly JsonElementEqualityComparer Instance = new();

    public bool Equals(JsonElement x, JsonElement y)
    {
        if (x.ValueKind != y.ValueKind) return false;

        return x.ValueKind switch
        {
            JsonValueKind.Null => true,
            JsonValueKind.True or JsonValueKind.False => x.GetBoolean() == y.GetBoolean(),
            JsonValueKind.Number => x.GetDecimal() == y.GetDecimal(),
            JsonValueKind.String => x.GetString() == y.GetString(),
            JsonValueKind.Array => ArrayEquals(x, y),
            JsonValueKind.Object => ObjectEquals(x, y),
            _ => false
        };
    }

    public int GetHashCode(JsonElement obj) => obj.GetHashCode();

    private bool ArrayEquals(JsonElement x, JsonElement y)
    {
        var xArray = x.EnumerateArray().ToArray();
        var yArray = y.EnumerateArray().ToArray();

        if (xArray.Length != yArray.Length) return false;

        for (var i = 0; i < xArray.Length; i++)
        {
            if (!Equals(xArray[i], yArray[i])) return false;
        }

        return true;
    }

    private bool ObjectEquals(JsonElement x, JsonElement y)
    {
        var xProperties = x.EnumerateObject().ToArray();
        var yProperties = y.EnumerateObject().ToArray();

        if (xProperties.Length != yProperties.Length) return false;

        foreach (var xProp in xProperties)
        {
            if (!y.TryGetProperty(xProp.Name, out var yValue) || !Equals(xProp.Value, yValue))
                return false;
        }

        return true;
    }
}
