using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.AI;

namespace OperationalAgentRuntime.Models
{
    public class TrackedAction
    {
        public ChatRole Role { get; set; }
        public string Content { get; set; }
    }
}
