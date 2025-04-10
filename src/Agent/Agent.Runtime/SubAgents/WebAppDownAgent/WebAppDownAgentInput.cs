using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.SubAgents.WebAppDownAgent;


public sealed record WebAppDownAgentInput(
    WebAppDownInput Input,
    IReadOnlyList<string> ToolSignatures,
    ThreadContext Context);
