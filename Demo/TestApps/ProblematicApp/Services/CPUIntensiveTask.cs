namespace PerformanceIssues.Serivces
{
    public class CPUIntensiveTask : ICPUIntensiveTask
    {
        private readonly int _complexity;
        private volatile bool _isRunning;
        private readonly List<double[]> _results = new();

        public CPUIntensiveTask(int complexity)
        {
            _complexity = complexity;
        }

        public void Start()
        {
            _isRunning = true;
            Task.Run(() =>
            {
                while (_isRunning)
                {
                    var results = new double[1000];
                    for (int i = 0; i < _complexity; i++)
                    {
                        results[i % 1000] = Math.Pow(Math.Sin(i), Math.Cos(i)) +
                                          Math.Sqrt(Math.Abs(Math.Tan(i)));
                    }
                    // Memory leak: storing results without bounds
                    _results.Add(results);
                }
            });
        }

        public void Stop()
        {
            _isRunning = false;
        }
    }
}
