using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive;
using System.Collections.Concurrent;
using System.Reactive.Subjects;

namespace FirstPartyAgent.Core.Services
{
    /// <summary>
    /// ISessionMessageService implementation by using System.Reactive
    /// </summary>
    public class SessionMessageService : ISessionMessageService
    {
        private ConcurrentDictionary<string, ReplaySubject<string>> _sessionMap;

        public SessionMessageService()
        {
            _sessionMap = new ConcurrentDictionary<string, ReplaySubject<string>>();
        }

        public void DeleteSession(string sessionId)
        {
            if (_sessionMap.ContainsKey(sessionId))
            {
                _sessionMap[sessionId].OnCompleted();
                _sessionMap.TryRemove(sessionId, out _);
            }
        }

        public Func<string, Task> GetPublisher(string sessionId)
        {
            if (!_sessionMap.ContainsKey(sessionId))
            {
                _sessionMap[sessionId] = new ReplaySubject<string>();
            }

            var subject = _sessionMap[sessionId];

            return async (message) =>
            {
                subject.OnNext(message);
                await Task.CompletedTask;
            };
        }

        public Task Subscribe(string sessionId, Func<string, Task> writer)
        {
            if (!_sessionMap.ContainsKey(sessionId))
            {
                _sessionMap[sessionId] = new ReplaySubject<string>();
            }
            var subject = _sessionMap[sessionId];

            var tcs = new TaskCompletionSource<bool>();

            // Subscribe to the subject
            subject.Subscribe(
                onNext: async message => await writer(message + "\n"),
                onError: ex => tcs.TrySetException(ex), 
                onCompleted: () => tcs.TrySetResult(true)
            );

            return tcs.Task;
        }
    }
}
