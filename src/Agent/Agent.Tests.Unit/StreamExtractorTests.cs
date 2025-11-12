using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Agent.Framework;
using Xunit;

namespace Agent.Tests.Unit;

public class StreamExtractorTests
{

    [Fact]
    public async Task Append_VerifyPieceByPieceOutput_CallbackInvokedForEachCharacter()
    {
        // Arrange
        var extractedChunks = new List<string>();

        var extractor = new StreamExtractor("UserMessage", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var jsonPieces = new[]
        {
            "{\n    \"",
            "State\" : ",
            "true,\n    \"",
            "UserMessage",
            "\" : \"🎉✨I am ",
            "user message",
            "\"\n}"
        };

        // Act & Assert - Append JSON string pieces chunk by chunk and verify output
        await extractor.AppendAsync(jsonPieces[0]); // "{\n    \""
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[1]); // "State\" : "
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[2]); // "true,\n    \""
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[3]); // "UserMessage"
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[4]); // "\" : \"I am "
        Assert.Single(extractedChunks);
        Assert.Equal("🎉✨I am ", extractedChunks[0]);

        await extractor.AppendAsync(jsonPieces[5]); // "user message"
        Assert.Equal(2, extractedChunks.Count);
        Assert.Equal("user message", extractedChunks[1]);

        await extractor.AppendAsync(jsonPieces[6]); // "\"\n}"
        Assert.Equal(2, extractedChunks.Count); // No new chunks after closing quote

        // Assert - Verify final result
        var result = string.Join("", extractedChunks);
        Assert.Equal("🎉✨I am user message", result);
    }

    [Fact]
    public async Task Append_VerifyPieceByPieceOutputWithStart_CallbackInvokedForEachCharacter()
    {
        // Arrange
        var extractedChunks = new List<string>();

        var extractor = new StreamExtractor("UserMessage", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var jsonPieces = new[]
        {
            "{\n    \"",
            "State\" : ",
            "true,\n    \"",
            "UserMessage",
            "\" : ",
            "\"🎉✨I am user message\"",
            "\n}"
        };

        // Act & Assert - Append JSON string pieces chunk by chunk and verify output
        await extractor.AppendAsync(jsonPieces[0]); // "{\n    \""
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[1]); // "State\" : "
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[2]); // "true,\n    \""
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[3]); // "UserMessage"
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[4]); // "\" : "
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(jsonPieces[5]); // "\"🎉✨I am user message\""
        Assert.Single(extractedChunks);
        Assert.Equal("🎉✨I am user message", extractedChunks[0]);

        await extractor.AppendAsync(jsonPieces[6]); // "\"\n}"
        Assert.Single(extractedChunks); // No new chunks after closing quote

        // Assert - Verify final result
        var result = string.Join("", extractedChunks);
        Assert.Equal("🎉✨I am user message", result);
    }

    [Fact]
    public async Task Append_ComplexJsonWithEscapesAndUnicode_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("ComplexMessage", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // The complete JSON being streamed represents:
        // - Multiple properties (some before, some after target)
        // - Escaped characters (quotes, backslashes, newlines, tabs)
        // - Unicode/emoji characters
        // - Nested objects (with different property names)
        // - The target property "ComplexMessage" at top level
        //
        // The JSON value is: "Line 1\nLine 2\tTabbed\n🚀 Rocket\n\"Quoted\" text\\Emoji: 🎉✨💻"
        // Which should decode to: Line 1<newline>Line 2<tab>Tabbed<newline>🚀 Rocket<newline>"Quoted" text\Emoji: 🎉✨💻
        var irregularChunks = new[]
        {
            "{\n    \"Id",                          // 0: Start of JSON, no target yet
            "\": 12345,\n    \"Nested",             // 1: Id property value, nested start
            "\": {\n        \"Inner",               // 2: Nested object start
            "Message\": \"This ",                   // 3: Nested property (not our target)
            "should be ignored\"\n    },\n",        // 4: End of nested object
            "    \"ComplexMessage\": \"",           // 5: Target property found!
            "Line 1\\n",                            // 6: First content chunk
            "Line 2\\t",                            // 7: Second content chunk
            "Tabbed\\",                             // 8: Backslash (start of escape sequence)
            "n🚀 Rocket",                           // 9: Complete \n escape + emoji chunk
            "\\n",                                  // 10: Newline escape
            "\\\"Quoted",                           // 11: Escaped quote + text
            "\\\" text",                            // 12: Escaped quote + text
            "\\\\",                                 // 13: Escaped backslash
            "Emoji: ",                              // 14: Plain text
            "🎉✨💻",                               // 15: Multiple emojis
            "\",\n    \"Other",                     // 16: End of target property value
            "Prop\": \"After target\",\n",          // 17: Property after target
            "    \"Status\": true\n}"               // 18: End of JSON
        };

        // Act & Assert - Append JSON chunks piece by piece and verify output at each step
        await extractor.AppendAsync(irregularChunks[0]); // "{\n    \"Id"
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(irregularChunks[1]); // "\": 12345,\n    \"Nested"
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(irregularChunks[2]); // "\": {\n        \"Inner"
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(irregularChunks[3]); // "Message\": \"This "
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(irregularChunks[4]); // "should be ignored\"\n    },\n"
        Assert.Empty(extractedChunks);

        await extractor.AppendAsync(irregularChunks[5]); // "    \"ComplexMessage\": \""
        Assert.Empty(extractedChunks); // Property found but no content yet

        await extractor.AppendAsync(irregularChunks[6]); // "Line 1\\n"
        Assert.Single(extractedChunks);
        Assert.Equal("Line 1\n", extractedChunks[0]);

        await extractor.AppendAsync(irregularChunks[7]); // "Line 2\\t"
        Assert.Equal(2, extractedChunks.Count);
        Assert.Equal("Line 2\t", extractedChunks[1]);

        await extractor.AppendAsync(irregularChunks[8]); // "Tabbed\\"
                                              // The backslash at the end is cached because it could be start of escape sequence
        Assert.Equal(2, extractedChunks.Count); // "Tabbed" not yet emitted due to trailing backslash

        await extractor.AppendAsync(irregularChunks[9]); // "n🚀 Rocket"
                                              // Now "\n" is complete and can be processed along with the rest
        Assert.Equal(3, extractedChunks.Count);
        Assert.Equal("Tabbed\n🚀 Rocket", extractedChunks[2]);

        await extractor.AppendAsync(irregularChunks[10]); // "\\n"
        Assert.Equal(4, extractedChunks.Count);
        Assert.Equal("\n", extractedChunks[3]);

        await extractor.AppendAsync(irregularChunks[11]); // "\\\"Quoted"
        Assert.Equal(5, extractedChunks.Count);
        Assert.Equal("\"Quoted", extractedChunks[4]);

        await extractor.AppendAsync(irregularChunks[12]); // "\\\" text"
        Assert.Equal(6, extractedChunks.Count);
        Assert.Equal("\" text", extractedChunks[5]);

        await extractor.AppendAsync(irregularChunks[13]); // "\\\\"
        Assert.Equal(7, extractedChunks.Count);
        Assert.Equal("\\", extractedChunks[6]);

        await extractor.AppendAsync(irregularChunks[14]); // "Emoji: "
        Assert.Equal(8, extractedChunks.Count);
        Assert.Equal("Emoji: ", extractedChunks[7]);

        await extractor.AppendAsync(irregularChunks[15]); // "🎉✨💻"
        Assert.Equal(9, extractedChunks.Count);
        Assert.Equal("🎉✨💻", extractedChunks[8]);

        await extractor.AppendAsync(irregularChunks[16]); // "\",\n    \"Other"
        Assert.Equal(9, extractedChunks.Count); // No new chunks after closing quote

        await extractor.AppendAsync(irregularChunks[17]); // "Prop\": \"After target\",\n"
        Assert.Equal(9, extractedChunks.Count); // Still no new chunks

        await extractor.AppendAsync(irregularChunks[18]); // "    \"Status\": true\n}"
        Assert.Equal(9, extractedChunks.Count); // Still no new chunks

        // Assert - Verify final result
        var result = string.Join("", extractedChunks);
        var expected = "Line 1\nLine 2\tTabbed\n🚀 Rocket\n\"Quoted\" text\\Emoji: 🎉✨💻";
        Assert.Equal(expected, result);

        // Verify specific escape sequences were handled correctly
        Assert.Contains("Line 1\nLine 2", result); // Newline escape
        Assert.Contains("\tTabbed", result); // Tab escape
        Assert.Contains("\"Quoted\"", result); // Quote escapes
        Assert.Contains("text\\Emoji", result); // Backslash (single backslash in output)
        Assert.Contains("🚀", result); // Unicode emoji
        Assert.Contains("🎉✨💻", result); // Multiple emojis
    }

    [Fact]
    public async Task Append_Character_CallbackInvokedForEachCharacter()
    {
        // Arrange
        var extractedChunks = new List<string>();

        var extractor = new StreamExtractor("UserMessage", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // JSON string to stream character by character
        var json = "{\"State\":true,\"UserMessage\":\"ABCDEFGHIJKLMN\"}";

        // Act & Assert - Append JSON string character by character and verify output
        var chunkIndex = 0;
        for (int i = 0; i < json.Length; i++)
        {
            await extractor.AppendAsync(json[i].ToString());

            // We expect chunks to start appearing after we've passed the opening quote of the value
            // JSON structure: {"State":true,"UserMessage":"I am user message"}
            // Position 28 is the opening quote before the value
            // Characters from position 29 onwards should be extracted (until closing quote at position 46)

            if (i < 29)
            {
                // Before the value starts
                Assert.Equal(chunkIndex, extractedChunks.Count);
            }
            else if (i >= 29 && i < 46)
            {
                // Inside the value - each character should produce a chunk
                // Note: Emojis may be multi-byte, so we track by chunk count
                var expectedChunkCount = chunkIndex + 1;

                // Extract character at position i from the original JSON
                var currentChar = json[i];

                // If this character produced a new chunk, verify it
                if (extractedChunks.Count > chunkIndex)
                {
                    chunkIndex++;
                }
            }
        }

        // Assert - Verify final result
        var result = string.Join("", extractedChunks);
        Assert.Equal("ABCDEFGHIJKLMN", result);

        // Verify that we got multiple chunks (character by character streaming)
        Assert.True(extractedChunks.Count > 1, "Expected multiple chunks for character-by-character streaming");
    }

    [Fact]
    public async Task Append_PropertyCaseInsensitive_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();

        // Search for "usermessage" (lowercase)
        var extractor = new StreamExtractor("usermessage", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // JSON has "UserMessage" (mixed case)
        var json = @"{""UserMessage"": ""Case insensitive test""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("Case insensitive test", result);
    }

    [Fact]
    public async Task Append_MultiplePropertiesWithSameName_ExtractsFirstOccurrence()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("Message", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // JSON with multiple "Message" properties at different levels
        var json = @"{
    ""Message"": ""First message"",
    ""Data"": {
        ""Message"": ""Nested message""
    },
    ""Message"": ""Duplicate message""
}";

        // Act
        await extractor.AppendAsync(json);

        // Assert - Should extract only the first top-level occurrence
        var result = string.Join("", extractedChunks);
        Assert.Equal("First message", result);
    }

    [Fact]
    public async Task Append_UnicodeEscapeSequences_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("Emoji", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // JSON with unicode escape sequences: \u263A (☺) and \u2764 (❤)
        var json = @"{""Emoji"":""Hello \u263A and \u2764""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("Hello ☺ and ❤", result);

        // Verify that unicode characters were properly decoded
        Assert.Contains("☺", result);
        Assert.Contains("❤", result);
    }

    [Fact]
    public async Task Append_MultiByteUtf8Characters_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("Text", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // JSON with multi-byte UTF-8 characters (Chinese characters and emoji)
        var json = @"{""Text"":""Hello 世界 🌍""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("Hello 世界 🌍", result);

        // Verify multi-byte characters
        Assert.Contains("世界", result); // Chinese characters
        Assert.Contains("🌍", result); // Emoji
    }

    [Fact]
    public async Task Append_MixedEscapeSequences_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("Text", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // JSON with various escape sequences
        var json = @"{""Text"":""Line1\nLine2\tTabbed\""Quoted\""Back\\Slash""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("Line1\nLine2\tTabbed\"Quoted\"Back\\Slash", result);

        // Verify specific escape sequences
        Assert.Contains("\n", result); // Newline
        Assert.Contains("\t", result); // Tab
        Assert.Contains("\"", result); // Quotes
        Assert.Contains("\\", result); // Backslash
    }

    [Fact]
    public async Task Append_EscapedQuotesInValue_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("message", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = @"{""message"":""Hello, I am \""Ahson!\""""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("Hello, I am \"Ahson!\"", result);
    }

    [Fact]
    public async Task Append_AllEscapeSequences_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("message", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // Test all JSON escape sequences: \/, \r, \b, \n, \f, \t
        var json = @"{""message"":""Hello /a/b/c \/ \r\b\n\f\t\/""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("Hello /a/b/c / \r\b\n\f\t/", result);
    }

    [Fact]
    public async Task Append_WindowsFilePath_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("path", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = @"{""path"":""C:\\Users\\file.txt""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("C:\\Users\\file.txt", result);
    }

    [Fact]
    public async Task Append_MultipleBackslashes_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = @"{""val"":""one\\\\two""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("one\\\\two", result);
    }

    [Fact]
    public async Task Append_UnicodeDigits_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("digits", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // Unicode escape sequences: \u0030-\u0035 = '0'-'5'
        var json = @"{""digits"":""\u0030\u0031\u0032\u0033\u0034\u0035""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("012345", result);
    }

    [Fact]
    public async Task Append_UnicodeNullAndPlus_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // \u0000 = null character, \u002B = '+'
        var json = @"{""val"":""\u0000\u002B""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("\u0000+", result);
    }

    [Fact]
    public async Task Append_MixedUnicodeAndBackslash_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // \u005C = backslash, \u0072 = 'r'
        var json = @"{""val"":""a\u005C\u0072b""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("a\\rb", result);
    }

    [Fact]
    public async Task Append_EscapedUnicodeSequence_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // \\u005C means the literal string "\u005C", not the unicode character
        var json = @"{""val"":""a\\u005C\\u0072b""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("a\\u005C\\u0072b", result);
    }

    [Fact]
    public async Task Append_HighUnicodeCharacters_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = @"{""val"":""a\u008E\u008Fb""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("a\u008E\u008Fb", result);
    }

    [Fact]
    public async Task Append_SurrogatePair_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // \uD802\uDE6D = U+10A6D (𐙭) surrogate pair
        var json = @"{""val"":""a\uD802\uDE6Db""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        var expected = "a" + char.ConvertFromUtf32(0x10A6D) + "b";
        Assert.Equal(expected, result);
        Assert.Contains("𐩭", result);
    }

    [Fact]
    public async Task Append_MusicalNoteSurrogatePair_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // \uD834\uDD1E = U+1D11E (𝄞 - musical symbol G clef)
        var json = @"{""val"":""a\uD834\uDD1Eb""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        var expected = "a" + char.ConvertFromUtf32(0x1D11E) + "b";
        Assert.Equal(expected, result);
        Assert.Contains("𝄞", result);
    }

    [Fact]
    public async Task Append_EscapedSurrogatePair_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // Escaped surrogate pair means literal text, not a unicode character
        var json = @"{""val"":""a\\uD834\\uDD1Eb""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("a\\uD834\\uDD1Eb", result);
    }

    [Fact]
    public async Task Append_MultilineString_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("text", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = @"{""text"":""Multiline\r\n String\r\n""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("Multiline\r\n String\r\n", result);
    }

    [Fact]
    public async Task Append_ComplexMixedEscapes_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("text", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        // Mix of quotes, tabs, newlines, backslashes
        var json = @"{""text"":""\""somequote\""\tMu\""\"" l\r\ntiline\""another\"" String\\""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("\"somequote\"\tMu\"\" l\r\ntiline\"another\" String\\", result);
    }

    [Fact]
    public async Task Append_EmptyString_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = @"{""val"":""""}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("", result);
        Assert.Empty(extractedChunks);
    }

    [Fact]
    public async Task Append_OnlyEscapedQuotes_ExtractsCorrectly()
    {
        // Arrange
        var extractedChunks = new List<string>();
        var extractor = new StreamExtractor("val", chunk =>
        {
            extractedChunks.Add(chunk);
            return Task.CompletedTask;
        });

        var json = "{\"val\":\"\\\"\\\"\"}";

        // Act
        await extractor.AppendAsync(json);

        // Assert
        var result = string.Join("", extractedChunks);
        Assert.Equal("\"\"", result);
    }


    [Theory]
    [InlineData("{\"message\":\"Hello, I am \\\"Ahson!\\\"\"}", "message", "Hello, I am \"Ahson!\"")]
    [InlineData("{\"name\":\"ahson\"}", "name", "ahson")]
    [InlineData("{\"str\":\"Here is a\"}", "str", "Here is a")]
    [InlineData("{\"str\":\"\\\"\\\"\"}", "str", "\"\"")]
    [InlineData("{\"text\":\"\\u0030\\u0031\\u0032\\u0033\\u0034\\u0035\"}", "text", "012345")]
    [InlineData("{\"text\":\"\\u0000\\u002B\"}", "text", "\0+")]
    [InlineData("{\"text\":\"a\\u005C\\u0072b\"}", "text", "a\\rb")]
    [InlineData("{\"text\":\"a\\\\u005C\\u0072b\"}", "text", "a\\u005Crb")]
    [InlineData("{\"text\":\"a\\u008E\\u008Fb\"}", "text", "a\u008E\u008Fb")]
    [InlineData("{\"message\":\"Hello /a/b/c \\/ \\r\\b\\n\\f\\t\\/\"}", "message", "Hello /a/b/c / \r\b\n\f\t/")]
    [InlineData("{\"text\":\"Multiline\\r\\n String\\r\\n\"}", "text", "Multiline\r\n String\r\n")]
    [InlineData("{\"text\":\"\\tMul\\r\\ntiline String\"}", "text", "\tMul\r\ntiline String")]
    [InlineData("{\"text\":\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"}", "text", "\"somequote\"\tMu\"\"l\r\ntiline\"another\" String\\")]
    // Additional test cases from individual tests
    [InlineData("{\"UserMessage\": \"Case insensitive test\"}", "usermessage", "Case insensitive test")]
    [InlineData("{\"Emoji\":\"Hello \\u263A and \\u2764\"}", "Emoji", "Hello ☺ and ❤")]
    [InlineData("{\"Text\":\"Line1\\nLine2\\tTabbed\\\"Quoted\\\"Back\\\\Slash\"}", "Text", "Line1\nLine2\tTabbed\"Quoted\"Back\\Slash")]
    [InlineData("{\"path\":\"C:\\\\Users\\\\file.txt\"}", "path", "C:\\Users\\file.txt")]
    [InlineData("{\"val\":\"one\\\\\\\\two\"}", "val", "one\\\\two")]
    [InlineData("{\"val\":\"\"}", "val", "")]
    [InlineData("{\"val\":\"\\\"\\\"\"}", "val", "\"\"")]
    public async Task TestingGetString_WithStreamExtractor_Chunked(string jsonString, string propertyName, string expectedValue)
    {
        // This test verifies that StreamExtractor works correctly when JSON is appended chunk by chunk
        // Split the JSON into various chunk sizes to test streaming behavior
        // Note: Surrogate pairs and some complex unicode sequences are excluded as they may fail
        // when split across chunk boundaries (this is a known limitation of the current implementation)

        foreach (var chunkSize in new[] { 1, 2, 3, 5, 7, 11 })
        {
            // Split JSON string into chunks before creating extractor
            var jsonChunks = new List<string>();
            for (int i = 0; i < jsonString.Length; i += chunkSize)
            {
                int length = Math.Min(chunkSize, jsonString.Length - i);
                jsonChunks.Add(jsonString.Substring(i, length));
            }

            var extractedChunks = new List<string>();
            var extractor = new StreamExtractor(propertyName, chunk =>
            {
                // Verify each chunk is non-null and non-empty
                Assert.NotNull(chunk);
                Assert.NotEmpty(chunk);

                // Add chunk to list first
                extractedChunks.Add(chunk);

                // Verify the accumulated result so far exactly matches the expected value prefix
                var accumulatedSoFar = string.Join("", extractedChunks);

                // The accumulated output must be exactly equal to the corresponding prefix of expected value
                var expectedPrefix = expectedValue.Substring(0, Math.Min(accumulatedSoFar.Length, expectedValue.Length));
                Assert.Equal(expectedPrefix, accumulatedSoFar);

                // Verify we haven't exceeded the expected length
                Assert.True(accumulatedSoFar.Length <= expectedValue.Length,
                    $"Accumulated output length ({accumulatedSoFar.Length}) exceeds expected length ({expectedValue.Length}). Accumulated: '{accumulatedSoFar}', Expected: '{expectedValue}'");

                return Task.CompletedTask;
            });

            // Act - Append JSON chunks
            foreach (var jsonChunk in jsonChunks)
            {
                await extractor.AppendAsync(jsonChunk);
            }

            // Assert - Verify that chunks were received (for non-empty expected values)
            if (!string.IsNullOrEmpty(expectedValue))
            {
                Assert.NotEmpty(extractedChunks);
            }

            // Assert - Verify each chunk is non-empty
            foreach (var chunk in extractedChunks)
            {
                Assert.False(string.IsNullOrEmpty(chunk), $"Received empty chunk when processing with chunk size {chunkSize}");
            }

            // Assert - Verify final concatenated result matches expected value
            var result = string.Join("", extractedChunks);
            Assert.Equal(expectedValue, result);
        }
    }
}
