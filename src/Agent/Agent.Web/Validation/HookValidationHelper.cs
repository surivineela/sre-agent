// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Data.DataModels;

namespace Agent.Web.Validation;

/// <summary>
/// Provides validation methods for hook definitions.
/// </summary>
public static class HookValidationHelper
{
    private static readonly string[] ValidHookTypes = { "prompt", "command" };

    /// <summary>
    /// Validates hook definitions and returns a list of validation errors.
    /// </summary>
    /// <param name="hooks">The hooks dictionary to validate.</param>
    /// <returns>A list of validation error messages. Empty if all hooks are valid.</returns>
    public static List<string> ValidateHooks(Dictionary<string, List<HookDefinitionDto>>? hooks)
    {
        var errors = new List<string>();

        if (hooks == null || hooks.Count == 0)
        {
            return errors;
        }

        foreach (var (eventType, definitions) in hooks)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                var hook = definitions[i];
                var prefix = $"hooks.{eventType}[{i}]";

                // Type is required
                if (string.IsNullOrWhiteSpace(hook.Type))
                {
                    errors.Add($"{prefix}.type: Required. Valid values are 'prompt' or 'command'.");
                }
                else if (!ValidHookTypes.Contains(hook.Type, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"{prefix}.type: Invalid value '{hook.Type}'. Valid values are 'prompt' or 'command'.");
                }

                // Matcher required for PostToolUse
                if (eventType.Equals("PostToolUse", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(hook.Matcher))
                    {
                        errors.Add($"{prefix}.matcher: Required for PostToolUse hooks. Use '*' to match all tools.");
                    }
                }

                // Type-specific validation
                var hookType = hook.Type?.ToLowerInvariant();
                if (hookType == "prompt")
                {
                    if (string.IsNullOrWhiteSpace(hook.Prompt))
                    {
                        errors.Add($"{prefix}.prompt: Required for prompt hooks.");
                    }
                }
                else if (hookType == "command")
                {
                    bool hasCommand = !string.IsNullOrWhiteSpace(hook.Command);
                    bool hasScript = !string.IsNullOrWhiteSpace(hook.Script);

                    if (hasCommand && hasScript)
                    {
                        errors.Add($"{prefix}: Cannot specify both 'command' and 'script'. Use one or the other.");
                    }
                    else if (!hasCommand && !hasScript)
                    {
                        errors.Add($"{prefix}: Command hooks require either 'command' or 'script' property.");
                    }

                    // Script size limit (64KB)
                    if (hasScript && hook.Script!.Length > Constants.Hooks.MaxScriptSizeBytes)
                    {
                        errors.Add($"{prefix}.script: Script exceeds maximum size of 64KB ({hook.Script.Length} bytes).");
                    }

                    // Shebang validation
                    if (hasScript)
                    {
                        var firstLine = hook.Script!.Split('\n', 2)[0].Trim();
                        if (!firstLine.StartsWith(Constants.Hooks.BashShebang) && !firstLine.StartsWith(Constants.Hooks.PythonShebang))
                        {
                            errors.Add($"{prefix}.script: Script must start with a valid shebang. Allowed values: '{Constants.Hooks.BashShebang}' or '{Constants.Hooks.PythonShebang}'.");
                        }
                    }
                }

                // FailMode validation (if provided)
                if (!string.IsNullOrWhiteSpace(hook.FailMode))
                {
                    if (!hook.FailMode.Equals("allow", StringComparison.OrdinalIgnoreCase) &&
                        !hook.FailMode.Equals("block", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"{prefix}.failMode: Invalid value '{hook.FailMode}'. Valid values are 'allow' or 'block'.");
                    }
                }

                // Timeout validation
                if (hook.Timeout <= 0)
                {
                    errors.Add($"{prefix}.timeout: Must be greater than 0.");
                }
            }
        }

        return errors;
    }
}
