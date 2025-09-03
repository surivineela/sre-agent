using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Data.AgentMemory;

public static class AgentMemoryEmbeddingGeneratorExtensions
{
    public static readonly EmbeddingGenerationOptions DefaultEmbeddingOptions = new()
    {
        Dimensions = Constants.VectorDimension, // Ensure this matches the expected dimensions
    };

    /// <summary>
    /// Generates embeddings for the given content using the specified embedding generator.
    /// </summary>
    /// <param name="generator">The embedding generator to use.</param>
    /// <param name="content">The content to generate embeddings for.</param>
    /// <returns>A task that represents the asynchronous operation, containing the generated embeddings.</returns>
    public static async Task<ReadOnlyMemory<float>> GenerateVectorForAgentMemoryAsync(this IEmbeddingGenerator<string, Embedding<float>> generator, string content, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            // Call the core GenerateAsync so we can inspect Usage metadata for token logging
            var generated = await generator.GenerateAsync(new[] { content }, DefaultEmbeddingOptions, cancellationToken);

            if (generated == null || generated.Count == 0)
            {
                return ReadOnlyMemory<float>.Empty;
            }

            var embedding = generated[0];

            if (generated.Usage != null)
            {
                try
                {
                    var modelId = embedding?.ModelId?.ToString() ?? string.Empty;

                    string model = modelId;
                    string modelVersion = string.Empty;

                    if (!string.IsNullOrEmpty(modelId))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(modelId, "^(.*)-(\\d{4}-\\d{2}-\\d{2})$");
                        if (m.Success && m.Groups.Count == 3)
                        {
                            model = m.Groups[1].Value;
                            modelVersion = m.Groups[2].Value;
                        }
                        else
                        {
                            model = modelId;
                            modelVersion = string.Empty;
                        }
                    }

                    logger.LogTokenConsumption(
                        model,
                        modelVersion,
                        generated.Usage.InputTokenCount ?? 0,
                        generated.Usage.OutputTokenCount ?? 0);
                }
                catch
                {
                    // don't fail the embedding flow because logging failed
                }
            }

            if (embedding == null)
            {
                return ReadOnlyMemory<float>.Empty;
            }

            return embedding.Vector;
        }
        catch (Exception ex)
        {
            try { logger.LogInternalError(ex, "Error generating agent memory vector"); } catch { }
            return ReadOnlyMemory<float>.Empty;
        }
    }
}
