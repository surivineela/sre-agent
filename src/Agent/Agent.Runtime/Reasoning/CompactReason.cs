// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Reason for triggering chat history compaction.
/// </summary>
public enum CompactReason
{
    /// <summary>
    /// User explicitly requested compaction via command.
    /// </summary>
    UserCommand = 0,

    /// <summary>
    /// Automatic compaction triggered by scheduled task completion.
    /// </summary>
    ScheduledTaskAutoCompact = 1,

    /// <summary>
    /// Automatic compaction triggered by token limit approaching.
    /// </summary>
    TokenLimitAutoCompact = 2
}
