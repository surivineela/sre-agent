// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading;

namespace Agent.Core.Services
{
    /// <summary>
    /// Default implementation of IThreadContextAccessor using AsyncLocal to flow the ThreadId.
    /// </summary>
    public class ThreadContextAccessor : IThreadContextAccessor
    {
        private static readonly AsyncLocal<Guid?> _current = new();
        public Guid? CurrentThreadId
        {
            get => _current.Value;
            set => _current.Value = value;
        }
    }
}
