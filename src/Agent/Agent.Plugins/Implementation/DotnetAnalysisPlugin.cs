// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using System.Text;
using Agent.Plugins.Definitions;
using Microsoft.Diagnostics.Runtime;
using Etlx = Microsoft.Diagnostics.Tracing.Etlx;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Agent.Plugins.Implementation;

public sealed class DotnetAnalysisPlugin : IDotnetAnalysisPlugin
{
    public async Task<string> GetCPUAnalysis(string profilePath)
    {
        // Precondition: path is a nettrace file.
        var etlxFile = Etlx.TraceLog.CreateFromEventPipeDataFile(profilePath);
        var traceLog = Etlx.TraceLog.OpenOrConvert(etlxFile);

        StringBuilder output = new();
        output.AppendLine(await GetGCCPUAnalysis(profilePath));

        int topN = 10;
        var topNExclusive = await GetTopNMethods(profilePath, 10, false, default);
        var topNInclusive = await GetTopNMethods(profilePath, 10, true, default);
        string highestCpu = $"Results from '{profilePath}'\n" +
               $"Highest CPU methods (exclusive):\n{topNExclusive}\n" +
               $"Highest CPU methods (inclusive):\n{topNInclusive}\n";
        output.AppendLine(highestCpu);

        return output.ToString(); 
    }

