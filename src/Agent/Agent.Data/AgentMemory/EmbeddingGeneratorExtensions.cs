using Microsoft.Extensions.AI;

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
    public static async Task<ReadOnlyMemory<float>> GenerateVectorForAgentMemoryAsync(this IEmbeddingGenerator<string, Embedding<float>> generator, string content, CancellationToken cancellationToken = default)
    {
        return await generator.GenerateVectorAsync(content, DefaultEmbeddingOptions, cancellationToken);
    }
}
