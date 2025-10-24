// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Immutable;

namespace Agent.Framework;

/// <summary>
/// Represents a unique combination of experiment variant assignments for caching purposes.
/// Two keys are equal if they have the same set of experiment assignments.
/// </summary>
public sealed class VariantCombinationKey : IEquatable<VariantCombinationKey>
{
    private readonly ImmutableSortedDictionary<string, string> _assignments;
    private readonly int _hashCode;

    public VariantCombinationKey(IEnumerable<AssignedVariant> assignments)
    {
        // Only include experiments where InExperiment = true
        _assignments = assignments
            .Where(a => a.InExperiment)
            .ToImmutableSortedDictionary(a => a.ExperimentId, a => a.VariantName);

        // Pre-compute hash code for better performance
        _hashCode = ComputeHashCode();
    }

    private int ComputeHashCode()
    {
        var hash = new HashCode();
        foreach (var kvp in _assignments)
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }

    public bool Equals(VariantCombinationKey? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Quick check using hash code
        if (_hashCode != other._hashCode)
        {
            return false;
        }

        // Deep equality check
        if (_assignments.Count != other._assignments.Count)
        {
            return false;
        }

        foreach (var kvp in _assignments)
        {
            if (!other._assignments.TryGetValue(kvp.Key, out var otherValue) || kvp.Value != otherValue)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as VariantCombinationKey);

    public override int GetHashCode() => _hashCode;

    public override string ToString() =>
        _assignments.Any()
            ? string.Join("|", _assignments.Select(kvp => $"{kvp.Key}:{kvp.Value}"))
            : "no-experiments";
}
