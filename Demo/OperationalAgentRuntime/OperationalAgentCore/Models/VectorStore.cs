using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System.Text.Json;

namespace OperationalAgentCore.Models
{
    /// <summary>
    /// Each instance of this class will store a vectorized object in the vector store
    /// </summary>
    /// <typeparam name="T">The type of the object we actually want to search/ store</typeparam>
    public class VectorStore<T> where T : class
    {
        InMemoryVectorStore _vectorStore = new InMemoryVectorStore();
        IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        IVectorStoreRecordCollection<int, Embeddable<T>> _vectorStoreRecordCollection;

        int _key = 0;

        public VectorStore(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _embeddingGenerator = embeddingGenerator;
            _vectorStoreRecordCollection = _vectorStore.GetCollection<int, Embeddable<T>>("collection");
        }

        public async Task AddEmbedding(T input, string? searchString = null)
        {
            // Wrap data in an Embeddable object to assist in storing
            var embeddable = new Embeddable<T>() { Key = _key++, SearchString = searchString != null ? searchString : JsonSerializer.Serialize(input), Data = input };

            await _vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync().ConfigureAwait(false);
            embeddable.Vector = await _embeddingGenerator.GenerateEmbeddingVectorAsync(embeddable.SearchString);
            await _vectorStoreRecordCollection.UpsertAsync(embeddable);
        }

        /// <summary>
        /// Returns the top N results
        /// </summary>
        /// <param name="query">String to search for matches to</param>
        /// <param name="top">Number of results to return</param>
        /// <returns></returns>
        public async Task<List<T>> Search(string query, int top = 10)
        {
            var searchOptions = new VectorSearchOptions()
            {
                Top = top,
                VectorPropertyName = "Vector"
            };

            var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(query);

            VectorSearchResults<Embeddable<T>> results = await _vectorStoreRecordCollection.VectorizedSearchAsync(queryEmbedding, searchOptions);

            return await results.Results.Select(result => result.Record.Data).ToListAsync();
        }

        public async Task<T?> SearchSingle(string query)
        {
            var searchOptions = new VectorSearchOptions()
            {
                Top = 1,
                VectorPropertyName = "Vector"
            };

            var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(query);

            var results = await _vectorStoreRecordCollection.VectorizedSearchAsync(queryEmbedding, searchOptions);

            return (await results.Results.Select(result => result.Record.Data).ToListAsync()).FirstOrDefault();
        }
    }

    /// <summary>
    /// You can subclass this to store more information in an object that can be fetched by 
    /// </summary>
    public class Embeddable<T>
    {
        [VectorStoreRecordKey]
        public required int Key { get; set; }

        /// <summary>
        /// This is the string that will be stored in the vector store
        /// </summary>
        [VectorStoreRecordData]
        public required string SearchString { get; set; }

        /// <summary>
        /// This is the object which you want to store in the vector store
        /// </summary>
        public required T Data { get; set; }

        /// <summary>
        /// This is where the actual vector will be stored once it is generated
        /// </summary>
        [VectorStoreRecordVector(384, DistanceFunction.CosineSimilarity)]
        public ReadOnlyMemory<float> Vector { get; set; }
    }
}
