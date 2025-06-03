using System.Diagnostics;

namespace DotnetAnalyzer.LatencyHelpers
{
    public class DeadlockAnalyzer
    {
        public static (string ClrStackOutput, string SyncBlkOutput) RetrievePattern(string dumpContent)
        {
            List<string> clrStackOutput = new List<string>();
            List<string> syncBlkOutput = new List<string>();

            // Define markers for command outputs
            const string clrStackStartMarker = "> clrstack -all";
            const string syncBlkStartMarker = "> syncblk";
            const string endCommandMarker = "<END_COMMAND_OUTPUT>";

            // Split the content by lines
            string[] lines = dumpContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Parse state
            bool inClrStackOutput = false;
            bool inSyncBlkOutput = false;

            foreach (string line in lines)
            {
                // Start of clrstack output
                if (line.Trim() == clrStackStartMarker)
                {
                    inClrStackOutput = true;
                    inSyncBlkOutput = false;
                    continue;
                }
                // Start of syncblk output
                else if (line.Trim() == syncBlkStartMarker)
                {
                    inClrStackOutput = false;
                    inSyncBlkOutput = true;
                    continue;
                }
                // End of any command output
                else if (line.Trim() == endCommandMarker)
                {
                    inClrStackOutput = false;
                    inSyncBlkOutput = false;
                    continue;
                }

                // Collect the output based on current state
                if (inClrStackOutput)
                {
                    clrStackOutput.Add(line);
                }
                else if (inSyncBlkOutput)
                {
                    syncBlkOutput.Add(line);
                }
            }

            return (string.Join(Environment.NewLine, clrStackOutput), string.Join(Environment.NewLine, syncBlkOutput));
        }

        public static (bool isDeadlocked, string details) AnalyzeDeadlock(string outputFromAnalysis)
        {
            (string clrStackPattern, string syncblkPattern) = RetrievePattern(outputFromAnalysis);
            StackParser.StackTrace stackTrace = StackParser.StackTrace.ParseFromString(clrStackPattern);
            (bool isDeadlocked, string deadlockAnalysis) = stackTrace.AnalyzeDeadlock(syncblkPattern);
            return (isDeadlocked, deadlockAnalysis);
        }
    }

    public static class Deadlock
    {
        public static async Task<string> DetectAndDiagnoseDeadLock(string dumpPath)
        {
            using (Process process = new())
            {
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.Arguments = $"dump analyze {dumpPath}";
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.Start();

                using (StreamWriter writer = process.StandardInput)
                {
                    if (writer.BaseStream.CanWrite)
                    {
                        writer.WriteLine("clrstack -all");
                        writer.WriteLine(" ");
                        writer.WriteLine("syncblk");
                        writer.WriteLine("exit");
                    }
                }

                string output = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();

                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(error))
                {
                    throw new Exception($"Error during dump analysis: {error}");
                }

                (bool isDeadlocked, string result) = DeadlockAnalyzer.AnalyzeDeadlock(output);
                return result;
            }
        }
    }
}
