// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Threading;

namespace Agent.Core.Helpers;

/// <summary>
/// Ambient context to indicate that the current execution was initiated by a scanner (e.g., IcmScanner).
/// This flows through async calls and can be used by orchestrators/plugins to gate side-effects.
/// </summary>
public static class IncidentProcessingContext
{
    private static readonly AsyncLocal<bool> _isScannerOrigin = new();

    /// <summary>
    /// Gets whether the current async flow originated from a scanner.
    /// </summary>
    public static bool IsScannerOrigin => _isScannerOrigin.Value;

    /// <summary>
    /// Begins a scope where IsScannerOrigin is set to true. Restores the previous value on dispose.
    /// </summary>
    public static IDisposable BeginScannerOriginScope()
    {
        var previous = _isScannerOrigin.Value;
        _isScannerOrigin.Value = true;
        return new Scope(() => _isScannerOrigin.Value = previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;
        public Scope(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _onDispose();
        }
    }
}
