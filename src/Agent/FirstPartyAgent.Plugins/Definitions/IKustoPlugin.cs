using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Plugins
{
    public interface IKustoPlugin
    {
        public Task<string> ExecuteKustoQuery(string region, string query);
    }
}
