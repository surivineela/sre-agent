using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FirstPartyAgent.Core.Services
{
    public interface ISessionMessageService
    {
        Func<string, Task> GetPublisher(string sessionId);

        Task Subscribe(string sessionId, Func<string, Task> writer);

        void DeleteSession(string sessionId);
    }
}
