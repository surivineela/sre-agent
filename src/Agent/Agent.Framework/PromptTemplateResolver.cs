// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Agent.Framework;

/// <summary>
/// Resolves template placeholders in prompts using the {{template_name}} syntax.
/// Performs case-insensitive matching and detects circular dependencies.
/// </summary>
public partial class PromptTemplateResolver
{
    private readonly IReadOnlyDictionary<string, IPromptDescriptor> _promptDescriptors;
    private readonly ILogger _logger;

    // Regex pattern to match {{template_name}} - compiled for performance
    [GeneratedRegex(@"\{\{([a-zA-Z0-9_]+)\}\}", RegexOptions.Compiled)]
    private static partial Regex TemplatePattern();

    public PromptTemplateResolver(
        IReadOnlyDictionary<string, IPromptDescriptor> promptDescriptors,
        ILogger logger)
    {
        _promptDescriptors = promptDescriptors ?? throw new ArgumentNullException(nameof(promptDescriptors));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolves all template placeholders in the given text.
    /// </summary>
    /// <param name="text">The text containing template placeholders.</param>
    /// <param name="contextDescription">Optional description of where this prompt is used (e.g., "replacement prompt", "agent instructions") for better error logging.</param>
    /// <param name="agentName">Optional agent name for logging purposes.</param>
    /// <returns>The text with all templates replaced by their content. Returns original text if resolution fails.</returns>
    public string ResolveTemplates(
        string text,
        string? contextDescription = null,
        string? agentName = null)
    {
        var resolvedPrompts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        try
        {
            var resolutionStack = new Stack<string>();
            var resolvedText = ResolveTemplatesRecursive(text, resolvedPrompts, resolutionStack);

            // Log if templates were resolved
            if (resolvedPrompts.Count > 0)
            {
                var contextInfo = !string.IsNullOrEmpty(contextDescription) ? $" in {contextDescription}" : "";
                var agentInfo = !string.IsNullOrEmpty(agentName) ? $" for agent {agentName}" : "";

                _logger.LogInternalInformation(
                    "Resolved {Count} inline template(s){ContextInfo}{AgentInfo}: {Templates}",
                    resolvedPrompts.Count,
                    contextInfo,
                    agentInfo,
                    string.Join(", ", resolvedPrompts)
                );
            }

            return resolvedText;
        }
        catch (Exception ex)
        {
            var contextInfo = !string.IsNullOrEmpty(contextDescription) ? $" in {contextDescription}" : "";
            var agentInfo = !string.IsNullOrEmpty(agentName) ? $" for agent {agentName}" : "";

            _logger.LogInternalError(
                ex,
                "Failed to resolve templates{ContextInfo}{AgentInfo}. Using prompt without templates.",
                contextInfo,
                agentInfo
            );

            // Clear resolved prompts since we're returning original text
            resolvedPrompts.Clear();
            return text;
        }
    }

    /// <summary>
    /// Recursively resolves templates, detecting circular dependencies.
    /// </summary>
    private string ResolveTemplatesRecursive(
        string text,
        HashSet<string> resolvedPrompts,
        Stack<string> resolutionStack)
    {
        var result = TemplatePattern().Replace(text, match =>
        {
            var templateName = match.Groups[1].Value;

            // Check for circular dependency
            if (resolutionStack.Contains(templateName, StringComparer.OrdinalIgnoreCase))
            {
                var cycle = string.Join(" -> ", resolutionStack.Reverse()) + $" -> {templateName}";
                var errorMsg = $"Circular template dependency detected: {cycle}";
                _logger.LogInternalError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            // Try to find the prompt descriptor (case-insensitive)
            var promptDescriptor = _promptDescriptors.FirstOrDefault(
                kvp => string.Equals(kvp.Key, templateName, StringComparison.OrdinalIgnoreCase)
            ).Value;

            if (promptDescriptor == null)
            {
                _logger.LogInternalWarning(
                    "Template placeholder {{{{templateName}}}} not found in available prompts. Leaving as-is.",
                    templateName
                );
                return match.Value; // Keep the placeholder
            }

            // Track that we resolved this prompt
            resolvedPrompts.Add(promptDescriptor.Name);

            // Push onto stack for circular dependency detection
            resolutionStack.Push(templateName);

            try
            {
                // Recursively resolve any nested templates in the prompt content
                var resolvedContent = ResolveTemplatesRecursive(
                    promptDescriptor.Prompt,
                    resolvedPrompts,
                    resolutionStack
                );

                return resolvedContent;
            }
            finally
            {
                // Pop from stack
                resolutionStack.Pop();
            }
        });

        return result;
    }
}
