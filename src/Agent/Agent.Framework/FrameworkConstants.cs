// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Framework-wide constants.
/// </summary>
public static class FrameworkConstants
{
    /// <summary>
    /// Environment variable name for forcing experiment variants, meant for testing.
    /// Format: "experiment1=variant1;experiment2=variant2"
    /// </summary>
    public const string ForceExperimentVariantsEnvVar = "FORCE_EXPERIMENT_VARIANTS";

    /// <summary>
    /// Environment variable for force-disabling experiments, meant as a kill switch.
    /// Format: "experiment1;experiment2"
    /// </summary>
    public const string ForceDisableExperimentsEnvVar = "FORCE_DISABLE_EXPERIMENTS";
}
