// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Framework
{
    /// <summary>
    /// Extracts a specific property value from a streaming JSON input and invokes a callback
    /// character-by-character as content for that property becomes available.
    /// </summary>
    /// <remarks>
    /// This class uses Utf8JsonReader to locate the target property, then manually parses
    /// the string value byte-by-byte to enable true streaming character extraction.
    /// </remarks>
    public class StreamExtractor
    {
        private readonly string _propertyName;
        private readonly Func<string, Task> _onStreamableReceived;
        private readonly byte[] _propertyNameBytes;

        private JsonReaderState _readerState;
        private byte[] _buffer;
        private int _bufferLength;

        // State for manual string parsing
        private enum ParsingState
        {
            LookingForProperty,
            FoundPropertyName,
            InsideStringValue,
            Completed
        }

        private ParsingState _state;
        private int _stringStartIndex;
        private int _currentParseIndex;
        private bool _isEscaped;

        // Accumulate content during each Append call, emit at end of Append
        private StringBuilder _currentChunk;
        private bool _waitingForEscapeSequence; // True if we ended on a backslash and need more data
        private bool _nextCharIsEscaped; // Track escape state across buffer refills

        private const int InitialBufferSize = 1024;
        private const int MaxBufferSize = 1024 * 1024; // 1MB safety limit

        /// <summary>
        /// Initializes a new instance of the StreamExtractor class.
        /// </summary>
        /// <param name="propertyName">The name of the top-level property to extract.</param>
        /// <param name="onStreamableReceived">Callback invoked with each character of the property value as it becomes available.</param>
        public StreamExtractor(string propertyName, Func<string, Task> onStreamableReceived)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("Property name cannot be null or empty.", nameof(propertyName));
            }

            _propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            _onStreamableReceived = onStreamableReceived ?? throw new ArgumentNullException(nameof(onStreamableReceived));
            _propertyNameBytes = Encoding.UTF8.GetBytes(propertyName);

            _readerState = new JsonReaderState(new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            _buffer = new byte[InitialBufferSize];
            _bufferLength = 0;
            _state = ParsingState.LookingForProperty;
            _stringStartIndex = -1;
            _currentParseIndex = 0;
            _isEscaped = false;
            _currentChunk = new StringBuilder();
            _waitingForEscapeSequence = false;
            _nextCharIsEscaped = false;
        }

        /// <summary>
        /// Appends a piece of JSON content and processes it incrementally, streaming characters
        /// from the target property value as they become available.
        /// </summary>
        /// <param name="content">The content to append (can be a single character or multiple characters).</param>
        public async Task AppendAsync(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            // Early return if already completed, but first check if there's a pending chunk to emit
            if (_state == ParsingState.Completed)
            {
                return;
            }

            byte[] contentBytes = Encoding.UTF8.GetBytes(content);

            // Ensure buffer has enough capacity
            EnsureBufferCapacity(_bufferLength + contentBytes.Length);

            // Append new content to buffer
            Array.Copy(contentBytes, 0, _buffer, _bufferLength, contentBytes.Length);
            _bufferLength += contentBytes.Length;

            // Try to process the buffer - content accumulates in _currentChunk during parsing
            ProcessBuffer();

            // Emit accumulated chunk if any, but only if we're not waiting for more data to complete an escape sequence
            // Also emit if we just completed (even if waiting for escape, since there won't be more data)
            if (_currentChunk.Length > 0 && (!_waitingForEscapeSequence || _state == ParsingState.Completed))
            {
                await _onStreamableReceived(_currentChunk.ToString());
                _currentChunk.Clear();
            }
        }

        private void EnsureBufferCapacity(int requiredCapacity)
        {
            if (requiredCapacity > _buffer.Length)
            {
                int newSize = Math.Max(_buffer.Length * 2, requiredCapacity);
                if (newSize > MaxBufferSize)
                {
                    throw new InvalidOperationException($"Buffer size exceeded maximum allowed size of {MaxBufferSize} bytes.");
                }

                byte[] newBuffer = new byte[newSize];
                Array.Copy(_buffer, 0, newBuffer, 0, _bufferLength);
                _buffer = newBuffer;
            }
        }

        private void ProcessBuffer()
        {
            if (_bufferLength == 0)
            {
                return;
            }

            if (_state == ParsingState.LookingForProperty)
            {
                // Use Utf8JsonReader to find the property
                FindTargetProperty();
            }
            else if (_state == ParsingState.FoundPropertyName)
            {
                // We've found the property name but haven't found the string value start yet
                // Try again to find the opening quote
                if (FindStringValueStart(0)) // Start from beginning of buffer after previous removal
                {
                    _state = ParsingState.InsideStringValue;
                    _isEscaped = true;
                }
                // If still not found, wait for more data
            }

            if (_state == ParsingState.InsideStringValue)
            {
                // Manually parse the string value byte-by-byte
                ParseStringValue();
            }
        }

        private void FindTargetProperty()
        {
            ReadOnlySpan<byte> bufferSpan = new ReadOnlySpan<byte>(_buffer, 0, _bufferLength);
            var reader = new Utf8JsonReader(bufferSpan, isFinalBlock: false, _readerState);

            int lastConsumedPosition = 0;

            try
            {
                while (reader.Read())
                {
                    lastConsumedPosition = (int)reader.BytesConsumed;

                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        // Check if this is our target property
                        if (IsTargetProperty(ref reader))
                        {
                            _state = ParsingState.FoundPropertyName;

                            // Save the reader state
                            _readerState = reader.CurrentState;

                            // Now look for the colon and opening quote manually
                            // Start from where the reader left off
                            if (FindStringValueStart(lastConsumedPosition))
                            {
                                _state = ParsingState.InsideStringValue;
                                // Assume all content needs escaping
                                _isEscaped = true;
                                return;
                            }
                            else
                            {
                                // Not enough data yet, wait for more
                                // Remove consumed bytes up to where we've checked
                                RemoveConsumedBytes(lastConsumedPosition);
                                return;
                            }
                        }
                    }
                }

                // Save the state for next append
                _readerState = reader.CurrentState;

                // Remove consumed bytes from buffer
                if (lastConsumedPosition > 0)
                {
                    RemoveConsumedBytes(lastConsumedPosition);
                }
            }
            catch (JsonException)
            {
                // Incomplete JSON, wait for more data
                // The state is already preserved in _readerState
            }
        }

        // Adapted from System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs
        // Method: ReadSingleSegment (whitespace skipping logic at lines 827-836)
        // Method: ConsumeValue (string token detection at line 1049)
        private bool FindStringValueStart(int startIndex)
        {
            // After ConsumePropertyName, the next Read() call skips whitespace and then calls ConsumeValue
            // We replicate this logic here to find the opening quote of the string value

            // Source: Utf8JsonReader.cs lines 827-836 (SkipWhiteSpace check and call)
            // Source: JsonConstants.cs for whitespace byte constants
            const byte Quote = (byte)'"';
            const byte Space = (byte)' ';
            const byte Tab = (byte)'\t';
            const byte CarriageReturn = (byte)'\r';
            const byte LineFeed = (byte)'\n';

            int i = startIndex;

            // Skip whitespace characters as defined by JSON RFC 8259 section 2
            while (i < _bufferLength)
            {
                byte currentByte = _buffer[i];

                // This check is done as an optimization to avoid individual comparisons
                if (currentByte <= Space)
                {
                    if (currentByte == Space || currentByte == Tab ||
                        currentByte == CarriageReturn || currentByte == LineFeed)
                    {
                        i++;
                        continue;
                    }
                }

                // Source: ConsumeValue line 1049 - checks for Quote to call ConsumeString
                if (currentByte == Quote)
                {
                    // Found the opening quote
                    _stringStartIndex = i + 1; // After the quote
                    _currentParseIndex = _stringStartIndex;

                    // Remove bytes up to and including the opening quote
                    RemoveConsumedBytes(i + 1);

                    // Reset indices after buffer shift
                    _stringStartIndex = 0;
                    _currentParseIndex = 0;

                    return true;
                }
                else
                {
                    throw new InvalidOperationException($"Expected '\"' for string value, got 0x{currentByte:X2} at index {i}");
                }
            }

            // Not enough data yet
            return false;
        }

        // Note: System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs provides ValueTextEquals
        // for case-sensitive byte comparison only. Case-insensitive comparison in System.Text.Json
        // happens at a higher level by decoding to strings and using StringComparison.OrdinalIgnoreCase.
        // This implementation performs ASCII case-insensitive comparison at the byte level for efficiency.
        private bool IsTargetProperty(ref Utf8JsonReader reader)
        {
            ReadOnlySpan<byte> propertyName = reader.HasValueSequence
                ? reader.ValueSequence.ToArray()
                : reader.ValueSpan;

            // Case-insensitive ASCII comparison
            if (propertyName.Length != _propertyNameBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < propertyName.Length; i++)
            {
                byte b1 = propertyName[i];
                byte b2 = _propertyNameBytes[i];

                // Convert to lowercase for ASCII letters (A-Z to a-z)
                if (b1 >= 'A' && b1 <= 'Z')
                {
                    b1 = (byte)(b1 + 32);
                }
                if (b2 >= 'A' && b2 <= 'Z')
                {
                    b2 = (byte)(b2 + 32);
                }

                if (b1 != b2)
                {
                    return false;
                }
            }

            return true;
        }

        // Adapted from System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs
        // Method: ConsumeStringAndValidate
        private void ParseStringValue()
        {
            ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(_buffer, _currentParseIndex, _bufferLength - _currentParseIndex);

            if (!_isEscaped)
            {
                // Simple case: no escape sequences, stream bytes directly
                ParseUnescapedString(data);
            }
            else
            {
                // Complex case: handle escape sequences
                ParseEscapedString(data);
            }
        }

        // Adapted from System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs
        // Method: ConsumeString (lines 1279-1318) - validates no backslash or control characters
        // Note: This is a streaming adaptation that accumulates content in chunks instead of using
        // vectorized search, since we need to emit content incrementally as it arrives.
        private void ParseUnescapedString(ReadOnlySpan<byte> data)
        {
            // Source: System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs - ConsumeString method
            // Constants from: System.Text.Json\src\System\Text\Json\JsonConstants.cs

            const byte Quote = (byte)'"';
            const byte BackSlash = (byte)'\\';
            const byte Space = (byte)' '; // 0x20 - control characters are < 0x20

            int startIdx = 0;
            int i = 0;

            while (i < data.Length)
            {
                byte currentByte = data[i];

                // Source: ConsumeString checks for quote, backslash, or control characters
                if (currentByte == Quote)
                {
                    // Append any remaining data before the quote
                    if (i > startIdx)
                    {
                        AppendToChunk(data.Slice(startIdx, i - startIdx));
                    }

                    // Found the closing quote, we're done
                    _state = ParsingState.Completed;
                    _waitingForEscapeSequence = false;
                    _currentParseIndex += i + 1; // Skip past the quote
                    return;
                }
                else if (currentByte == BackSlash || currentByte < Space)
                {
                    // Found escape sequence or control character in supposedly unescaped string
                    // This means _isEscaped should have been true - treat as error
                    throw new InvalidOperationException($"Unexpected character in unescaped string: 0x{currentByte:X2}");
                }

                // Validate UTF-8 character boundaries
                int bytesInChar = GetUtf8CharacterLength(currentByte);

                if (i + bytesInChar > data.Length)
                {
                    // Not enough bytes for complete character, append what we have and wait for more data
                    if (i > startIdx)
                    {
                        AppendToChunk(data.Slice(startIdx, i - startIdx));
                    }
                    _currentParseIndex += i;
                    return;
                }

                i += bytesInChar;
            }

            // Append all the data we processed
            if (i > startIdx)
            {
                AppendToChunk(data.Slice(startIdx, i - startIdx));
            }

            // Need more data
            _currentParseIndex += i;
        }

        // Adapted from System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs
        // Method: ConsumeStringAndValidate (lines 1323-1405)
        // Note: This is a streaming adaptation that accumulates content and emits per Append() call
        private void ParseEscapedString(ReadOnlySpan<byte> data)
        {
            // Source: System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs - ConsumeStringAndValidate
            // Constants from: System.Text.Json\src\System\Text\Json\JsonConstants.cs

            const byte Quote = (byte)'"';
            const byte BackSlash = (byte)'\\';

            // Continue from previous state if we were in the middle of an escape sequence
            bool nextCharEscaped = _nextCharIsEscaped;
            int startIdx = 0;
            int i = 0;

            while (i < data.Length)
            {
                byte currentByte = data[i];

                if (currentByte == Quote && !nextCharEscaped)
                {
                    // Accumulate any remaining unescaped content before the quote
                    if (i > startIdx)
                    {
                        AppendToChunk(data.Slice(startIdx, i - startIdx));
                    }

                    // Found the closing quote, we're done
                    _state = ParsingState.Completed;
                    _waitingForEscapeSequence = false;
                    _nextCharIsEscaped = false;
                    _currentParseIndex += i + 1; // Skip past the quote
                    return;
                }
                else if (currentByte == BackSlash)
                {
                    // Accumulate any content before this backslash
                    if (i > startIdx)
                    {
                        AppendToChunk(data.Slice(startIdx, i - startIdx));
                    }

                    nextCharEscaped = !nextCharEscaped;

                    if (!nextCharEscaped)
                    {
                        // This was an escaped backslash, accumulate it
                        _currentChunk.Append('\\');
                        i++;
                        startIdx = i;
                    }
                    else
                    {
                        // Start of escape sequence, move to next byte
                        i++;
                        startIdx = i;
                    }
                }
                else if (nextCharEscaped)
                {
                    // Handle escape sequences
                    // Source: System.Text.Json\src\System\Text\Json\Reader\JsonReaderHelper.Unescaping.cs - TryUnescape
                    string? unescapedChar = UnescapeCharacter(currentByte, data, ref i);
                    if (unescapedChar == null)
                    {
                        // Need more data for \uXXXX sequence
                        _waitingForEscapeSequence = true;
                        _nextCharIsEscaped = true;
                        _currentParseIndex += i;
                        return;
                    }

                    // Accumulate the unescaped character
                    _currentChunk.Append(unescapedChar);
                    nextCharEscaped = false;
                    i++;
                    startIdx = i;
                }
                else
                {
                    // Regular character - validate UTF-8 boundaries but accumulate for bulk emission
                    int bytesInChar = GetUtf8CharacterLength(currentByte);

                    if (i + bytesInChar > data.Length)
                    {
                        // Not enough bytes for complete character, accumulate what we have and wait for more data
                        if (i > startIdx)
                        {
                            AppendToChunk(data.Slice(startIdx, i - startIdx));
                        }
                        _waitingForEscapeSequence = false;
                        _nextCharIsEscaped = nextCharEscaped;
                        _currentParseIndex += i;
                        return;
                    }

                    i += bytesInChar;
                }
            }

            // Accumulate any remaining content
            if (i > startIdx)
            {
                AppendToChunk(data.Slice(startIdx, i - startIdx));
            }

            // Need more data
            // Track if we're waiting for the rest of an escape sequence
            _waitingForEscapeSequence = nextCharEscaped;
            _nextCharIsEscaped = nextCharEscaped;
            _currentParseIndex += i;
        }

        // Adapted from System.Text.Json\src\System\Text\Json\Reader\JsonReaderHelper.Unescaping.cs
        // Method: TryUnescape
        private string? UnescapeCharacter(byte escapedByte, ReadOnlySpan<byte> data, ref int index)
        {
            // Source: System.Text.Json\src\System\Text\Json\Reader\JsonReaderHelper.Unescaping.cs - TryUnescape
            // Constants from: System.Text.Json\src\System\Text\Json\JsonConstants.cs

            const byte Quote = (byte)'"';
            const byte BackSlash = (byte)'\\';
            const byte Slash = (byte)'/';

            switch (escapedByte)
            {
                case Quote:
                    return "\"";
                case (byte)'n':
                    return "\n";
                case (byte)'r':
                    return "\r";
                case BackSlash:
                    return "\\";
                case Slash:
                    return "/";
                case (byte)'t':
                    return "\t";
                case (byte)'b':
                    return "\b";
                case (byte)'f':
                    return "\f";
                case (byte)'u':
                    // Unicode escape sequence \uXXXX
                    return UnescapeUnicodeSequence(data, ref index);
                default:
                    throw new InvalidOperationException($"Invalid escape sequence: \\{(char)escapedByte}");
            }
        }

        // Adapted from System.Text.Json\src\System\Text\Json\Reader\JsonReaderHelper.Unescaping.cs
        // Method: TryUnescape (unicode handling portion)
        private string? UnescapeUnicodeSequence(ReadOnlySpan<byte> data, ref int index)
        {
            // Source: System.Text.Json\src\System\Text\Json\Reader\JsonReaderHelper.Unescaping.cs - TryUnescape
            // Need 4 hex digits after \u
            if (index + 4 >= data.Length)
            {
                // Not enough data yet
                return null;
            }

            ReadOnlySpan<byte> hexDigits = data.Slice(index + 1, 4);
            if (!Utf8Parser.TryParse(hexDigits, out int scalar, out int bytesConsumed, 'x') || bytesConsumed != 4)
            {
                throw new InvalidOperationException("Invalid unicode escape sequence");
            }

            index += 4; // Move past the 4 hex digits

            // Handle surrogate pairs
            // Constants from: System.Text.Json\src\System\Text\Json\JsonConstants.cs
            const int HighSurrogateStartValue = 0xD800;
            const int LowSurrogateStartValue = 0xDC00;
            const int LowSurrogateEndValue = 0xDFFF;
            const int BitShiftBy10 = 0x400;
            const int UnicodePlane01StartValue = 0x10000;

            if (scalar >= HighSurrogateStartValue && scalar <= LowSurrogateEndValue)
            {
                if (scalar >= LowSurrogateStartValue)
                {
                    throw new InvalidOperationException($"Invalid UTF-16 surrogate: 0x{scalar:X4}");
                }

                // High surrogate, need low surrogate
                if (index + 6 >= data.Length || data[index + 1] != (byte)'\\' || data[index + 2] != (byte)'u')
                {
                    // Not enough data or not a proper surrogate pair
                    return null;
                }

                ReadOnlySpan<byte> lowHexDigits = data.Slice(index + 3, 4);
                if (!Utf8Parser.TryParse(lowHexDigits, out int lowSurrogate, out bytesConsumed, 'x') || bytesConsumed != 4)
                {
                    throw new InvalidOperationException("Invalid unicode escape sequence in surrogate pair");
                }

                if (lowSurrogate < LowSurrogateStartValue || lowSurrogate > LowSurrogateEndValue)
                {
                    throw new InvalidOperationException($"Invalid low surrogate: 0x{lowSurrogate:X4}");
                }

                index += 6; // Move past \uXXXX

                // Calculate the actual unicode scalar value
                scalar = (BitShiftBy10 * (scalar - HighSurrogateStartValue))
                    + (lowSurrogate - LowSurrogateStartValue)
                    + UnicodePlane01StartValue;
            }

            // Convert scalar to string
            var rune = new Rune(scalar);
            return rune.ToString();
        }

        // Helper to determine UTF-8 character length from the first byte
        private static int GetUtf8CharacterLength(byte firstByte)
        {
            // UTF-8 character length determination
            if ((firstByte & 0x80) == 0) return 1;      // 0xxxxxxx - ASCII
            if ((firstByte & 0xE0) == 0xC0) return 2;   // 110xxxxx
            if ((firstByte & 0xF0) == 0xE0) return 3;   // 1110xxxx
            if ((firstByte & 0xF8) == 0xF0) return 4;   // 11110xxx

            throw new InvalidOperationException($"Invalid UTF-8 first byte: 0x{firstByte:X2}");
        }

        // Accumulate content into _currentChunk
        // Similar pattern to how Utf8JsonWriter accumulates before flushing
        private void AppendToChunk(ReadOnlySpan<byte> utf8Bytes)
        {
            // Decode UTF-8 bytes and accumulate in current chunk
            try
            {
                string text = Encoding.UTF8.GetString(utf8Bytes);
                _currentChunk.Append(text);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidOperationException("Invalid UTF-8 sequence in JSON string");
            }
        }

        private void RemoveConsumedBytes(int consumedCount)
        {
            if (consumedCount > 0 && consumedCount < _bufferLength)
            {
                int remainingBytes = _bufferLength - consumedCount;
                Array.Copy(_buffer, consumedCount, _buffer, 0, remainingBytes);
                _bufferLength = remainingBytes;
            }
            else if (consumedCount >= _bufferLength)
            {
                _bufferLength = 0;
            }
        }

        /// <summary>
        /// Resets the extractor to its initial state for reuse.
        /// </summary>
        public void Reset()
        {
            _readerState = new JsonReaderState(new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            _bufferLength = 0;
            _state = ParsingState.LookingForProperty;
            _stringStartIndex = -1;
            _currentParseIndex = 0;
            _isEscaped = false;
            _currentChunk.Clear();
            _waitingForEscapeSequence = false;
            _nextCharIsEscaped = false;
        }
    }
}
