// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Service responsible for loading experiment definitions from YAML files.
/// Experiments are used for A/B testing agent configurations.
/// </summary>
public interface IExperimentLoader
{
    /// <summary>
    /// Gets all loaded experiments.
    /// </summary>
    IReadOnlyList<Experiment> Experiments { get; }

    /// <summary>
    /// Loads experiments from a specific directory.
    /// Useful for loading additional experiments at runtime.
    /// </summary>
    void LoadExperimentsFromDirectory(string directory);

    /// <summary>
    /// Loads a single experiment from YAML content.
    /// </summary>
    Experiment LoadExperimentFromYaml(string yamlContent);

    /// <summary>
    /// Checks if an experiment is force-disabled.
    /// </summary>
    bool IsExperimentDisabled(string experimentId);

    /// <summary>
    /// Gets the forced variant name for an experiment, if one is configured.
    /// Returns null if no variant is forced for the experiment.
    /// </summary>
    string? GetForcedVariant(string experimentId);

    /// <summary>
    /// Parses and adds forced experiment variants from a string.
    /// Format: "experiment1=variant1;experiment2=variant2"
    /// </summary>
    void ParseAndAddForcedVariants(string? forcedVariants);

    /// <summary>
    /// Parses and adds disabled experiments from a string.
    /// Format: "experiment1;experiment2;experiment3" or "*" to disable all
    /// </summary>
    void ParseAndAddDisabledExperiments(string? disabledExperiments);

    /// <summary>
    /// Checks if a feature flag is enabled by any active experiment.
    /// This checks global experiments with system overlay feature flags.
    /// </summary>
    /// <param name="featureFlagName">The name of the feature flag to check.</param>
    /// <returns>True if the feature flag is enabled by any active experiment, false otherwise.</returns>
    bool IsFeatureFlagEnabled(string featureFlagName);
}
