// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Framework;

namespace Agent.Core.Configuration
{
    /// <summary>
    /// Defines model priority configuration for different scenario types
    /// </summary>
    public class ModelScenarioPriority
    {
        /// <summary>
        /// Ordered list of model deployment names by priority (most preferred first)
        /// </summary>
        [Required]
        public List<string> PriorityModels { get; set; } = new();

        /// <summary>
        /// Default model to use if no models from priority list are available
        /// </summary>
        [Required]
        public string DefaultModel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Configuration for model selection based on scenario types.
    /// Uses a dictionary for efficient scenario-based model lookups.
    /// </summary>
    public class ModelScenarioConfiguration : Dictionary<ModelScenarioType, ModelScenarioPriority>
    {
        public ModelScenarioConfiguration()
        {
            // Initialize with empty priorities for all scenario types to ensure validation
            this[ModelScenarioType.ReasoningHeavy] = new ModelScenarioPriority();
            this[ModelScenarioType.ReasoningFast] = new ModelScenarioPriority();
            this[ModelScenarioType.GeneralPurpose] = new ModelScenarioPriority();
            this[ModelScenarioType.SmallFast] = new ModelScenarioPriority();
            this[ModelScenarioType.LongContext] = new ModelScenarioPriority();
            this[ModelScenarioType.Eval] = new ModelScenarioPriority();
        }

        /// <summary>
        /// Gets the scenario priority for the specified scenario type.
        /// Returns null if the scenario type is not configured.
        /// </summary>
        public ModelScenarioPriority? GetScenarioPriority(ModelScenarioType scenarioType)
        {
            return TryGetValue(scenarioType, out var priority) ? priority : null;
        }
    }
}
