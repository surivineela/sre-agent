using System;
using System.Threading;
using System.Threading.Tasks;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Unified boot diagnostics interface (VM screenshot + serial log + orchestration).
    /// Extended to support VM connectivity diagnostic orchestration.
    /// </summary>
    public interface ICannotConnectToVmPlugin
    {
        Guid? ThreadId { get; set; }

        /// <summary>
        /// Analyze the VM boot screenshot (always safe first step).
        /// </summary>
        Task<string> AnalyzeVmScreenshotAsync(string resourceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyze the VM serial (boot) log (Linux only).
        /// </summary>
        Task<string> AnalyzeVmSerialLogAsync(string resourceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// High-level orchestrator for "cannot connect to VM" scenarios.
        /// tsgFileName is optional. If an error is matched in internal mapping, guidance from the tsg is returned immediately.
        /// Otherwise the method uses osType to evaluate boot diagnostics, invokes screenshot/serial analysis,
        /// and if still no cause is found returns a handoff indicator to meta agent.
        /// </summary>
        Task<string> DiagnoseVmConnectivityIssuesAsync(string resourceId, string osType, string? tsgFileName, CancellationToken cancellationToken = default);
    }
}
