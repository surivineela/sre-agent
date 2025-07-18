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
        var assemblies = appDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var name = assembly.GetName().Name;
            if (name != null && (name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase) ||
                                 name.StartsWith("Microsoft.TestPlatform", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
