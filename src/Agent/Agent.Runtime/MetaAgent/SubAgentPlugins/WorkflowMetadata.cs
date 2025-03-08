using Agent.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Runtime.MetaAgent;

public sealed record WorkflowMetadata<TInput>(
    // TODO: this should have a property of Teams thread id
    string WorkflowInstanceId,
    TInput Input);
