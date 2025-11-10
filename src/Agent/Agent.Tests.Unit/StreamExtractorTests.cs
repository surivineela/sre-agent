using System;
using System.Collections.Generic;
using System.Text;
using Agent.Framework;
using Xunit;

namespace Agent.Tests.Unit;

public class StreamExtractorTests
{
    [Fact]
    public async Task Append_RealWorldExample_ExtractsUserMessageCharByChar()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("UserMessage", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = @"{
    ""UserMessage"" : ""🎉✨I'm user message"",
    ""State"" : true,
}";

        // Act - Append char by char as specified in requirements
        foreach (char c in json)
        {
            await extractor.AppendAsync(c.ToString());
        }

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("🎉✨I'm user message", result);
    }

    [Fact]
    public async Task Append_VerifyPieceByPieceOutput_CallbackInvokedForEachCharacter()
    {
        // Arrange
        var expectedMessage = "I am user message";
        var currentIndex = 0;

        var extractor = new StreamExtractor("UserMessage", chunk =>
        {
            // Assert - Verify each chunk matches the expected character at the current position
            Assert.True(currentIndex < expectedMessage.Length,
                $"Callback invoked more times than expected. Index: {currentIndex}, Expected length: {expectedMessage.Length}");

            var expectedChar = expectedMessage[currentIndex].ToString();
            Assert.Equal(expectedChar, chunk);

            currentIndex++;
            return Task.CompletedTask;
        });

        var json = @"{
    ""State"" : true,
    ""UserMessage"" : ""I am user message""
}";

        // Act - Append char by char as specified in requirements
        foreach (char c in json)
        {
            await extractor.AppendAsync(c.ToString());
        }

        // Assert - Verify all expected characters were received
        Assert.Equal(expectedMessage.Length, currentIndex);
    }
}
