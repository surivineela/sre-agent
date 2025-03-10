using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Plugins.Definitions
{
    public interface IPostToTeamsPlugin
    {
        Task<string> PostAsync(string message);
    }
}
