using OperationalAgentCore.Models;
using Xunit.Abstractions;

namespace OperationalAgent.Tests.Integration
{
    [Collection(nameof(CombinedTestCollection))]
    public class VectorStoreTests
    {
        VectorStore<ObjToEmbed> _vectorStore;
        ITestOutputHelper _output;

        public VectorStoreTests(CombinedFixture fixture, ITestOutputHelper output)
        {
            _vectorStore = new VectorStore<ObjToEmbed>(fixture.EmbeddingGeneratorFixture.EmbeddingGenerator);
            _output = output;
        }

        [Fact]
        public void AddEmbedding()
        {
            _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Apple" });
        }

        [Fact]
        public async Task GetEmbedding()
        {
            List<Task> tasks = new List<Task>()
            {
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Apple" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Pear" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Orange" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Bannana" })
            };
            await Task.WhenAll(tasks);

            var res = await _vectorStore.SearchSingle("monkey");
            Assert.Equal("Bannana", res?.Fruit);

            res = await _vectorStore.SearchSingle("isaac newton");
            Assert.Equal("Apple", res?.Fruit);

            res = await _vectorStore.SearchSingle("florida");
            Assert.Equal("Orange", res?.Fruit);
        }

        [Fact]
        public async Task GetMultipleEmbeddings()
        {
            List<Task> tasks = new List<Task>()
            {
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Orange" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Kiwi" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Persimmon" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Avacodo" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Mango" }),
                _vectorStore.AddEmbedding(new ObjToEmbed() { Fruit = "Pear" })
            };
            await Task.WhenAll(tasks);

            List<ObjToEmbed> res = await _vectorStore.Search("orange fruits", 3);
            HashSet<string> strings = new HashSet<string>(res.Select(x => x.Fruit));
            _output.WriteLine($"Fetched: [{string.Join(", ", strings)}]");

            Assert.Equal(3, res.Count());
            Assert.Contains("Orange", strings);
            Assert.Contains("Mango", strings);
            Assert.Contains("Persimmon", strings);
        }

        public class ObjToEmbed
        {
            public required string Fruit { get; set; }
        }
    }
}