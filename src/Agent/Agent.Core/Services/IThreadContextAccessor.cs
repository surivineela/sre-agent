// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;

namespace Agent.Core.Services
{
    /// <summary>
    /// Provides access to the current Thread (conversation) Id flowing through the agent pipeline.
    /// Uses AsyncLocal to flow context across async calls within the same logical operation.
    /// </summary>
    public interface IThreadContextAccessor
    {
        Guid? CurrentThreadId { get; set; }
    }
}
