using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Models
{
    public class ApplensIssueRootCause
    {
        public string RootCauseIntent { get; set; }
        public string RootCauseMessage { get; set; }
        public string QuickMitigation { get; set; }
        public string DataCollection { get; set; }
    }

    public enum DataCollection
    {
        None = 0,
        MemoryDump,
        ProfilerTrace
    }

    public enum QuickMitigation
    {
        Reboot = 0,
        ScaleUp,
        ScaleOut
    }
}
