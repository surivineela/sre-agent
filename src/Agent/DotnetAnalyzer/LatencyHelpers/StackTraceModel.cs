using System.Text;
using System.Text.RegularExpressions;

namespace StackParser
{
    /// <summary>
    /// Represents a complete stack trace with multiple thread stacks
    /// </summary>
    public sealed class StackTrace
    {
        /// <summary>
        /// Collection of all thread stacks in the trace
        /// </summary>
        public List<ThreadStack> Threads { get; } = new List<ThreadStack>();

        /// <summary>
        /// Parses a stack trace from a string
        /// </summary>
        /// <param name="content">Stack trace content as string</param>
        /// <returns>Parsed StackTrace object</returns>
        public static StackTrace ParseFromString(string content)
        {
            var stackTrace = new StackTrace();

            // Split the content by thread sections
            string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            ThreadStack currentThread = null;
            bool isHeaderLine = false;

            foreach (var line in lines)
            {
                // Skip comment lines
                if (line.TrimStart().StartsWith("//"))
                    continue;

                // Check for thread ID line
                var threadIdMatch = Regex.Match(line.Trim(), @"^OS Thread Id: (0x[0-9a-fA-F]+)$");
                if (threadIdMatch.Success)
                {
                    // Create a new thread stack
                    currentThread = new ThreadStack
                    {
                        ThreadId = threadIdMatch.Groups[1].Value
                    };
                    stackTrace.Threads.Add(currentThread);
                    isHeaderLine = true;
                    continue;
                }

                // Skip header line with column names
                if (isHeaderLine && line.Contains("Child SP") && line.Contains("IP") && line.Contains("Call Site"))
                {
                    isHeaderLine = false;
                    continue;
                }

                // Process stack frame lines if we have a current thread
                if (currentThread != null && !string.IsNullOrWhiteSpace(line) && !isHeaderLine)
                {
                    // Parse the stack frame line
                    var frame = ParseStackFrame(line);
                    if (frame != null)
                    {
                        currentThread.StackFrames.Add(frame);
                    }
                }
            }

            return stackTrace;
        }

        /// <summary>
        /// Parses a single stack frame line
        /// </summary>
        /// <param name="line">Line to parse</param>
        /// <returns>Parsed StackFrame object or null if parsing failed</returns>
        private static StackFrame ParseStackFrame(string line)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // Split by whitespace for the memory addresses
            string[] parts = line.Split(new[] { ' ' }, 3);
            if (parts.Length < 3)
                return null;

            var frame = new StackFrame
            {
                StackPointer = parts[0],
                InstructionPointer = parts[1]
            };

            string callSitePart = parts[2].Trim();

            // Handle special frame types (like HelperMethodFrame, InlinedCallFrame, etc.)
            var bracketMatch = Regex.Match(callSitePart, @"^\[(.*?):\s*(.*?)\]\s*(.*)$");
            if (bracketMatch.Success)
            {
                frame.FrameType = bracketMatch.Groups[1].Value;
                frame.FrameAddress = bracketMatch.Groups[2].Value;
                frame.CallSite = bracketMatch.Groups[3].Value.Trim();
            }
            else
            {
                frame.CallSite = callSitePart;
            }

            // Extract source file information if available
            var sourceMatch = Regex.Match(frame.CallSite, @"\[(.*?)@\s*(\d+)\]$");
            if (sourceMatch.Success)
            {
                frame.SourceFile = sourceMatch.Groups[1].Value.Trim();
                if (int.TryParse(sourceMatch.Groups[2].Value, out int lineNumber))
                {
                    frame.LineNumber = lineNumber;
                }

                // Remove source info from call site for method extraction
                frame.CallSite = frame.CallSite.Substring(0, sourceMatch.Index).Trim();
            }

            // Extract method information
            ExtractMethodInfo(frame);

            return frame;
        }

