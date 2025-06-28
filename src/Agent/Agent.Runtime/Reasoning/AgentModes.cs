namespace Agent.Runtime.Reasoning;

/// <summary>
/// Constants for agent modes to avoid hardcoded strings throughout the controller
/// </summary>
public static class AgentModes
{
    public const string ReadOnly = "ReadOnly";
    public const string Review = "Review";
    public const string Autonomous = "Autonomous";

    /// <summary>
    /// Gets all available agent modes
    /// </summary>
    public static readonly string[] All = { ReadOnly, Review, Autonomous };

    /// <summary>
    /// Gets the available modes based on the global default mode
    /// </summary>
    /// <param name="globalDefaultMode">The global default agent mode</param>
    /// <returns>Array of available modes for the given global default</returns>
    public static string[] GetAvailableModesFor(string globalDefaultMode)
    {
        var normalizedGlobalMode = (globalDefaultMode?.Trim() ?? Review).ToUpperInvariant();

        return normalizedGlobalMode switch
        {
            "READONLY" => new[] { ReadOnly },
            "REVIEW" => new[] { ReadOnly, Review },
            "AUTONOMOUS" => new[] { ReadOnly, Review, Autonomous },
            _ => new[] { ReadOnly, Review } // Fallback to Review mode behavior for unknown global modes
        };
    }

    /// <summary>
    /// Validates if a requested mode is allowed based on the global default mode
    /// </summary>
    /// <param name="globalDefaultMode">The global default agent mode</param>
    /// <param name="requestedMode">The requested agent mode</param>
    /// <returns>True if the mode change is valid, false otherwise</returns>
    public static bool IsValidModeChange(string globalDefaultMode, string requestedMode)
    {
        var availableModes = GetAvailableModesFor(globalDefaultMode);
        return availableModes.Any(mode => string.Equals(requestedMode?.Trim(), mode, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the validation error message for an invalid mode change
    /// </summary>
    /// <param name="globalDefaultMode">The global default agent mode</param>
    /// <returns>Error message describing the allowed modes</returns>
    public static string GetValidationErrorMessage(string globalDefaultMode)
    {
        var normalizedGlobalMode = (globalDefaultMode?.Trim() ?? Review).ToUpperInvariant();

        return normalizedGlobalMode switch
        {
            "READONLY" => "When global agent mode is ReadOnly, threads can only be set to ReadOnly mode.",
            "REVIEW" => "When global agent mode is Review, threads can only be set to ReadOnly or Review mode.",
            "AUTONOMOUS" => "Invalid agent mode. Allowed modes are: ReadOnly, Review, Autonomous.",
            _ => "Invalid agent mode. Allowed modes are: ReadOnly, Review."
        };
    }
}