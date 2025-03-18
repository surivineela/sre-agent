using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Models;

namespace Agent.Plugins
{
    public interface IArmPlugin
    {
        Task<string> SetMinimumTlsVersion(string appResourceId, string minimumTlsVersion);
        Task<List<TlsStatus>> GetTlsSettings(List<string> resourceIds);
        Task<bool> CheckIfResourceExists(string appResourceId);
        Task<bool> RestartWebApp(string appResourceId);
    }
}
