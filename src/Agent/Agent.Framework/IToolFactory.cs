using Microsoft.Extensions.AI;

namespace Agent.Framework;

public enum BehaviorOnNameConflict
{
    /// <summary>
    /// If a function with the same name already exists, throw an exception.
    /// </summary>
    ThrowException,

    /// <summary>
    /// If a function with the same name already exists, ignore the new function.
    /// </summary>
    Ignore,

    /// <summary>
    /// If a function with the same name already exists, overwrite the existing function.
    /// </summary>
    Overwrite
}

public interface IToolFactory
{
    /// <summary>
    /// Find an AI function by its name.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Thrown when the function with the specified name is not found.</exception>
    public AIFunction FindAIFunction(string name);

    /// <summary>
    /// Find an AI function by its name and set threadId if the ToolPlugin type has a public ThreadId property of type Guid?
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException">Thrown when the function with the specified name is not found.</exception>
    public AIFunction FindAIFunction(string name, Guid threadId);

    public bool TryFindAIFunction(string name, out AIFunction? function);

    public bool HasAIFunction(string name);
}

// Usage of this attribute is to mark classes that hold tools for agents to use.
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class AgentToolPluginAttribute : Attribute
{
}


