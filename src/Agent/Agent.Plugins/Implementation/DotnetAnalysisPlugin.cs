// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using System.Text;
using Agent.Plugins.Definitions;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Tracing.Etlx;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.IO.Compression;
using Etlx = Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Symbols;
using Agent.Core.Helpers;

namespace Agent.Plugins.Implementation;

public sealed class DotnetAnalysisPlugin : IDotnetAnalysisPlugin
{
    private ArmHelper _armHelper;

    public DotnetAnalysisPlugin(ArmHelper armHelper)
    {
        _armHelper = armHelper;
    }

    public async Task<string> GetCPUAnalysis(string profilePath, int pid)
    {
        StringBuilder output = new();
        string extension = Path.GetExtension(profilePath);

        Etlx.TraceLog traceLog = null;
        int topN = 10; // Maybe parameterize?

        if (extension.Contains("nettrace"))
        {
            var etlxFile = Etlx.TraceLog.CreateFromEventPipeDataFile(profilePath);
            traceLog = Etlx.TraceLog.OpenOrConvert(etlxFile);

            //output.AppendLine(await GetGCCPUAnalysis(profilePath, pid));
            await EnsureDotnetTraceInstalledAsync();

            // Give the top 10 exclusive and inclusive count.
            var topNExclusive = await GetTopNMethods(profilePath, 10, false, default);
            var topNInclusive = await GetTopNMethods(profilePath, 10, true, default);
            string highestCpu = $"Results from '{profilePath}'\n" +
                   $"Highest CPU methods (exclusive):\n{topNExclusive}\n" +
                   $"Highest CPU methods (inclusive):\n{topNInclusive}\n";
            output.AppendLine(highestCpu);
            return output.ToString();
        }

        else if (extension.Contains("diagsession"))
        {
            string fileName = $"test_{Guid.NewGuid()}";
            string zipDirectory = Path.Combine(Path.GetTempPath(), fileName);
            Directory.CreateDirectory(zipDirectory);
            ZipFile.ExtractToDirectory(profilePath, zipDirectory, true);
            var etlFilePath = Directory.GetFiles(zipDirectory, "*.etl", SearchOption.AllDirectories);

            if (!etlFilePath.Any())
            {
                throw new ArgumentException("No ETL file found in the extracted directory.");
            }

            // Merge the ETL files?
            traceLog = Etlx.TraceLog.OpenOrConvert(etlFilePath.First());
        }

        else if (extension.Contains("etl"))
        {
            traceLog = Etlx.TraceLog.OpenOrConvert(profilePath);
        }

        using var symbolReader = new SymbolReader(TextWriter.Null)
        {
            // Configure symbol path: local cache + Microsoft public symbol server
            SymbolPath = SymbolPath.MicrosoftSymbolServerPath, // Or use SRV*C:\symbols*https://msdl.microsoft.com/download/symbols
        };

        List<string> unwantedMethodNames = new() { "ROOT", "Process" };

        //Create an extension function to help
        static List<CallTreeNodeBase> ByIDSortedInclusiveMetric(CallTree callTree)
        {
            List<CallTreeNodeBase> ret = new(callTree.ByID);
            ret.Sort((x, y) => Math.Abs(y.InclusiveMetric).CompareTo(Math.Abs(x.InclusiveMetric)));
            return ret;
        }

        var codeAddresses = traceLog.CodeAddresses;
        foreach (var module in traceLog.ModuleFiles)
        {
            try
            {
                traceLog.CodeAddresses.LookupSymbolsForModule(symbolReader, module);
            }

            catch
            {
               // Swallow.
            }
        }

        var kernelEvents = traceLog.Events.Filter(e => e is SampledProfileTraceData && e.ProcessID == pid);

        var inclusiveSamples = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var exclusiveSamples = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var stackSource = new MutableTraceEventStackSource(traceLog);

        foreach (SampledProfileTraceData ev in kernelEvents)
        {
            if (ev.CallStackIndex() != CallStackIndex.Invalid)
            {
                var sample = new StackSourceSample(stackSource)
                {
                    TimeRelativeMSec = ev.TimeStampRelativeMSec,
                    Metric = 1, // Increment by 1 for each sample
                    StackIndex = (StackSourceCallStackIndex)ev.CallStackIndex()
                };
                stackSource.AddSample(sample);
            }
        }
        stackSource.DoneAddingSamples();
        stackSource.LookupWarmSymbols(100, symbolReader);

        stackSource.ForEach(sample =>
        {
            string topFrame = string.Empty;
            var stackIndex = sample.StackIndex;

            while (stackIndex != StackSourceCallStackIndex.Invalid)
            {
                var frameIndex = stackSource.GetFrameIndex(stackIndex);
                var method = stackSource.GetFrameName(frameIndex, true);
                var methodLower = method.ToLowerInvariant();

                // Filter out BCL constructs and unmanaged methods
                if (methodLower.Contains("clr!") || methodLower.Contains("coreclr!")
                || methodLower.Contains("kernelbase!") || methodLower.Contains("mscorlib") || methodLower.Contains("ntdll!")
                || methodLower.Contains("kernel32!") || methodLower.Contains("system") || methodLower.Contains("thread ("))
                {
                    stackIndex = stackSource.GetCallerIndex(stackIndex);
                    continue;
                }

                if (!inclusiveSamples.TryAdd(method, 1))
                    inclusiveSamples[method]++;

                if (topFrame == null)
                    topFrame = method;

                stackIndex = stackSource.GetCallerIndex(stackIndex);
            }

            if (topFrame != null)
            {
                if (!exclusiveSamples.TryAdd(topFrame, 1))
                    exclusiveSamples[topFrame]++;
            }
        });

        output.AppendLine($"Top {topN} Inclusive Methods by CPU:");
        foreach (var entry in inclusiveSamples.OrderByDescending(e => e.Value).Take(topN))
        {
            output.AppendLine($"{entry.Key}: {entry.Value} samples");
        }

        output.AppendLine($"\nTop {topN} Exclusive Methods by CPU:");
        foreach (var entry in exclusiveSamples.OrderByDescending(e => e.Value).Take(topN))
        {
            output.AppendLine($"{entry.Key}: {entry.Value} samples");
        }

        return output.ToString(); 
    }

