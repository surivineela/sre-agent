// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;

namespace Agent.Plugins.Models.WorkspaceTools;

/// <summary>
/// Memory-efficient buffer that keeps the first N and last M characters of output.
/// Used for capturing terminal output without unbounded memory growth.
/// </summary>
/// <remarks>
/// Strategy:
/// - Head: First 500 chars in a StringBuilder
/// - Tail: Last 1000 chars using a ring buffer of 2KB chunks
/// - Total memory: ~11KB per session
/// </remarks>
public class ChunkedOutputBuffer
{
    private const int HeadSize = 500;           // First N chars to keep
    private const int TailSize = 1000;          // Last M chars to keep
    private const int ChunkSize = 2048;         // 2KB chunks
    private const int MaxTailChunks = 4;        // Keep 4 chunks = 8KB max

    private readonly StringBuilder _head = new(HeadSize);
    private readonly Queue<string> _tailChunks = new();
    private StringBuilder _currentChunk = new(ChunkSize);
    private bool _headFull;
    private long _totalBytesReceived;
    private readonly object _lock = new();

    /// <summary>
    /// Appends text to the buffer, maintaining head/tail constraints.
    /// </summary>
    public void Append(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        lock (_lock)
        {
            _totalBytesReceived += Encoding.UTF8.GetByteCount(text);

            foreach (var ch in text)
            {
                // Fill head first
                if (!_headFull)
                {
                    _head.Append(ch);
                    if (_head.Length >= HeadSize)
                    {
                        _headFull = true;
                    }
                }

                // Always append to current chunk (for tail)
                _currentChunk.Append(ch);

                // Rotate chunks when full
                if (_currentChunk.Length >= ChunkSize)
                {
                    _tailChunks.Enqueue(_currentChunk.ToString());
                    _currentChunk = new StringBuilder(ChunkSize);

                    // Drop oldest if too many
                    while (_tailChunks.Count > MaxTailChunks)
                    {
                        _tailChunks.Dequeue();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the buffered output with truncation indicator if content was dropped.
    /// </summary>
    public string GetOutput()
    {
        lock (_lock)
        {
            // Build tail from chunks + current
            var tailBuilder = new StringBuilder();
            foreach (var chunk in _tailChunks)
            {
                tailBuilder.Append(chunk);
            }
            tailBuilder.Append(_currentChunk);

            var tail = tailBuilder.ToString();

            // Take only last TailSize chars
            if (tail.Length > TailSize)
            {
                tail = tail.Substring(tail.Length - TailSize);
            }

            var headStr = _head.ToString();

            // Small output - no truncation needed
            if (!_headFull && _tailChunks.Count == 0)
            {
                return headStr;
            }

            // Check if output fits in head + tail without overlap
            if (_totalBytesReceived <= HeadSize + TailSize)
            {
                // Avoid duplicating content - calculate proper offset
                var overlap = Math.Max(0, headStr.Length - (int)(_totalBytesReceived - tail.Length));
                if (overlap < tail.Length)
                {
                    return headStr + tail.Substring(overlap);
                }
                return headStr;
            }

            // Large output - show truncation
            return $"{headStr}\n\n... [{_totalBytesReceived:N0} bytes total, content truncated] ...\n\n{tail}";
        }
    }

    /// <summary>
    /// Clears all buffered content.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _head.Clear();
            _tailChunks.Clear();
            _currentChunk.Clear();
            _headFull = false;
            _totalBytesReceived = 0;
        }
    }

    /// <summary>
    /// Total bytes received since last clear.
    /// </summary>
    public long TotalBytesReceived
    {
        get
        {
            lock (_lock)
            {
                return _totalBytesReceived;
            }
        }
    }
}
