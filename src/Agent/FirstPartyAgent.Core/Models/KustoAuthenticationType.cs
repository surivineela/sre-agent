using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Models
{
    public enum KustoAuthenticationType
    {
        ManagedIdentity,
        UAMI,
        App,
        User, // for testing
    }
}
