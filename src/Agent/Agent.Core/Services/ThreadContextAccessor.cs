// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Models.Api.v1;

namespace Agent.Core.Services
{
    /// <summary>
    /// Static accessor for thread-scoped context. Uses AsyncLocal for ThreadId
    /// and a ConcurrentDictionary to track retro mode per thread.
    /// </summary>
    public static class ThreadContextAccessor
    {
        private static readonly AsyncLocal<Guid?> _currentThreadId = new();
        private static readonly ConcurrentDictionary<Guid, bool> _retroModeByThread = new();

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
                return threadId.HasValue && _retroModeByThread.TryGetValue(threadId.Value, out var enabled) && enabled;
            }
            private set
            {
                var threadId = _currentThreadId.Value;
                if (threadId.HasValue)
                {
                    _retroModeByThread[threadId.Value] = value;
                }
            }
        }

        public static void SetThreadContext(AgentContext context)
        {
            CurrentThreadId = context.ThreadId;
            if (context.IsIncidentTestModeEnabled.HasValue)
            {
                _retroModeByThread[context.ThreadId] = context.IsIncidentTestModeEnabled.Value;
            }
        }


        /// <summary>
        /// Clears retro mode state for a specific thread. Call when thread is disposed to prevent memory leaks.
        /// </summary>
        public static void ClearRetroMode(Guid threadId)
        {
            _retroModeByThread.TryRemove(threadId, out _);
        }
    }
}