    // Assumption: dotnet-trace is installed and available in the PATH.
    internal static async Task<string> GetTopNMethods(string nettraceFile, int topN = 10, bool inclusive = false, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureDotnetTraceInstalledAsync();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"trace report \"{nettraceFile}\" topN -n {topN}{(inclusive ? " --inclusive" : "")}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    outputBuilder.AppendLine(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    errorBuilder.AppendLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                return $"Error executing dotnet trace report: {errorBuilder.ToString()}";
            }

            return outputBuilder.ToString();
        }

        catch (Exception ex)
        {
            return $"An unexpected error occurred: {ex.Message}";
        }
    }

    public async Task<string> GetMemoryAnalysis(string resourceId, string dumpPath)
    {
        // Precondition Checks.
        if (string.IsNullOrEmpty(dumpPath))
        {
            throw new ArgumentException("The dumpPath is empty");
        }

        if (string.IsNullOrEmpty(resourceId))
        {
            throw new ArgumentException("The resourceId is empty");
        }

        // If the DotnetAnalyzer isn't uploaded - do so.
        KuduManager kuduManager = await KuduManager.Initialize(resourceId, _armHelper);

        // Download the dotnet analyzer via blob storage. 
        if (kuduManager.OS == "Windows")
        {
            if (kuduManager.Is32Bit)
            {
                await kuduManager.ExecuteCommandAsync("curl -X GET https://dotnetanalysis.blob.core.windows.net/win32/DotnetAnalyzer.exe -o DotnetAnalyzer.exe", "C://local//");
            }

            else
            {
                await kuduManager.ExecuteCommandAsync("curl -X GET https://dotnetanalysis.blob.core.windows.net/win64/DotnetAnalyzer.exe -o DotnetAnalyzer.exe", "C://local//");
            }

            // Run the dotnet analyzer on the dump file with the appropriate commands. 
            string result = await kuduManager.ExecuteCommandAsync($"DotnetAnalyzer.exe analyze-memory C://local//{dumpPath}", "C://local//");

            // Delete dump after analysis to save space.
            //try
            //{
            //    string _ = await kuduManager.ExecuteCommandAsync($"del C://local//temp//{dumpPath}", "C://local//temp");
            //}

            //catch (Exception)
            //{
            //    Console.WriteLine($"[DotnetAnalysisPlugin] Failed to delete dump: {dumpPath}");
            //}

            return result;
        }

        else
        {
            throw new NotImplementedException("Not implemented for Linux yet.");
        }
    }

    private class CallStackComparer : IEqualityComparer<TraceCallStack>
    {
        public bool Equals(TraceCallStack? x, TraceCallStack? y)
        {
            if (x == null && y == null)
                return true;
            if (x == null || y == null)
                return false;
            // if each method in the callstack is equal
            if (x.Depth != y.Depth)
                return false;
            while (x is not null && y is not null && x.Depth > 0)
            {
                if (x.CodeAddress.FullMethodName != y.CodeAddress.FullMethodName)
                    return false;
                x = x.Caller;
                y = y.Caller;
            }
            return true;
        }

        public int GetHashCode(TraceCallStack obj)
        {
            if (obj == null)
                return 0;
            int hash = HashCode.Combine(obj.Depth, 17);
            while (obj is not null && obj.Depth > 0)
            {
                hash = HashCode.Combine(hash, obj.CodeAddress.FullMethodName);
                obj = obj.Caller;
            }
            return hash;
        }
    }

    internal Etlx.TraceLog GetTraceLogFromProfilePath(string profilePath)
    {
        var etlxFile = Etlx.TraceLog.CreateFromEventPipeDataFile(profilePath);
        var traceLog = Etlx.TraceLog.OpenOrConvert(etlxFile);
        return traceLog;
    }

    internal async Task<string> GetGCCPUAnalysis(Etlx.TraceLog traceLog, int pid)
    {
        StringBuilder output = new();

        // Check the GC % and see if the high percentage is due to inducing GCs.
        var traceSource = traceLog.Events.GetSource();

        List<GCStartTraceData> inducedGcEvents = new List<GCStartTraceData>();
        traceSource.Clr.GCStart += (gcEvent) =>
        {
            if (gcEvent.Reason is GCReason.Induced or GCReason.InducedNotForced)
            {
                inducedGcEvents.Add(gcEvent);
            }
        };
        traceSource.Process();

        var process = traceSource.Processes().FirstOrDefault(p => p.ProcessID == pid);
        if (process == null)
        {
            throw new ArgumentException($"No process found with PID {pid} in the trace.");
        }

        var mang = process.LoadedDotNetRuntime();
        if (mang.GC.Stats().Count == 0)
        {
            return "No GCs found in the trace.";
        }
        int inducedPercentage = mang.GC.Stats().NumInduced * 100 / mang.GC.Stats().Count;
        if (inducedPercentage > 10)
        {
            var inducedGcs = mang.GC.GCs.Where(gc => gc.IsComplete && (gc.Reason == GCReason.Induced || gc.Reason == GCReason.InducedNotForced));

            Dictionary<TraceCallStack, int> callStackCounts = new(new CallStackComparer());
            foreach (var gc in inducedGcEvents)
            {
                foreach (var callStack in gc.CallStacks())
                {
                    if (callStack == null)
                        continue;
                    if (!callStackCounts.TryGetValue(callStack, out int count))
                    {
                        callStackCounts[callStack] = 1;
                    }
                    else
                    {
                        callStackCounts[callStack] = count + 1;
                    }
                }
            }

            // Get the top 10 call stacks
            var topCallStacks = callStackCounts.OrderByDescending(kvp => kvp.Value).Take(10);

            string outputFromInducedGCs = $"The Induced GC percentage is >10% : {inducedPercentage}%\n and is a contributive factor to the high CPU %. " +
                    $"Top 10 call stacks:\n";
            outputFromInducedGCs +=
                    string.Join("\n", callStackCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .Take(10)
                    .Select(kvp => $"{kvp.Value} times: {kvp.Key}"));
            output.AppendLine(outputFromInducedGCs);
            return output.ToString();
        }

        else
        {
            return $"The Induced GC percentage is < 10% ({inducedPercentage}%) and is not a contributive factor to the high CPU %.";
        }
    }

    public async Task<string> GetGCCPUAnalysis(string profilePath, int pid)
    {
        Etlx.TraceLog traceLog = GetTraceLogFromProfilePath(profilePath);
        string gcCPUAnalysis = await GetGCCPUAnalysis(traceLog, pid);
        return gcCPUAnalysis;
    }

    private static async Task EnsureDotnetTraceInstalledAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--list-tools",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    outputBuilder.AppendLine(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    errorBuilder.AppendLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (!outputBuilder.ToString().Contains("dotnet-trace"))
            {
                // Install dotnet-trace if not found
                var installProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "tool install --global dotnet-trace",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                installProcess.Start();
                await installProcess.WaitForExitAsync();

                if (installProcess.ExitCode != 0)
                {
                    throw new InvalidOperationException("Failed to install dotnet-trace. Ensure you have the necessary permissions and try again.");
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"An error occurred while ensuring dotnet-trace is installed: {ex.Message}");
        }
    }

    public Task<string> GetDeadlockAnalysis(string dumpPath)
    {
        // TODO: Implement
        throw new NotImplementedException(); 
    }

    public Task<string> GetThreadpoolStarvationAnalysis(string profilePath, int pid)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }
}
