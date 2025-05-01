using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models;
public record ApprovalContext(
    Guid ThreadId,
    Guid ApprovalId)
{
}
