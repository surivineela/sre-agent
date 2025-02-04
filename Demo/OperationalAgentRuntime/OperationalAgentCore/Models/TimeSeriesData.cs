using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentCore;

public class TimeSeriesData
{
    public string Name { get; set; }
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; }
}
