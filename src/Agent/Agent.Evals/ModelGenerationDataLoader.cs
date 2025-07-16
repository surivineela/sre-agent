using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Agent.Evals;

public static class ModelGenerationDataLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Loads all JSON files from the specified data folder and deserializes them into ChatMessages.
    /// </summary>
    /// <param name="dataFolderPath">The path to the data folder containing JSON files.</param>
    /// <returns>A dictionary with file names as keys and ChatMessages as values.</returns>
    public static Dictionary<string, ModelGenerationContent> LoadChatMessagesFromJsonFilesAsync(string dataFolderPath)
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

    public static ModelGenerationContent? ParseModelGenerationContent(string content)
    {
        var raw = JsonSerializer.Deserialize<ModelGenerationContentRaw>(content, _jsonOptions)!;

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
}