        /// <summary>
        /// Extracts method name, namespace, and parameters from a call site
        /// </summary>
        /// <param name="frame">The stack frame to process</param>
        private static void ExtractMethodInfo(StackFrame frame)
        {
            if (string.IsNullOrEmpty(frame.CallSite))
                return;

            // Try to match the fully qualified method name pattern
            var methodMatch = Regex.Match(frame.CallSite, @"((?:[a-zA-Z0-9_]+\.)+)([a-zA-Z0-9_+<>]+)(\(.*\))");
            if (methodMatch.Success)
            {
                frame.Namespace = methodMatch.Groups[1].Value.TrimEnd('.');
                frame.MethodName = methodMatch.Groups[2].Value;
                frame.Parameters = methodMatch.Groups[3].Value;
            }
            else
            {
                // If no match, try a simpler pattern without parameters
                var simpleMatch = Regex.Match(frame.CallSite, @"((?:[a-zA-Z0-9_]+\.)+)([a-zA-Z0-9_+<>]+)");
                if (simpleMatch.Success)
                {
                    frame.Namespace = simpleMatch.Groups[1].Value.TrimEnd('.');
                    frame.MethodName = simpleMatch.Groups[2].Value;
                }
                else
                {
                    // If all else fails, just set the whole call site as method name
                    frame.MethodName = frame.CallSite;
                }
            }
        }


        /// <summary>
        /// Finds threads that are likely in a deadlocked state, incorporating syncblk information
        /// </summary>
        /// <returns>Collection of potentially deadlocked threads</returns>
        public (List<ThreadStack>, Dictionary<string, string>, Dictionary<string, string>) FindDeadlockedThreadsWithSyncblk(string syncblkContent)
        {   // Parse syncblk content to identify threads holding and waiting for locks
            var syncblkLines = syncblkContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var lockHolders = new Dictionary<string, string>(); // ThreadId -> SyncBlock Owner
            var lockWaiters = new Dictionary<string, string>(); // SyncBlock Owner -> ThreadId

            bool isHeaderPassed = false;

            foreach (var line in syncblkLines)
            {
                // Skip header lines, footer lines, and separator lines
                if (line.Contains("Index") && line.Contains("SyncBlock") && line.Contains("MonitorHeld"))
                {
                    isHeaderPassed = true;
                    continue;
                }

                if (line.StartsWith("----") || line.StartsWith("Total") || line.StartsWith("CCW") ||
                    line.StartsWith("RCW") || line.StartsWith("ComClassFactory") || line.StartsWith("Free"))
                {
                    continue;
                }

                // Only process data lines after the header
                if (!isHeaderPassed)
                    continue;

                // Format: Index SyncBlockAddr MonitorHeld Recursion OwningThreadInfo TID ThreadNum SyncBlockOwnerAddr ObjectType
                var match = Regex.Match(line.Trim(), @"^\s*(\d+)\s+([0-9a-fA-F]+)\s+(\d+)\s+(\d+)\s+([0-9a-fA-F]+)\s+([0-9a-fA-F]+)\s+(\d+)\s+([0-9a-fA-F]+)\s+(.*)$");
                if (match.Success)
                {
                    var syncBlockIndex = match.Groups[1].Value;
                    var syncBlockAddr = match.Groups[2].Value;
                    var owningThreadAddr = match.Groups[5].Value;
                    var owningThreadId = match.Groups[6].Value;
                    var syncblockOwnerAddr = match.Groups[8].Value;
                    var objectType = match.Groups[9].Value.Trim();

                    var threadIdHex = "0x" + owningThreadId;

                    lockHolders[threadIdHex] = syncBlockAddr;
                    lockWaiters[syncBlockAddr] = threadIdHex;
                }
            }

            // Find threads with specific lock-related methods in their stack
            var lockWaitThreads = Threads.Where(t =>
                t.StackFrames.Any(f =>
                    f.CallSite != null &&
                    (f.CallSite.Contains("Monitor.ReliableEnter") ||
                     f.CallSite.Contains("Monitor.ObjWait") ||
                     f.CallSite.Contains("Monitor.Wait"))
                )
            ).ToList();

            // Cross-reference with syncblk data to confirm deadlocks
            var deadlockedThreads = new List<ThreadStack>();

            foreach (var thread in lockWaitThreads)
            {
                if (lockHolders.TryGetValue(thread.ThreadId, out var heldLock) &&
                    lockWaiters.TryGetValue(heldLock, out var waitingThreadId) &&
                    waitingThreadId == thread.ThreadId)
                {
                    deadlockedThreads.Add(thread);
                }
            }

            return (deadlockedThreads.ToList(), lockHolders, lockWaiters);
        }

