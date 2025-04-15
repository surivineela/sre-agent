using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Attributes;
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class Submit202Attribute : Attribute
{
    public required string ExecuteMethodName { get; set; }
}
