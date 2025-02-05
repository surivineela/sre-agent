// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;

public class MemoryAnalysisPlugin
{
    public static async Task<string> TakeMemoryDumpAsync(string resourceId)
    {
        return await ArmHelper.TakeMemoryDumpAsync(resourceId);
    }

    private class MemoryAnalysis
    {
        public List<ObjectHeapInfo> TopObjects { get; set; }

        public override string ToString()
        {
            return string.Join("\n", TopObjects.Select(o =>
                $"{o.TypeName}: {o.TotalSize} bytes ({o.InstanceCount} instances)"));
        }
    }

    private class ObjectHeapInfo
    {
        public string TypeName { get; set; }
        public long TotalSize { get; set; }
        public int InstanceCount { get; set; }
    }
}
