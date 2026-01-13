// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Models.Api.v1;

namespace Agent.Core.Services
{
    /// <summary>
    /// Static accessor for thread-scoped context. Uses AsyncLocal for ThreadId
    /// and a ConcurrentDictionary to track test mode per thread.
    /// </summary>
    public static class ThreadContextAccessor
    {
        private static readonly AsyncLocal<Guid?> _currentThreadId = new();
        private static readonly ConcurrentDictionary<Guid, bool> _testModeByThread = new();

        public static Guid? CurrentThreadId
        {
            get => _currentThreadId.Value;
            private set => _currentThreadId.Value = value;
        }

        public static bool IsIncidentTestModeEnabled
        {
            get
            {
                var threadId = _currentThreadId.Value;
                return threadId.HasValue && _testModeByThread.TryGetValue(threadId.Value, out var enabled) && enabled;
            }
            private set
            {
                var threadId = _currentThreadId.Value;
                if (threadId.HasValue)
                {
                    _testModeByThread[threadId.Value] = value;
                }
            }
        }

        public static void SetThreadContext(AgentContext context)
        {
            CurrentThreadId = context.ThreadId;
            if (context.IsIncidentTestModeEnabled.HasValue)
            {
                _testModeByThread[context.ThreadId] = context.IsIncidentTestModeEnabled.Value;
            }
        }


        /// <summary>
        /// Clears test mode state for a specific thread. Call when thread is disposed to prevent memory leaks.
        /// </summary>
        public static void ClearTestMode(Guid threadId)
        {
            _testModeByThread.TryRemove(threadId, out _);
        }
    }
}
