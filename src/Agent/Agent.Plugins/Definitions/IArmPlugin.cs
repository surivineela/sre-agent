using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins
{
    public interface IArmPlugin
    {
        Task<string> SetMinimumTlsVersion(string appResourceId, string minimumTlsVersion);
    }
}
