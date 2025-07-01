// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Reasoning;

public class ReasoningLoopIterationResult(uint currentIterationCount)
{
    public bool IsContinuation { get; set; } = false;

    // Indicates how many iterations have been completed in the current round of reasoning.
    // We can use this to break out of the reasoning loop if it exceeds a certain threshold.
    public uint CurrentIterationCount { get; } = currentIterationCount;
}
