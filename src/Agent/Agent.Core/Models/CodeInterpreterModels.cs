// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;

namespace Agent.Core.Models;

public class CodeExecuteRequest
{
    public CodeExecuteRequestProperties Properties { get; set; } = new();
}

public class CodeExecuteRequestProperties
{
    public string CodeInputType { get; set; } = "inline"; // inline | file (future)
    public string ExecutionType { get; set; } = "synchronous"; // synchronous for now
    public string Code { get; set; } = string.Empty;
}

public class CodeExecutionResponse
{
    public string RawJson { get; set; } = string.Empty;
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public int? ExitCode { get; set; }

    public static CodeExecutionResponse Parse(string json)
    {
        var resp = new CodeExecutionResponse { RawJson = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // Attempt flexible extraction
            Extract(root, ref resp);
        }
        catch { /* keep raw */ }
        return resp;
    }

    private static void Extract(JsonElement element, ref CodeExecutionResponse resp)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                Extract(prop.Value, ref resp);
                continue;
            }
            if (prop.NameEquals("stdout") && prop.Value.ValueKind == JsonValueKind.String) resp.Stdout = prop.Value.GetString();
            if (prop.NameEquals("stderr") && prop.Value.ValueKind == JsonValueKind.String) resp.Stderr = prop.Value.GetString();
            if (prop.NameEquals("exitCode") && prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var ec)) resp.ExitCode = ec;
        }
    }
}
