// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Factory for creating tool output processors based on output type.
/// </summary>
public interface IToolOutputProcessorFactory
{
    /// <summary>
    /// Gets a processor for the specified output object type.
    /// </summary>
    /// <param name="outputType">The type of the output object from the tool call</param>
    /// <returns>The appropriate processor for the output type, or null if none matches</returns>
    IToolOutputProcessor? GetProcessorForType(Type outputType);

    /// <summary>
    /// Registers a processor for a specific output type.
    /// </summary>
    /// <param name="outputType">The type of output this processor handles</param>
    /// <param name="processor">The processor to register</param>
    void RegisterProcessorForType(Type outputType, IToolOutputProcessor processor);
}
