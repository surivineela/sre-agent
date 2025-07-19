using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

public static class ModelGenerationDataLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = AIJsonUtilities.DefaultOptions;

    /// <summary>
    /// Loads all JSON files from the specified data folder and deserializes them into ChatMessages.
    /// </summary>
    /// <param name="dataFolderPath">The path to the data folder containing JSON files.</param>
    /// <returns>A dictionary with file names as keys and ChatMessages as values.</returns>
    public static Dictionary<string, ModelGenerationContent> LoadChatMessagesFromJsonFiles(string dataFolderPath)
    {
        if (!Directory.Exists(dataFolderPath))
        {
            throw new DirectoryNotFoundException($"The specified data folder path does not exist: {dataFolderPath}");
        }

        var jsonFiles = Directory.GetFiles(dataFolderPath, "*.json", SearchOption.AllDirectories);
        var result = new Dictionary<string, ModelGenerationContent>();

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var fileName = Path.GetFileName(jsonFile);
                var jsonContent = File.ReadAllText(jsonFile);
                var modelGeneration = ParseModelGenerationContent(jsonContent);

                if (modelGeneration != null)
                {
                    result[fileName] = modelGeneration;
                }
            }
            catch (JsonException ex)
            {
                // Log or handle JSON deserialization errors
                Console.WriteLine($"Error deserializing JSON file {jsonFile}: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Log or handle other errors
                Console.WriteLine($"Error processing file {jsonFile}: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// Loads all JSON files from the specified data folder and deserializes them into ChatMessages.
    /// </summary>
    /// <param name="dataFolderPath">The path to the data folder containing debugger trace JSON files.</param>
    /// <returns>A dictionary with file names as keys and ChatMessages as values.</returns>
    public static Dictionary<string, ModelGenerationContent> LoadChatMessagesFromDebuggerTraces(string dataFolderPath)
    {
        if (!Directory.Exists(dataFolderPath))
        {
            throw new DirectoryNotFoundException($"The specified data folder path does not exist: {dataFolderPath}");
        }

        var jsonFiles = Directory.GetFiles(dataFolderPath, "*.json", SearchOption.AllDirectories);
        var result = new Dictionary<string, ModelGenerationContent>();

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var fileName = Path.GetFileName(jsonFile);
                var jsonContent = File.ReadAllText(jsonFile);
                var modelGeneration = ParseDebuggerTraceContent(jsonContent);

                if (modelGeneration != null)
                {
                    result[fileName] = modelGeneration;
                }
            }
            catch (JsonException ex)
            {
                // Log or handle JSON deserialization errors
                Console.WriteLine($"Error deserializing JSON file {jsonFile}: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Log or handle other errors
                Console.WriteLine($"Error processing file {jsonFile}: {ex.Message}");
            }
        }

        return result;
    }

    public static ModelGenerationContent? ParseDebuggerTraceContent(string content)
    {
        // 1. top‑level: array of traces
        using var tracesDoc = JsonDocument.Parse(content);
        var traces = tracesDoc.RootElement;

        if (traces.ValueKind != JsonValueKind.Array || traces.GetArrayLength() == 0)
        {
            return null;
        }

        var finalTrace = traces.EnumerateArray().Last();

        // 2. trace contains an array of spans
        if (!finalTrace.TryGetProperty("spans", out var spansProp)
            || spansProp.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        // 3. get last span element with type model_generation
        JsonElement? lastModelSpan = null;
        foreach (var span in spansProp.EnumerateArray().Reverse())
        {
            if (span.TryGetProperty("operationName", out var opName)
                && string.Equals(opName.GetString(), "model_generation", StringComparison.OrdinalIgnoreCase))
            {
                lastModelSpan = span;
                break;
            }
        }

        if (lastModelSpan is null)
        {
            return null;
        }

        // 4. get attributes property of that span
        if (!lastModelSpan.Value.TryGetProperty("attributes", out var attrsProp))
        {
            return null;
        }

        // 5. pull out the three scalar‑string attributes we need
        if (!attrsProp.TryGetProperty("agent.name", out var agentNameProp) ||
            !attrsProp.TryGetProperty("model.input", out var modelInputProp) ||
            !attrsProp.TryGetProperty("model.output", out var modelOutputProp))
        {
            return null;
        }

        var agentName = agentNameProp.GetString();
        var chatInput = JsonSerializer.Deserialize<ModelGenerationContentRaw.Message[]>(modelInputProp.GetString()!, _jsonOptions);
        var chatOutput = JsonSerializer.Deserialize<ModelGenerationContentRaw.Message[]>(modelOutputProp.GetString()!, _jsonOptions);

        if (agentName is null
            || chatInput is null
            || chatOutput is null)
        {
            return null;
        }

        return new ModelGenerationContent
        {
            AgentName = agentName,
            ModelInput = chatInput
                .Select(m => m.ToChatMessage())
                .ToArray(),
            ModelOutput = chatOutput
                .Select(m => m.ToChatMessage())
                .ToArray(),
        };
    }

    private static ModelGenerationContent? ParseModelGenerationContent(string content)
    {
        var raw = JsonSerializer.Deserialize<ModelGenerationContentRaw>(content, _jsonOptions)!;

        var result = new ModelGenerationContent
        {
            AgentName = raw.AgentName,
            ModelInput = raw.ModelInput
                .Select(m => m.ToChatMessage())
                .ToArray(),
            ModelOutput = raw.ModelOutput
                .Select(m => m.ToChatMessage())
                .ToArray(),
        };

        return result;
    }
}
