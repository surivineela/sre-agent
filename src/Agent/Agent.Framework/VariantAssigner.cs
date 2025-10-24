// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;

namespace Agent.Framework;

public interface IVariantAssigner
{
    AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null);
}

public sealed record AssignedVariant(
    string ExperimentId,
    string VariantName,
    bool InExperiment
);

/// <summary>
/// Default implementation of <see cref="IVariantAssigner"/>, uses SHA256 to hash experiment ID and unit key (instance or thread ID)
/// to produce a consistent assignment. The hash is then mapped to a value in [0,1) to determine assignment
/// based on experiment coverage and variant splits.
/// </summary>
public sealed class HashVariantAssigner : IVariantAssigner
{
    public AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null)
    {
        if (!experiment.Enabled)
        {
            return new(experiment.ExperimentId, "control", false);
        }

        // Determine the unit key based on experiment configuration
        var unitKey = experiment.Unit switch
        {
            ExperimentUnit.Global => instanceId,
            ExperimentUnit.PerThread => threadId ?? instanceId, // Fall back to instanceId if threadId not provided
            _ => instanceId
        };

        var hashInput = $"{experiment.ExperimentId}:{unitKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        // Convert first 8 bytes to double in [0,1)
        var value = BitConverter.ToUInt64(hash, 0) / (double)ulong.MaxValue;

        if (value > experiment.Coverage)
        {
            return new(experiment.ExperimentId, "control", false);
        }

        var variants = NormalizeSplits(experiment.Variants); // make sure splits sum to 1
        var scaled = value / experiment.Coverage; // renormalize the key value to [0,1) after coverage check

        double cumulative = 0;
        foreach (var v in variants)
        {
            cumulative += v.Split;
            if (scaled <= cumulative)
            {
                return new(experiment.ExperimentId, v.Name, true);
            }
        }

        return new(experiment.ExperimentId, variants.Last().Name, true);
    }

    private static IEnumerable<Variant> NormalizeSplits(IEnumerable<Variant> variants)
    {
        const double epsilon = 1e-9;
        var total = variants.Sum(v => v.Split);
        if (Math.Abs(total - 1.0) > epsilon)
        {
            if (total <= 0)
            {
                throw new InvalidOperationException("Sum of splits must be > 0.");
            }

            return [.. variants.Select(v => v with { Split = v.Split / total })];
        }
        return variants;
    }
}

/// <summary>
/// Variant assigner that disables all experiments, always assigning the control variant.
/// </summary>
public sealed class DisableExperimentsVariantAssigner : IVariantAssigner
{
    public AssignedVariant Assign(Experiment experiment, string instanceId, string? threadId = null)
    {
        return new(experiment.ExperimentId, "control", false);
    }
}
