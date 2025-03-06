using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models
{
    public record LongRunningOperationStatus(
        string OperationId,
        string Description
        );
}
