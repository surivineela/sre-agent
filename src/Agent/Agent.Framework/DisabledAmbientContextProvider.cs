// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// A disabled ambient context provider that returns empty context.
/// Used for scenarios where ambient context injection is not needed.
/// </summary>
public sealed class DisabledAmbientContextProvider : IAmbientContextProvider
{
    /// <summary>
    /// Singleton instance of the disabled provider.
    /// </summary>
    public static readonly DisabledAmbientContextProvider Instance = new();

    private DisabledAmbientContextProvider() { }

    /// <inheritdoc />
    public bool Enabled => false;

    /// <inheritdoc />
    public Task<string> GetInstructionsContextAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);

    /// <inheritdoc />
    public Task<string> GetEnvironmentContextAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);

    /// <inheritdoc />
    public Task<string> GetPreUserQueryContextAsync(CancellationToken ct = default) => Task.FromResult(string.Empty);
}
