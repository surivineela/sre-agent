using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agents.Core.Plugins;

public class TimePlugin

{
    [KernelFunction("get_current_time")]
    [Description("Tells you the current time in UTC")]
    public DateTime GetCurrentUtcTime()
    {
        return DateTime.UtcNow;
    }
}
