// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Factory for creating and managing tool output processors.
/// Supports type-based processor selection.
/// </summary>
public class ToolOutputProcessorFactory : IToolOutputProcessorFactory
{
    private readonly Dictionary<Type, IToolOutputProcessor> _typeProcessors = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolOutputProcessorFactory"/> class.
    /// </summary>
    public ToolOutputProcessorFactory()
    {
    }

    /// <summary>
    /// Initializes a new instance with a collection of processors and their types.
    /// </summary>
    /// <param name="processors">The processors to register with their associated types</param>
    public ToolOutputProcessorFactory(IEnumerable<(Type, IToolOutputProcessor)> processors)
    {
        foreach (var (type, processor) in processors)
        {
            RegisterProcessorForType(type, processor);
        }
    }

    /// <inheritdoc />
    public IToolOutputProcessor? GetProcessorForType(Type outputType)
    {
        ArgumentNullException.ThrowIfNull(outputType);

        // Check for exact type match
        if (_typeProcessors.TryGetValue(outputType, out var processor))
        {
            return processor;
        }

        // Check for assignable types (inheritance/interfaces)
        foreach (var (registeredType, registeredProcessor) in _typeProcessors)
        {
            if (registeredType.IsAssignableFrom(outputType))
            {
                return registeredProcessor;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void RegisterProcessorForType(Type outputType, IToolOutputProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(outputType);
        ArgumentNullException.ThrowIfNull(processor);

        _typeProcessors[outputType] = processor;
    }
}
