// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
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
        // private bool _foundProperty;
        // private bool _propertyValueCompleted;

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

        // Buffer for handling surrogate pairs when input is provided char-by-char
        private char? _pendingHighSurrogate;

        private const int InitialBufferSize = 1024;
        private const int MaxBufferSize = 1024 * 1024; // 1MB safety limit

        /// <summary>
        /// Initializes a new instance of the StreamExtractor class.
        /// </summary>
        /// <param name="propertyName">The name of the top-level property to extract.</param>
        /// <param name="onStreamableReceived">Callback invoked asynchronously with each character of the property value as it becomes available.</param>
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
            // _foundProperty = false;
            // _propertyValueCompleted = false;
            _state = ParsingState.LookingForProperty;
            _stringStartIndex = -1;
            _currentParseIndex = 0;
            _isEscaped = false;
        }

        /// <summary>
        /// Appends a piece of JSON content and processes it incrementally, streaming characters
        /// from the target property value as they become available.
        /// </summary>
        /// <param name="content">The content to append (can be a single character or multiple characters).</param>
        public async Task AppendAsync(string content)
        {
            if (string.IsNullOrEmpty(content) || _state == ParsingState.Completed)
            {
                return;
            }

            // Handle surrogate pairs properly when content is provided char-by-char
            // by buffering high surrogates and combining with low surrogates
            StringBuilder processedContent = new StringBuilder();

            for (int i = 0; i < content.Length; i++)
            {
                char currentChar = content[i];

                if (_pendingHighSurrogate.HasValue)
                {
                    // We have a pending high surrogate from previous call
                    if (char.IsLowSurrogate(currentChar))
                    {
                        // Complete the surrogate pair
                        processedContent.Append(_pendingHighSurrogate.Value);
                        processedContent.Append(currentChar);
                        _pendingHighSurrogate = null;
                    }
                    else
                    {
                        // Invalid surrogate pair - high surrogate not followed by low surrogate
                        // Add replacement character to processed content
                        processedContent.Append('\uFFFD');
                        _pendingHighSurrogate = null;

                        // Process current character
                        if (char.IsHighSurrogate(currentChar))
                        {
                            _pendingHighSurrogate = currentChar;
                        }
                        else if (char.IsLowSurrogate(currentChar))
                        {
                            // Orphaned low surrogate
                            processedContent.Append('\uFFFD');
                        }
                        else
                        {
                            processedContent.Append(currentChar);
                        }
                    }
                }
                else if (char.IsHighSurrogate(currentChar))
                {
                    // Save the high surrogate for next character
                    _pendingHighSurrogate = currentChar;
                }
                else if (char.IsLowSurrogate(currentChar))
                {
                    // Orphaned low surrogate without preceding high surrogate
                    processedContent.Append('\uFFFD');
                }
                else
                {
                    // Regular character
                    processedContent.Append(currentChar);
                }
            }

            if (processedContent.Length > 0)
            {
                byte[] contentBytes = Encoding.UTF8.GetBytes(processedContent.ToString());

                // Ensure buffer has enough capacity
                EnsureBufferCapacity(_bufferLength + contentBytes.Length);

                // Append new content to buffer
                Array.Copy(contentBytes, 0, _buffer, _bufferLength, contentBytes.Length);
                _bufferLength += contentBytes.Length;
            }

            // Try to process the buffer
            await ProcessBufferAsync();
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

        private async Task ProcessBufferAsync()
        {
            if (_bufferLength == 0)
            {
                return;
            }

            if (_state == ParsingState.LookingForProperty || _state == ParsingState.FoundPropertyName)
            {
                // Use Utf8JsonReader to find the property
                FindTargetProperty();
            }

            if (_state == ParsingState.InsideStringValue)
            {
                // Manually parse the string value byte-by-byte
                await ParseStringValueAsync();
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

                    if (_state == ParsingState.FoundPropertyName)
                    {
                        // We found the property name, now we expect the string value
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            // Get the position where the string value starts (after the opening quote)
                            _stringStartIndex = (int)reader.TokenStartIndex + 1; // +1 to skip the opening quote
                            _currentParseIndex = _stringStartIndex;
                            _state = ParsingState.InsideStringValue;
                            _isEscaped = reader.ValueIsEscaped;

                            // Save state and switch to manual parsing
                            _readerState = reader.CurrentState;

                            // Remove consumed bytes up to the start of the string value
                            RemoveConsumedBytes(_stringStartIndex);

                            // Reset indices after buffer shift
                            _stringStartIndex = 0;
                            _currentParseIndex = 0;

                            return;
                        }
                    }
                    else if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        // Check if this is our target property
                        if (IsTargetProperty(ref reader))
                        {
                            // _foundProperty = true;
                            _state = ParsingState.FoundPropertyName;
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

        private bool IsTargetProperty(ref Utf8JsonReader reader)
        {
            ReadOnlySpan<byte> propertyName = reader.HasValueSequence
                ? reader.ValueSequence.ToArray()
                : reader.ValueSpan;

            // Case-insensitive comparison
            if (propertyName.Length != _propertyNameBytes.Length)
            {
                return false;
            }

            for (int i = 0; i < propertyName.Length; i++)
            {
                byte b1 = propertyName[i];
                byte b2 = _propertyNameBytes[i];

                // Convert ASCII letters to lowercase for comparison
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
        private async Task ParseStringValueAsync()
        {
            if (!_isEscaped)
            {
                // Simple case: no escape sequences, stream bytes directly
                await ParseUnescapedStringAsync();
            }
            else
            {
                // Complex case: handle escape sequences
                await ParseEscapedStringAsync();
            }
        }

        private async Task ParseUnescapedStringAsync()
        {
            // Source: System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs - ConsumeString method
            // Constants from: System.Text.Json\src\System\Text\Json\JsonConstants.cs

            const byte Quote = (byte)'"';

            int i = 0;
            int dataLength = _bufferLength - _currentParseIndex;
            
            while (i < dataLength)
            {
                byte currentByte = _buffer[_currentParseIndex + i];

                if (currentByte == Quote)
                {
                    // Found the closing quote, we're done
                    _state = ParsingState.Completed;
                    // _propertyValueCompleted = true;
                    return;
                }

                // Stream this character and get number of bytes consumed
                int bytesConsumed = await StreamUtf8CharacterAsync(_currentParseIndex + i);
                if (bytesConsumed == 0)
                {
                    // Not enough data for complete character, wait for more
                    break;
                }
                i += bytesConsumed;
            }

            // Need more data
            _currentParseIndex += i;
        }

        // Adapted from System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs
        // Method: ConsumeStringAndValidate
        private async Task ParseEscapedStringAsync()
        {
            // Source: System.Text.Json\src\System\Text\Json\Reader\Utf8JsonReader.cs - ConsumeStringAndValidate
            // Constants from: System.Text.Json\src\System\Text\Json\JsonConstants.cs

            const byte Quote = (byte)'"';
            const byte BackSlash = (byte)'\\';

            bool nextCharEscaped = false;
            int i = 0;
            int dataLength = _bufferLength - _currentParseIndex;

            while (i < dataLength)
            {
                byte currentByte = _buffer[_currentParseIndex + i];

                if (currentByte == Quote && !nextCharEscaped)
                {
                    // Found the closing quote, we're done
                    _state = ParsingState.Completed;
                    // _propertyValueCompleted = true;
                    return;
                }
                else if (currentByte == BackSlash)
                {
                    nextCharEscaped = !nextCharEscaped;

                    if (!nextCharEscaped)
                    {
                        // This was an escaped backslash, stream it
                        await StreamCharacterAsync("\\");
                        i++;
                    }
                    else
                    {
                        // Start of escape sequence, move to next byte
                        i++;
                    }
                }
                else if (nextCharEscaped)
                {
                    // Handle escape sequences
                    // Source: System.Text.Json\src\System\Text\Json\Reader\JsonReaderHelper.Unescaping.cs - TryUnescape
                    // Create a temporary span for UnescapeCharacter
                    ReadOnlySpan<byte> tempData = new ReadOnlySpan<byte>(_buffer, _currentParseIndex, dataLength);
                    string? unescapedChar = UnescapeCharacter(currentByte, tempData, ref i);
                    if (unescapedChar == null)
                    {
                        // Need more data for \uXXXX sequence
                        _currentParseIndex += i;
                        return;
                    }

                    await StreamCharacterAsync(unescapedChar);
                    nextCharEscaped = false;
                    i++;
                }
                else
                {
                    // Regular character, stream it
                    int bytesConsumed = await StreamUtf8CharacterAsync(_currentParseIndex + i);
                    if (bytesConsumed == 0)
                    {
                        // Not enough data for complete character, wait for more
                        break;
                    }
                    i += bytesConsumed;
                }
            }

            // Need more data
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
            // const byte LineFeed = (byte)'\n';
            // const byte CarriageReturn = (byte)'\r';
            // const byte Tab = (byte)'\t';
            // const byte BackSpace = (byte)'\b';
            // const byte FormFeed = (byte)'\f';

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
            // const int HighSurrogateEndValue = 0xDBFF;
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

        private async Task<int> StreamUtf8CharacterAsync(int bufferIndex)
        {
            // Try to decode a single UTF-8 character starting at bufferIndex
            // Returns the number of bytes consumed
            ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(_buffer, bufferIndex, _bufferLength - bufferIndex);
            
            if (data.Length == 0)
            {
                return 0;
            }
            
            int bytesInChar = GetUtf8CharacterLength(data[0]);

            if (bytesInChar == -1)
            {
                // Invalid UTF-8 first byte, skip it and use replacement character
                await StreamCharacterAsync("\uFFFD"); // Unicode replacement character
                return 1;
            }

            if (bytesInChar > data.Length)
            {
                // Not enough bytes for complete character, wait for more data
                return 0;
            }

            ReadOnlySpan<byte> charBytes = data.Slice(0, bytesInChar);

            try
            {
                // Source: System.Text.Json\src\System\Text\Json\Reader\JsonReaderHelper.Unescaping.cs
                // Using s_utf8Encoding.GetString for UTF-8 decoding
                string character = Encoding.UTF8.GetString(charBytes);
                await StreamCharacterAsync(character);
                return bytesInChar;
            }
            catch (DecoderFallbackException)
            {
                // Invalid UTF-8 sequence, use replacement character
                await StreamCharacterAsync("\uFFFD"); // Unicode replacement character
                return 1;
            }
        }

        // Helper to determine UTF-8 character length from the first byte
        private int GetUtf8CharacterLength(byte firstByte)
        {
            // UTF-8 character length determination
            if ((firstByte & 0x80) == 0) return 1;      // 0xxxxxxx - ASCII
            if ((firstByte & 0xE0) == 0xC0) return 2;   // 110xxxxx
            if ((firstByte & 0xF0) == 0xE0) return 3;   // 1110xxxx
            if ((firstByte & 0xF8) == 0xF0) return 4;   // 11110xxx

            // Invalid UTF-8 first byte (e.g., continuation byte 0x80-0xBF appearing as first byte)
            // This could happen with corrupted data or encoding mismatch
            // Return -1 to signal invalid byte that should be skipped
            return -1;
        }

        private async Task StreamCharacterAsync(string character)
        {
            // Stream the character as a complete unit to preserve surrogate pairs
            // Don't split multi-char sequences (like emojis represented as surrogate pairs)
            if (!string.IsNullOrEmpty(character))
            {
                await _onStreamableReceived(character);
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
            // _foundProperty = false;
            // _propertyValueCompleted = false;
            _state = ParsingState.LookingForProperty;
            _stringStartIndex = -1;
            _currentParseIndex = 0;
            _isEscaped = false;
            _pendingHighSurrogate = null;
        }
    }
}
