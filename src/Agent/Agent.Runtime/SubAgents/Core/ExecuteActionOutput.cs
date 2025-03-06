using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.SubAgents.Core;

public sealed record ExecuteActionOutput(
    ChatMessage ChatMessage,
    bool Is202Submit);
