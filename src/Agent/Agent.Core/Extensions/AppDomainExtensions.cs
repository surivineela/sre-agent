using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Core.Extensions;
public static class AppDomainExtensions
{
    public static bool IsTestingContext(this AppDomain appDomain)
    {
        return appDomain.GetAssemblies().Any(a =>
            a.GetName().Name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
            || a.GetName().Name.StartsWith("Microsoft.TestPlatform", StringComparison.OrdinalIgnoreCase)
        );
    }
}