        /// <summary>
        /// Analyzes the deadlock pattern and provides a detailed report
        /// </summary>
        /// <returns>A detailed report of the deadlock analysis</returns>
        public (bool isDeadLocked, string details) AnalyzeDeadlock(string synblkResult)
        {
            (var deadlockedThreads, var lockholders, var lockwaiters) = FindDeadlockedThreadsWithSyncblk(synblkResult);

            if (deadlockedThreads.Count < 2)
            {
                return (false, "No deadlock detected or insufficient information to determine a deadlock pattern.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"DEADLOCK DETECTED: {deadlockedThreads.Count} threads are in a potential deadlock state.\n");

            // Group 1: Threads waiting to acquire a lock (ReliableEnter)
            var acquiringLockThreads = deadlockedThreads
                .Where(t => t.StackFrames.Any(f => f.CallSite?.Contains("Monitor.ReliableEnter") == true))
                .ToList();

            // Group 2: Threads waiting on a condition (Wait/ObjWait)
            var waitingOnConditionThreads = deadlockedThreads
                .Where(t => t.StackFrames.Any(f =>
                    f.CallSite?.Contains("Monitor.ObjWait") == true ||
                    f.CallSite?.Contains("Monitor.Wait") == true))
                .ToList();

            // Analyze the lock acquisition pattern
            sb.AppendLine("=== DEADLOCK PATTERN ===");

            // First list threads trying to acquire locks
            if (acquiringLockThreads.Any())
            {
                sb.AppendLine("\nThreads waiting to acquire locks:");
                foreach (var thread in acquiringLockThreads)
                {
                    sb.AppendLine($"Thread 0x{thread.ThreadId.Replace("0x", "")} - {thread.IsLikelyDeadlocked()}:");

                    // Find the user code methods near the lock
                    var userMethods = thread.StackFrames;

                    foreach (var method in userMethods)
                    {
                        sb.AppendLine($"  At {method.Namespace}.{method.MethodName} in {Path.GetFileName(method.SourceFile)}:line {method.LineNumber}");
                    }
                }
            }

            // Then list threads waiting on conditions
            if (waitingOnConditionThreads.Any())
            {
                sb.AppendLine("\nThreads waiting on conditions:");
                foreach (var thread in waitingOnConditionThreads)
                {
                    sb.AppendLine($"Thread 0x{thread.ThreadId.Replace("0x", "")}:");

                    // Find the user code methods near the wait
                    var userMethods = thread.StackFrames;

                    foreach (var method in userMethods)
                    {
                        sb.AppendLine($"  At {method.Namespace}.{method.MethodName} in {Path.GetFileName(method.SourceFile)}:line {method.LineNumber}");
                    }
                }
            }

            // Add diagnostic information
            sb.AppendLine("\n=== DIAGNOSTIC INFORMATION ===");

            if (acquiringLockThreads.Count >= 2)
            {
                sb.AppendLine("\nDiagnosis: Classical Deadlock (Circular Wait)");
                sb.AppendLine("Multiple threads are attempting to acquire locks in different orders, causing a circular wait condition.");
                sb.AppendLine("\nRecommended fixes:");
                sb.AppendLine("1. Establish a consistent lock acquisition order across all threads");
                sb.AppendLine("2. Use timeout versions of lock acquisition methods to detect deadlocks at runtime");
                sb.AppendLine("3. Consider using higher-level synchronization primitives like SemaphoreSlim or ReaderWriterLockSlim");
            }
            else if (waitingOnConditionThreads.Count >= 1 && acquiringLockThreads.Count >= 1)
            {
                sb.AppendLine("\nDiagnosis: Signal-Wait Deadlock");
                sb.AppendLine("One or more threads are waiting for a signal that will never arrive because");
                sb.AppendLine("another thread that should provide the signal is blocked trying to acquire a lock.");
                sb.AppendLine("\nRecommended fixes:");
                sb.AppendLine("1. Ensure signals are always sent, even in error paths (use try/finally blocks)");
                sb.AppendLine("2. Avoid acquiring locks while holding other locks");
                sb.AppendLine("3. Consider restructuring to use Task-based asynchronous patterns instead");
            }
            else if (waitingOnConditionThreads.Count >= 2)
            {
                sb.AppendLine("\nDiagnosis: Condition Deadlock");
                sb.AppendLine("Multiple threads are waiting for conditions that depend on each other, creating a circular dependency.");
                sb.AppendLine("\nRecommended fixes:");
                sb.AppendLine("1. Review the dependency structure of your wait conditions");
                sb.AppendLine("2. Consider using timeout versions of wait methods");
                sb.AppendLine("3. Use higher-level synchronization mechanisms like CountdownEvent or Barrier");
            }

            sb.AppendLine("Other Details: ");
            sb.AppendLine($"Full synblk result: {synblkResult}");

            sb.AppendLine($"Lock Holders (ThreadId -> Syncblock): {DictionaryToString(lockholders)}");
            sb.AppendLine($"Lock Waiters (Syncblock -> ThreadId): {DictionaryToString(lockwaiters)}");
            sb.AppendLine($"Stack Summary: {GetStackSummary()}");

            return (true, sb.ToString());
        }

        private static string DictionaryToString<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            return string.Join(", ", dict.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        }

        /// <summary>
        /// Prints a summary of the stack trace
        /// </summary>
        public string GetStackSummary()
        {
            StringBuilder sb = new();
            sb.AppendLine($"Stack Trace Summary: {Threads.Count} threads");
            sb.AppendLine(new string('-', 80));

            foreach (var thread in Threads)
            {
                sb.AppendLine($"Thread ID: {thread.ThreadId} - {thread.StackFrames.Count} frames");

                // Show top 3 frames for each thread
                foreach (var frame in thread.StackFrames.Take(3))
                {
                    string location = string.IsNullOrEmpty(frame.SourceFile) ? "" : $" ({frame.SourceFile}:{frame.LineNumber})";
                    sb.AppendLine($"  {frame.MethodName}{frame.Parameters}{location}");
                }

                if (thread.StackFrames.Count > 3)
                {
                    sb.AppendLine("  ...");
                }
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Represents a single thread's stack trace
    /// </summary>
    public class ThreadStack
    {
        /// <summary>
        /// The thread ID in hexadecimal (e.g., "0xba70")
        /// </summary>
        public string ThreadId { get; set; }

        /// <summary>
        /// Collection of stack frames in this thread's stack
        /// </summary>
        public List<StackFrame> StackFrames { get; } = new List<StackFrame>();

        /// <summary>
        /// Determines if this thread is likely in a deadlocked state
        /// </summary>
        public bool IsLikelyDeadlocked()
        {
            return StackFrames.Any(f =>
                f.CallSite != null &&
                (f.CallSite.Contains("Monitor.ReliableEnter") ||
                 f.CallSite.Contains("Monitor.ObjWait") ||
                 f.CallSite.Contains("Monitor.Wait")));
        }

        /// <summary>
        /// Gets the first stack frame from user code (not system libraries)
        /// </summary>
        /// <returns>The first user code stack frame, or null if none found</returns>
        public StackFrame GetFirstUserFrame()
        {
            return StackFrames.FirstOrDefault(f =>
                f.SourceFile != null &&
                !f.SourceFile.Contains("/src/libraries/") &&
                !f.SourceFile.Contains("/src/coreclr/"));
        }
    }

    /// <summary>
    /// Represents a single stack frame in a thread stack
    /// </summary>
    public class StackFrame
    {
        /// <summary>
        /// Stack pointer memory address
        /// </summary>
        public string StackPointer { get; set; }

        /// <summary>
        /// Instruction pointer memory address
        /// </summary>
        public string InstructionPointer { get; set; }

        /// <summary>
        /// Frame type (e.g., "HelperMethodFrame_1OBJ", "InlinedCallFrame")
        /// </summary>
        public string FrameType { get; set; }

        /// <summary>
        /// Address associated with the frame type
        /// </summary>
        public string FrameAddress { get; set; }

        /// <summary>
        /// Raw call site string
        /// </summary>
        public string CallSite { get; set; }

        /// <summary>
        /// Source file path
        /// </summary>
        public string SourceFile { get; set; }

        /// <summary>
        /// Line number in source file
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Namespace of the method
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Method name
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// Method parameters as a string
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Gets the fully qualified method name
        /// </summary>
        public string FullMethodName => string.IsNullOrEmpty(Namespace) ?
            MethodName : $"{Namespace}.{MethodName}";

        /// <summary>
        /// Gets a string representation of the source location
        /// </summary>
        public string SourceLocation => SourceFile != null ?
            $"{SourceFile}:line {LineNumber}" : "unknown location";
    }
}