    // Assumption: dotnet-trace is installed and available in the PATH.
    internal static async Task<string> GetTopNMethods(string nettraceFile, int topN = 10, bool inclusive = false, CancellationToken cancellationToken = default)
    {
        try
        {
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

    public async Task<string> GetMemoryAnalysis(string dumpPath)
    {
        // Precondition Checks.
        if (string.IsNullOrEmpty(dumpPath))
        {
            throw new ArgumentException("The dumpPath is empty");
        }

        StringBuilder outputStringBuilder = new();

        using (DataTarget dataTarget = DataTarget.LoadDump(dumpPath))
        {
            Dictionary<ulong, (int Count, ulong Size, string Name)> stats = new Dictionary<ulong, (int Count, ulong Size, string Name)>();
            Dictionary<string, List<ClrObject>> objectInfo = new();

            ClrRuntime runtime = dataTarget.ClrVersions.FirstOrDefault()?.CreateRuntime();

            if (runtime == null)
            {
                return "No Valid runtimes found!";
            }

            ClrHeap heap = runtime.Heap;

            // Traverse the heap once and get statistics on each type.
            foreach (ClrObject obj in heap.EnumerateObjects())
            {
                if (obj.Type == null || obj.Type?.MethodTable == null)
                {
                    continue;
                }

                if (!stats.TryGetValue(obj.Type.MethodTable, out (int Count, ulong Size, string Name) item))
                    item = (0, 0, obj.Type.Name);

                stats[obj.Type.MethodTable] = (item.Count + 1, item.Size + obj.Size, item.Name);

                if (!objectInfo.TryGetValue(item.Name, out var val))
                {
                    objectInfo[item.Name] = val = new List<ClrObject>();
                }

                val.Add(obj);
            }

            // dumpheap -stat 
            var sorted = from i in stats
                         orderby i.Value.Size descending
                         select new
                         {
                             i.Key,
                             i.Value.Name,
                             i.Value.Size,
                             i.Value.Count
                         };

            // dumpheap -mt <Address>
            var worstHitter = sorted.First();
            var mtAddress = worstHitter.Key;

            // Add a random sampling algorithm that samples the first few items from objectInfo[worstHitter.Name].
            Random random = new Random();
            int sampleSize = Math.Min(40, objectInfo[worstHitter.Name].Count);
            var sampledObjects = objectInfo[worstHitter.Name]
                .OrderBy(_ => random.Next())
                .Take(sampleSize)
                .ToList();

            outputStringBuilder.AppendLine($"Type that occupies the most space on the heap: {worstHitter.Name}");
            outputStringBuilder.AppendLine(@"Root references analysis for this type are as follows -
this analysis is important has it highlights why and how the objects are rooted
that is an important consideration when it comes to detecting memory leaks:\");

            Dictionary<string, int> talliedGCRoots = new();

            foreach (var s in sampledObjects)
            {
                // target is the object address.
                GCRoot gcroot = new GCRoot(heap, (d) => d.Address == s.Address);
                StringBuilder sbRoot = new();
                foreach (var rootPath in gcroot.EnumerateRootPaths())
                {
                    sbRoot.AppendLine($"{rootPath.Root} -> ");
                    sbRoot.AppendLine(PrintPath(rootPath.Root, rootPath.Path, heap));
                }

                string sbRootString = string.Join("\n", StandardizeCallStacks(sbRoot.ToString()));
                if (!talliedGCRoots.ContainsKey(sbRootString))
                {
                    talliedGCRoots[sbRootString] = 0;
                }
                talliedGCRoots[sbRootString]++;

                static List<string> StandardizeCallStacks(string input)
                {
                    // Split the input into individual call stacks
                    var callStacks = input.Split(new[] { "Microsoft.Diagnostics.Runtime.ClrStackRoot" }, StringSplitOptions.RemoveEmptyEntries);

                    // Regex to strip out memory addresses
                    var addressRegex = new Regex(@"0x[0-9a-fA-F]+|7[a-fA-F0-9]{12}");

                    var standardizedStacks = new List<string>();

                    foreach (var stack in callStacks)
                    {
                        // Remove memory addresses
                        var standardizedStack = addressRegex.Replace(stack, "");

                        // Normalize whitespace and trim
                        standardizedStack = string.Join(Environment.NewLine, standardizedStack.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(line => line.Trim()));

                        if (!string.IsNullOrWhiteSpace(standardizedStack))
                        {
                            standardizedStacks.Add(standardizedStack);
                        }
                    }

                    return standardizedStacks;
                }
            }

            outputStringBuilder.AppendLine("Root references analysis tabulated are as follows: ");
            outputStringBuilder.AppendLine("Root Reference\tCount");
            outputStringBuilder.AppendLine("--------------------------------------------------");
            foreach (var kvp in talliedGCRoots)
            {
                outputStringBuilder.AppendLine($"{kvp.Key}\t{kvp.Value}");
            }

            outputStringBuilder.AppendLine("For this analysis: ensure that the root references for user code are highlighted and underscored more than the ASP.NET or system ones.");

            return outputStringBuilder.ToString();
        }
    }

    internal static string PrintPath(ClrRoot root, GCRoot.ChainLink link, ClrHeap heap)
    {
        StringBuilder sb = new();
        sb.AppendLine(PrintRoot(root, root));
        sb.AppendLine(PrintPath(heap, link));
        return sb.ToString();
    }

    internal static string PrintPath(ClrHeap heap, GCRoot.ChainLink link)
    {
        StringBuilder sb = new();
        bool first = true;
        ClrObject firstObj = default;

        ulong prevObj = 0;
        while (link != null)
        {
            ClrObject obj = heap.GetObject(link.Object);
            if (first)
            {
                firstObj = obj;
                first = false;
            }

            sb.AppendLine($"-> {obj} {obj.Type}");
            prevObj = link.Object;
            link = link?.Next;
        }

        return sb.ToString();
    }

    internal static string PrintRoot(ClrRoot root, ClrRoot lastRoot)
    {
        StringBuilder sb = new();
        if (root is ClrStackRoot stackRoot)
        {
            ClrStackRoot lastStackRoot = lastRoot as ClrStackRoot;

            ClrThread currThread = stackRoot.StackFrame?.Thread;
            if (currThread is not null && lastStackRoot?.StackFrame?.Thread != currThread)
            {
                sb.AppendLine($"Thread {currThread.OSThreadId:x}:");
            }

            ClrStackFrame currFrame = stackRoot.StackFrame;
            if (currFrame is not null && lastStackRoot?.StackFrame != currFrame)
            {
                sb.AppendLine(GetFrameOutput(currFrame));
            }

            sb.AppendLine(GetRegisterOutput(stackRoot));
        }
        else if (root.RootKind == ClrRootKind.FinalizerQueue)
        {
            if (lastRoot is null || lastRoot.RootKind != ClrRootKind.FinalizerQueue)
            {
                sb.AppendLine("Finalizer Queue:");
            }

            sb.AppendLine($"    {root.Address:x16} (finalizer root)");
        }
        else if (root is ClrHandle handle)
        {
            if (lastRoot is null or not ClrHandle)
            {
                sb.AppendLine("HandleTable:");
            }
        }
        else
        {
            // There are no other options, but futureproofing in case we add something new
            if (lastRoot is null || lastRoot.RootKind != root.RootKind)
            {
                sb.AppendLine($"{root.RootKind}:");
            }

            sb.AppendLine($"    {root.Address:x16}");
        }

        lastRoot = root;
        return sb.ToString();
    }

    internal static string NameForHandle(ClrHandleKind handleKind)
    {
        return handleKind switch
        {
            ClrHandleKind.WeakShort => "weak short handle",
            ClrHandleKind.WeakLong => "weak long handle",
            ClrHandleKind.Strong => "strong handle",
            ClrHandleKind.Pinned => "pinned handle",
            ClrHandleKind.RefCounted => "ref counted handle",
            ClrHandleKind.Dependent => "dependent handle",
            ClrHandleKind.AsyncPinned => "async pinned handle",
            ClrHandleKind.SizedRef => "sized ref handle",
            ClrHandleKind.WeakWinRT => "weak WinRT handle",
            _ => handleKind.ToString()
        };
    }

    private static string GetFrameOutput(ClrStackFrame currFrame)
    {
        StringBuilder sb = new();
        sb.Clear();
        sb.Append("    ");

        sb.Append(currFrame.StackPointer.ToString("x"));

        // InstructionPointer is 0 for coreclr!Frame objects.
        if (currFrame.InstructionPointer != 0)
        {
            sb.Append(' ');
            sb.Append(currFrame.InstructionPointer.ToString("x"));
        }

        if (currFrame.FrameName is not null)
        {
            sb.Append(' ');
            sb.Append('[');
            sb.Append(currFrame.FrameName);
            sb.Append("] ");
        }

        if (currFrame.Method is not null)
        {
            sb.Append(' ');

            if (currFrame.FrameName is not null)
            {
                sb.Append('(');
            }

            if (currFrame.Method.Signature is not null)
            {
                sb.Append(currFrame.Method.Signature);
            }
            else
            {
                if (currFrame.Method.Type?.Name is not null)
                {
                    sb.Append(currFrame.Method.Type.Name);
                    sb.Append('.');
                }
                else
                {
                    sb.Append("UnknownType.");
                }

                if (currFrame.Method.Name is not null)
                {
                    sb.Append(currFrame.Method.Name);
                    sb.Append("(...)");
                }
                else
                {
                    sb.Append("UnknownMethod(...)");
                }
            }

            if (currFrame.FrameName is not null)
            {
                sb.Append(')');
            }
        }

        return sb.ToString();
    }

    private static string GetRegisterOutput(ClrStackRoot stackRoot)
    {
        StringBuilder sb = new();
        sb.Clear();
        sb.Append("        ");
        if (stackRoot.RegisterName is not null || stackRoot.RegisterOffset != 0)
        {
            sb.Append(stackRoot.RegisterName ?? "???");
            if (stackRoot.RegisterOffset > 0)
            {
                sb.Append('+');
                sb.Append(stackRoot.RegisterOffset.ToString("x"));
            }
            else if (stackRoot.RegisterOffset < 0)
            {
                sb.Append('-');
                sb.Append(Math.Abs(stackRoot.RegisterOffset).ToString("x"));
            }

            sb.Append(':');
        }

        if (stackRoot.Address != 0)
        {
            sb.Append(' ');
            sb.Append(stackRoot.Address.ToString("x16"));
        }

        return sb.ToString();
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

    internal async Task<string> GetGCCPUAnalysis(Etlx.TraceLog traceLog)
    {
        StringBuilder output = new();

        // Check the GC % and see if the high percentage is due to inducing GCs.
        var traceSource = traceLog.Events.GetSource();
        traceSource.NeedLoadedDotNetRuntimes();

        List<GCStartTraceData> inducedGcEvents = new List<GCStartTraceData>();
        traceSource.Clr.GCStart += (gcEvent) =>
        {
            if (gcEvent.Reason is GCReason.Induced or GCReason.InducedNotForced)
            {
                inducedGcEvents.Add(gcEvent);
            }
        };
        traceSource.Process();

        // Nettrace only has 1 process.
        var process = traceSource.Processes().Single();
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

    public async Task<string> GetGCCPUAnalysis(string profilePath)
    {
        Etlx.TraceLog traceLog = GetTraceLogFromProfilePath(profilePath);
        string gcCPUAnalysis = await GetGCCPUAnalysis(traceLog);

        // TODO: Top N Methods.
        // Write code to ensure dotnet-trace is installed on the machine and if not, install it.
        //await EnsureDotnetTraceInstalledAsync();

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

    public Task<string> GetThreadpoolStarvationAnalysis(string profilePath)
    {
        // TODO: Implement
        throw new NotImplementedException();
    }
}
