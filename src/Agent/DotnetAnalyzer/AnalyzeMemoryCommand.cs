using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Runtime;

namespace DotnetAnalyzer;
public static class AnalyzeMemoryCommand
{
    private class GCRootInfo
    {
        public double Size { get; set; }
        public int Count { get; set; }
    }

    internal static string AnalyzeMemory(string artifactPath)
    {
        StringBuilder outputStringBuilder = new();

        using (DataTarget dataTarget = DataTarget.LoadDump(artifactPath))
        {
            Dictionary<ulong, (int Count, ulong Size, string Name)> stats = new Dictionary<ulong, (int Count, ulong Size, string Name)>();
            Dictionary<string, List<ClrObject>> objectInfo = new();
            ClrInfo dt = dataTarget.ClrVersions.FirstOrDefault();
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
            int sampleSize = Math.Min(100, objectInfo[worstHitter.Name].Count);
            var sampledObjects = objectInfo[worstHitter.Name]
                .OrderBy(_ => random.Next())
                .Take(sampleSize)
                .ToList();

            outputStringBuilder.AppendLine($"Type that occupies the most space on the heap: {worstHitter.Name} with {worstHitter.Size} bytes and {worstHitter.Count} objects.");
            outputStringBuilder.AppendLine(@"Root references analysis for this type are as follows: ");
//this analysis is important has it highlights why and how the objects are rooted
//that is an important consideration when it comes to detecting memory leaks:\");

            Dictionary<string, GCRootInfo> talliedGCRoots = new();
            foreach (var s in sampledObjects)
            {
                // target is the object address.
                GCRoot gcroot = new GCRoot(heap, (d) => d.Address == s.Address);

                // For gcroot for the object with address - here are all the enumerated paths.
                foreach (var rootPath in gcroot.EnumerateRootPaths())
                {
                    StringBuilder sbRoot = new();
                    sbRoot.Append($"{rootPath.Root.Object.Type} -> ");
                    sbRoot.Append(PrintPath(rootPath.Root, rootPath.Path, heap));
                    string sbRootString = sbRoot.ToString(); //string.Join(" ", StandardizeCallStacks(sbRoot.ToString()));

                    // 0 unique roots -> Ignore.
                    if (string.IsNullOrEmpty(sbRootString))
                    {
                        continue;
                    }

                    if (!talliedGCRoots.TryGetValue(sbRootString, out var val))
                    {
                        talliedGCRoots[sbRootString] = val = new GCRootInfo { Size = 0, Count = 0 };
                    }

                    talliedGCRoots[sbRootString].Size += s.Size;
                    talliedGCRoots[sbRootString].Count += 1;
                }

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

            outputStringBuilder.AppendLine("Top 5 Root references analysis tabulated are as follows: ");
            foreach (var kvp in talliedGCRoots.OrderByDescending(kvp => kvp.Value.Count).ThenByDescending(kvp => kvp.Value.Size).Take(5))
            {
                // Print the root reference and its count
                outputStringBuilder.AppendLine($"Count: {kvp.Value.Count} | Size: {kvp.Value.Size}");
                outputStringBuilder.AppendLine($"GCRoot: {kvp.Key}");
                outputStringBuilder.AppendLine("------------------");
            }

            return outputStringBuilder.ToString();
        }
    }

    internal static string PrintPath(ClrRoot root, GCRoot.ChainLink link, ClrHeap heap)
    {
        StringBuilder sb = new();
        sb.Append(PrintRoot(root, root));
        sb.Append(PrintPath(heap, link));
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

            if (link?.Next != null)
            {
                sb.Append($" {obj.Type} -> ");
            }

            else
            {
                sb.Append($" {obj.Type}");
            }

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
                //sb.AppendLine($"Thread {currThread.OSThreadId:x}:");
            }

            ClrStackFrame currFrame = stackRoot.StackFrame;
            if (currFrame is not null && lastStackRoot?.StackFrame != currFrame)
            {
                //sb.AppendLine(GetFrameOutput(currFrame));
            }

            //sb.AppendLine(GetRegisterOutput(stackRoot));
        }
        else if (root.RootKind == ClrRootKind.FinalizerQueue)
        {
            if (lastRoot is null || lastRoot.RootKind != ClrRootKind.FinalizerQueue)
            {
                sb.AppendLine("Finalizer Queue:");
            }

            sb.AppendLine($"    (finalizer root)");
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

            //sb.AppendLine($"    {root.Address:x16}");
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

        //sb.Append(currFrame.StackPointer.ToString("x"));

        // InstructionPointer is 0 for coreclr!Frame objects.
        if (currFrame.InstructionPointer != 0)
        {
            sb.Append(' ');
            //sb.Append(currFrame.InstructionPointer.ToString("x"));
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
                //sb.Append(currFrame.Method.Signature);
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
            //sb.Append(stackRoot.?? "???");
            if (stackRoot.RegisterOffset > 0)
            {
                //sb.Append('+');
                //sb.Append(stackRoot.RegisterOffset.ToString("x"));
            }
            else if (stackRoot.RegisterOffset < 0)
            {
                //sb.Append('-');
                //sb.Append(Math.Abs(stackRoot.RegisterOffset).ToString("x"));
            }

            //sb.Append(':');
        }

        if (stackRoot.Address != 0)
        {
            //sb.Append(' ');
            //sb.Append(stackRoot.Address.ToString("x16"));
        }

        return sb.ToString();
    }
}
