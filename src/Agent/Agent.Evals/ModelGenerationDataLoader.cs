using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

public static class ModelGenerationDataLoader
{
    /// <summary>
    /// Loads all JSON files from the specified data folder and deserializes them into ChatMessages.
    /// </summary>
    /// <param name="dataFolderPath">The path to the data folder containing JSON files. Defaults to "Data" folder relative to the application base directory.</param>
    /// <returns>A dictionary with file names as keys and ChatMessages as values.</returns>
    public static async Task<Dictionary<string, ModelGenerationContent>> LoadChatMessagesFromJsonFilesAsync(string? dataFolderPath = null)
    {
        dataFolderPath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

        if (!Directory.Exists(dataFolderPath))
        {
            throw new DirectoryNotFoundException($"The specified data folder path does not exist: {dataFolderPath}");
        }

        var jsonFiles = Directory.GetFiles(dataFolderPath, "*.json", SearchOption.AllDirectories);
        var result = new Dictionary<string, ModelGenerationContent>();
        var jsonOptions = CreateJsonSerializerOptions();

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                var fileName = Path.GetFileName(jsonFile);
                var jsonContent = await File.ReadAllTextAsync(jsonFile);
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

    public static ModelGenerationContent? ParseModelGenerationContent(string content)
    {
        var jsonOptions = CreateJsonSerializerOptions();

        var raw = JsonSerializer.Deserialize<ModelGenerationContentRaw>(content, jsonOptions);

        var result = new ModelGenerationContent
        {
            AgentName = raw.AgentName,
            ModelInput = raw.ModelInput.Select(m => new ChatMessage
            {
                Role = new ChatRole(m.Role),
                Contents = m.Contents.Select(c => c.Value).ToList()
            }).ToArray(),
            ModelOutput = raw.ModelOutput.Select(m => new ChatMessage
            {
                Role = new ChatRole(m.Role),
                Contents = m.Contents.Select(c => c.Value).ToList()
            }).ToArray()
        };

        return result;
    }

    /// <summary>
    /// Creates JSON serializer options with appropriate settings for ChatMessage deserialization.
    /// </summary>
    /// <returns>Configured JsonSerializerOptions object.</returns>
    private static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}
