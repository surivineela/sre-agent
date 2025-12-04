// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Agent.Framework;

/// <summary>
/// Service responsible for loading experiment definitions from YAML files.
/// </summary>
public sealed class ExperimentLoader : IExperimentLoader
{
    private readonly ILogger<ExperimentLoader> _logger;
    private readonly IVariantAssigner _variantAssigner;
    private readonly string _instanceId;
    private readonly List<Experiment> _experiments = [];

    // Forced experiment variants from environment variable
    private readonly Dictionary<string, string> _forcedVariants = [];

    // Set of experiment IDs to force disable
    private readonly HashSet<string> _disabledExperiments = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ExperimentLoader"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="variantAssigner">Variant assigner for determining experiment assignments.</param>
    /// <param name="instanceId">The instance ID for variant assignment.</param>
    /// <param name="experimentsYamlDirectory">Optional directory containing experiment YAML files.</param>
    /// <param name="forceDisabledExperiments">Optional semicolon-separated list of experiment IDs to force disable, or "*" to disable all.</param>
    /// <param name="forcedVariants">Optional semicolon-separated list of experiment=variant pairs to force specific variants.</param>
    public ExperimentLoader(
        ILogger<ExperimentLoader> logger,
        IVariantAssigner variantAssigner,
        string instanceId,
        string? experimentsYamlDirectory = null,
        string? forceDisabledExperiments = null,
        string? forcedVariants = null)
    {
        _logger = logger;
        _variantAssigner = variantAssigner;
        _instanceId = instanceId;

        if (!string.IsNullOrEmpty(experimentsYamlDirectory))
        {
            LoadExperimentsFromDirectory(experimentsYamlDirectory);
        }

        // Parse force-disabled experiments
        // Format: "experiment1;experiment2;experiment3" or "*" to disable all
        if (!string.IsNullOrEmpty(forceDisabledExperiments))
        {
            var disabledExperiments = forceDisabledExperiments.Contains('*')
                ? _experiments.Select(e => e.ExperimentId)
                : forceDisabledExperiments.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var exp in disabledExperiments)
            {
                var experimentId = exp.Trim();
                if (!string.IsNullOrEmpty(experimentId))
                {
                    _disabledExperiments.Add(experimentId);
                    _logger.LogInternalInformation(
                        "Force-disabled experiment: {ExperimentId}",
                        experimentId);
                }
            }
        }

        // Parse forced experiment variants
        // Format: "experiment1=variant1;experiment2=variant2"
        ParseAndAddForcedVariants(forcedVariants);
    }

    /// <inheritdoc/>
    public IReadOnlyList<Experiment> Experiments => _experiments.AsReadOnly();

    /// <inheritdoc/>
    public void LoadExperimentsFromDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            _logger.LogInternalWarning("Experiment directory path is null or empty.");
            return;
        }

        if (!Directory.Exists(directory))
        {
            _logger.LogInternalWarning("Experiment directory does not exist: {directory}", directory);
            return;
        }

        var yamlFiles = Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(directory, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            try
            {
                string yamlContent = File.ReadAllText(yamlFile);
                var experiment = Experiment.FromYaml(yamlContent);

                experiment.Validate();

                _experiments.Add(experiment);

                _logger.LogInternalInformation(
                    "Successfully loaded experiment '{experimentId}' from YAML file '{yamlFile}'.",
                    experiment.ExperimentId,
                    yamlFile);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogInternalError(ex, "Validation failed for experiment in file {yamlFile}, skipping", yamlFile);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to load experiment from file {filePath}.", yamlFile);
            }
        }
    }

    /// <inheritdoc/>
    public Experiment LoadExperimentFromYaml(string yamlContent)
    {
        if (string.IsNullOrEmpty(yamlContent))
        {
            throw new ArgumentException("YAML content cannot be null or empty.", nameof(yamlContent));
        }

        return Experiment.FromYaml(yamlContent);
    }

    /// <inheritdoc/>
    public bool IsExperimentDisabled(string experimentId)
    {
        return _disabledExperiments.Contains(experimentId);
    }

    /// <inheritdoc/>
    public string? GetForcedVariant(string experimentId)
    {
        if (IsExperimentDisabled(experimentId))
        {
            return null;
        }

        return _forcedVariants.TryGetValue(experimentId, out var variantName) ? variantName : null;
    }

    /// <inheritdoc/>
    public void ParseAndAddForcedVariants(string? forcedVariants)
    {
        if (string.IsNullOrEmpty(forcedVariants))
        {
            return;
        }

        var experiments = forcedVariants.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var exp in experiments)
        {
            var parts = exp.Split('=', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                var experimentId = parts[0].Trim();
                var variantName = parts[1].Trim();
                _forcedVariants[experimentId] = variantName;
                _logger.LogInternalInformation(
                    "Forced experiment variant: {ExperimentId} = {VariantName}",
                    experimentId,
                    variantName);
            }
        }
    }

    /// <inheritdoc/>
    public void ParseAndAddDisabledExperiments(string? disabledExperiments)
    {
        if (string.IsNullOrEmpty(disabledExperiments))
        {
            return;
        }

        var experiments = disabledExperiments.Contains('*')
            ? _experiments.Select(e => e.ExperimentId)
            : disabledExperiments.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var exp in experiments)
        {
            var experimentId = exp.Trim();
            if (!string.IsNullOrEmpty(experimentId))
            {
                _disabledExperiments.Add(experimentId);
                _logger.LogInternalInformation(
                    "Force-disabled experiment: {ExperimentId}",
                    experimentId);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsFeatureFlagEnabled(string featureFlagName)
    {
        foreach (var experiment in _experiments)
        {
            if (experiment.Enabled
                && experiment.Unit == ExperimentUnit.Global
                && experiment.Variants.Any(v => v.Overlay.SystemOverlay != null))
            {
                var forcedVariant = GetForcedVariant(experiment.ExperimentId);

                AssignedVariant assignedVariant;

                if (forcedVariant != null)
                {
                    assignedVariant = new AssignedVariant(experiment.ExperimentId, forcedVariant, InExperiment: true);
                }
                else
                {
                    assignedVariant = _variantAssigner.Assign(experiment, _instanceId);
                }

                if (assignedVariant.InExperiment)
                {
                    var variant = experiment.GetVariantByName(assignedVariant.VariantName);

                    if (variant?.Overlay.SystemOverlay != null
                        && variant.Overlay.SystemOverlay.FeatureFlags != null
                        && variant.Overlay.SystemOverlay.FeatureFlags.Contains(featureFlagName))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
