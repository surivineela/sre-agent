// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Helpers.JmesPath;

namespace Agent.Tests.Unit.Core.Services;

public class ComplianceTestCase
{
    [JsonPropertyName("expression")]
    public string Expression { get; set; } = "";

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("bench")]
    public string? Bench { get; set; }
}

public class ComplianceTestGroup
{
    [JsonPropertyName("given")]
    public JsonElement Given { get; set; }

    [JsonPropertyName("cases")]
    public List<ComplianceTestCase> Cases { get; set; } = new();

    [JsonPropertyName("comment")]
    public string? Comment { get; set; }
}

public class ComplianceTests
{
    private const string ComplianceDir = "./Core/JmesPath/Compliance";
    private const string LegacyDir = "./Core/JmesPath/Legacy";

    public static IEnumerable<object[]> GetResultTestCases()
    {
        var allFiles = Directory.GetFiles(ComplianceDir, "*.json")
            .Concat(Directory.GetFiles(LegacyDir, "*.json"));

        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);
            var testGroups = JsonSerializer.Deserialize<List<ComplianceTestGroup>>(content);

            if (testGroups == null) continue;

            foreach (var group in testGroups)
            {
                foreach (var testCase in group.Cases)
                {
                    if (testCase.Result != null)
                    {
                        // Return: given, expression, expected result, filename
                        yield return new object[]
                        {
                            group.Given,
                            testCase.Expression,
                            testCase.Result.Value
                        };
                    }
                }
            }
        }
    }

    public static IEnumerable<object[]> GetErrorTestCases()
    {
        var allFiles = Directory.GetFiles(ComplianceDir, "*.json")
            .Concat(Directory.GetFiles(LegacyDir, "*.json"));

        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);
            var testGroups = JsonSerializer.Deserialize<List<ComplianceTestGroup>>(content);

            if (testGroups == null) continue;

            foreach (var group in testGroups)
            {
                foreach (var testCase in group.Cases)
                {
                    if (testCase.Error != null)
                    {
                        // Return: given, expression, error type, filename
                        yield return new object[]
                        {
                            group.Given,
                            testCase.Expression,
                            testCase.Error
                        };
                    }
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetResultTestCases))]
    public void TestComplianceResult(JsonElement given, string expression, JsonElement expected)
    {
        // Parse and execute the expression
        var actual = JmesPath.Query(expression, given);

        // Compare results using JSON serialization for deep comparison
        var expectedJson = JsonSerializer.Serialize(expected);
        var actualJson = JsonSerializer.Serialize(actual);

        Assert.Equal(expectedJson, actualJson);
    }

    [Theory]
    [MemberData(nameof(GetErrorTestCases))]
    public void TestComplianceError(JsonElement given, string expression, string errorType)
    {
        // Map error types to exception types
        Exception? exception = null;
        try
        {
            JmesPath.Query(expression, given);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.NotNull(exception);

        // Validate the error type matches expected
        switch (errorType)
        {
            case "syntax":
                Assert.True(exception is ParseException || exception is LexerException,
                    $"Expected syntax error but got {exception.GetType().Name}: {exception.Message}");
                break;
            case "invalid-type":
                Assert.True(exception is JmesPathTypeException,
                    $"Expected invalid-type error but got {exception.GetType().Name}: {exception.Message}");
                break;
            case "invalid-arity":
                Assert.True(exception is ArityException,
                    $"Expected invalid-arity error but got {exception.GetType().Name}: {exception.Message}");
                break;
            case "unknown-function":
                Assert.True(exception is UnknownFunctionException,
                    $"Expected unknown-function error but got {exception.GetType().Name}: {exception.Message}");
                break;
            case "invalid-value":
                Assert.True(exception is JmesPathException,
                    $"Expected invalid-value error but got {exception.GetType().Name}: {exception.Message}");
                break;
            default:
                Assert.Fail($"Unknown error type: {errorType}");
                break;
        }
    }
}
