using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Models
{
    public sealed record APIManagementDescriptor(
        string ResourceId,
        string Name,
        string Type,
        string Location,
        string ResourceGroup);
}
