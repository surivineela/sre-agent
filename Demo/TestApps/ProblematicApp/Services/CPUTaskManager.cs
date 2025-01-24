using System.Collections.Concurrent;

namespace PerformanceIssues.Serivces
{
    public class CPUTaskManager
    {
        private readonly ConcurrentDictionary<string, ICPUIntensiveTask> _activeTasks = new();

        public string StartNewTask(int complexity)
        {
            var taskId = Guid.NewGuid().ToString();
            var task = new CPUIntensiveTask(complexity);
            task.Start();
            _activeTasks.TryAdd(taskId, task);
            return taskId;
        }

        public bool StopTask(string taskId)
        {
            if (_activeTasks.TryRemove(taskId, out var task))
            {
                task.Stop();
                return true;
            }
            return false;
        }

        public void StopAllTasks()
        {
            foreach (var task in _activeTasks.Values)
            {
                task.Stop();
            }
            _activeTasks.Clear();
        }

        public IEnumerable<string> GetActiveTasks()
        {
            return _activeTasks.Keys;
        }
    }
}
