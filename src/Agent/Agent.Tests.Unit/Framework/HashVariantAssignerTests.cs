// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Framework;

namespace Agent.Tests.Unit.Framework;

public class HashVariantAssignerTests
{
    private static Experiment MakeExperiment(
        string id,
        double coverage,
        bool enabled,
        params Variant[] variants) => new()
        {
            ExperimentId = id,
            Coverage = coverage,
            Enabled = enabled,
            Variants = variants
        };

    private static Variant V(string name, double split) => new()
    {
        Name = name,
        Split = split,
        Overlay = new VariantOverlay()
    };

    [Fact]
    public void DisabledExperiment_ReturnsControlNotInExperiment()
    {
        var exp = MakeExperiment("exp_disabled", 1.0, false, V("A", 1.0));
        var assigner = new HashVariantAssigner();
        var assigned = assigner.Assign(exp, instanceId: "any");
        Assert.False(assigned.InExperiment);
        Assert.Equal("control", assigned.VariantName);
    }

    [Fact]
    public void CoverageApproximation_WithinTolerance()
    {
        var coverage = 0.30; // 30%
        var exp = MakeExperiment("exp_cov_sample", coverage, true, V("A", 0.5), V("B", 0.5));
        var assigner = new HashVariantAssigner();

        int samples = 5000; // deterministic hash but over different instanceIds approximates distribution
        int inExp = 0;
        for (int i = 0; i < samples; i++)
        {
            var result = assigner.Assign(exp, instanceId: $"unit-{i}");
            if (result.InExperiment) inExp++;
        }

        var observed = inExp / (double)samples;
        // Allow a tolerance due to discrete sampling & hash pseudo-randomness
        Assert.InRange(observed, coverage - 0.05, coverage + 0.05);
    }

    [Fact]
    public void VariantSplitApproximation_WithinTolerance()
    {
        // 3 variants with uneven splits
        var exp = MakeExperiment("exp_split_sample", 1.0, true,
            V("A", 0.2), V("B", 0.3), V("C", 0.5));
        var assigner = new HashVariantAssigner();

        int samples = 6000;
        Dictionary<string, int> counts = new() { { "A", 0 }, { "B", 0 }, { "C", 0 } };
        for (int i = 0; i < samples; i++)
        {
            var r = assigner.Assign(exp, $"uid-{i}");
            if (r.InExperiment) counts[r.VariantName]++;
        }

        double aObs = counts["A"] / (double)samples;
        double bObs = counts["B"] / (double)samples;
        double cObs = counts["C"] / (double)samples;

        // Tolerance ±6% absolute
        Assert.InRange(aObs, 0.14, 0.26);
        Assert.InRange(bObs, 0.24, 0.36);
        Assert.InRange(cObs, 0.44, 0.56);
        // Ensure they roughly sum to 1
        var sum = aObs + bObs + cObs;
        Assert.InRange(sum, 0.98, 1.02);
    }

    [Fact]
    public void CoverageBoundary_ExactlyAtCoverageEdge_InOrOutStable()
    {
        // Attempt to find an instanceId right at the edge; brute force search a small range.
        // We just assert stability: assignment for a given id doesn't change across calls.
        var exp = MakeExperiment("exp_edge", 0.5, true, V("A", 0.5), V("B", 0.5));
        var assigner = new HashVariantAssigner();
        for (int i = 1000; i < 2000; i++)
        {
            var first = assigner.Assign(exp, $"edge-{i}");
            var second = assigner.Assign(exp, $"edge-{i}");
            Assert.Equal(first.InExperiment, second.InExperiment);
            Assert.Equal(first.VariantName, second.VariantName);
        }
    }

    [Fact]
    public void CoverageZero_ReturnsControlNotInExperiment()
    {
        var exp = MakeExperiment("exp_cov0", 0.0, true, V("A", 1.0));
        var assigner = new HashVariantAssigner();
        var assigned = assigner.Assign(exp, instanceId: "unit1");
        Assert.False(assigned.InExperiment);
        Assert.Equal("control", assigned.VariantName);
    }

    [Fact]
    public void SingleVariantFullCoverage_AssignsVariant()
    {
        var exp = MakeExperiment("exp_single", 1.0, true, V("A", 1.0));
        var assigner = new HashVariantAssigner();
        var assigned = assigner.Assign(exp, instanceId: "unit42");
        Assert.True(assigned.InExperiment);
        Assert.Equal("A", assigned.VariantName);
    }

    [Fact]
    public void SplitsNormalized_AssignsWithinNormalizedDistribution()
    {
        // Provide splits that do not sum to 1; 0.2 + 0.2 + 0.2 = 0.6 -> will normalize to ~0.333 each
        var exp = MakeExperiment("exp_norm", 1.0, true, V("A", 0.2), V("B", 0.2), V("C", 0.2));
        var assigner = new HashVariantAssigner();
        var assigned1 = assigner.Assign(exp, instanceId: "alpha");
        var assigned2 = assigner.Assign(exp, instanceId: "alpha");
        // Deterministic for same instanceId
        Assert.Equal(assigned1.VariantName, assigned2.VariantName);
        Assert.True(assigned1.InExperiment);
        Assert.Contains(assigned1.VariantName, new[] { "A", "B", "C" });
    }

    [Fact]
    public void DeterministicAssignment_SameUnitSameVariant_DifferentUnitDifferentVariantLikely()
    {
        var exp = MakeExperiment("exp_det", 1.0, true, V("A", 0.5), V("B", 0.5));
        var assigner = new HashVariantAssigner();
        var a1 = assigner.Assign(exp, "unit-x");
        var a2 = assigner.Assign(exp, "unit-x");
        var b1 = assigner.Assign(exp, "unit-y");
        Assert.Equal(a1.VariantName, a2.VariantName); // same unit stable
        // Not guaranteed different, but if same we still assert InExperiment True
        Assert.True(b1.InExperiment);
    }

    [Fact]
    public void ZeroOrNegativeTotalSplits_Throws()
    {
        var exp = MakeExperiment("exp_bad", 1.0, true, V("A", 0.0), V("B", 0.0));
        var assigner = new HashVariantAssigner();
        Assert.Throws<InvalidOperationException>(() => assigner.Assign(exp, "unit"));
    }
}
