// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace E2ETests
{
    public class AzureFunctionProcess : IDisposable
    {
        public Process FuncHostProcess;
        private bool disposed;
        private bool _funcHostIsReady;
        private readonly bool _useShellExecute;
        private Regex _regex = new Regex(@"^(?!.*exception).*Functions\.", RegexOptions.Compiled | RegexOptions.IgnoreCase); // Filter function calls out of the working output except those which contain exceptions
        private IMessageSink _sink;

        public List<string> Startup = [];
        public List<string> Output = [];
        public List<string> WorkingOutput = [];

        public AzureFunctionProcess(
            string functionAppFolder,
            int port,
            IMessageSink sink,
            bool useShellExecute = true)
        {
            FuncHostProcess = new Process
            {
                StartInfo =
                {
                    FileName = "func",
                    Arguments = $"start -p {port}",
                    WorkingDirectory = functionAppFolder,
                    UseShellExecute = useShellExecute,
                    RedirectStandardOutput = !useShellExecute,
                    RedirectStandardError = !useShellExecute
                }
            };
            _useShellExecute = useShellExecute;
            _sink = sink;
        }

        public void Start(int timeoutSeconds = 30)
        {
            FuncHostProcess.OutputDataReceived += _check_if_started;
            FuncHostProcess.OutputDataReceived += StoreStartup;
            FuncHostProcess.ErrorDataReceived += StoreStartup;
            FuncHostProcess.Exited += _crashed_on_startup;

            try
            {
                FuncHostProcess.Start();

                var stopwatch = Stopwatch.StartNew();

                if (!_useShellExecute)
                {
                    FuncHostProcess.BeginOutputReadLine();

                    while (!FuncHostProcess.HasExited && !_funcHostIsReady && stopwatch.ElapsedMilliseconds < (timeoutSeconds * 1000))
                    {
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    Thread.Sleep(timeoutSeconds * 1000);
                    _funcHostIsReady = true;
                }
            }
            finally
            {
                FuncHostProcess.OutputDataReceived -= _check_if_started;
                FuncHostProcess.OutputDataReceived -= StoreStartup;
                FuncHostProcess.ErrorDataReceived -= StoreStartup;
                FuncHostProcess.Exited -= _crashed_on_startup;
            }

            if (!_funcHostIsReady)
            {
                string logs = string.Join(Environment.NewLine, Startup);
                throw new InvalidOperationException($"The Azure Functions host did not start up within an acceptable time.\n\nStartup Logs:\n{logs}");
            }
        }

        private void _crashed_on_startup(object? sender, EventArgs e)
        {
            Startup.Add($"Process exited with code {FuncHostProcess.ExitCode}");
            if (FuncHostProcess.ExitCode != 0)
            {
                string errorOutput = FuncHostProcess.StandardError.ReadToEnd();
                Startup.Add(errorOutput);
                throw new InvalidOperationException($"The Azure Functions host exited unexpectedly.\n\nStartup Logs:\n{string.Join(Environment.NewLine, Startup)}");
            }
        }

        private void _check_if_started(object sender, DataReceivedEventArgs e)
        {
            _sink.WriteLine("Waiting for function app to start...");

            if (e.Data?.Contains("For detailed output, run func with --verbose flag") ?? false)
            {
                _funcHostIsReady = true;
                _sink.WriteLine("Function app started");
                FuncHostProcess.OutputDataReceived += StoreOutput;
                FuncHostProcess.ErrorDataReceived += StoreOutput;
            }
        }

        private void StoreStartup(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Startup.Add(e.Data);
            }
        }

        private void StoreOutput(object sender, DataReceivedEventArgs e)
        {
            // Filter out function calls
            if (e.Data != null)
            {
                Output.Add(e.Data);

                if (true || !_regex.IsMatch(e.Data))
                {
                    WorkingOutput.Add(e.Data);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (FuncHostProcess != null)
                    {
                        if (!FuncHostProcess.HasExited)
                        {
                            FuncHostProcess.Kill();
                        }

                        FuncHostProcess.Dispose();
                    }
                }

                disposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// Waits for a specific output string from the Azure Function host process within a given timeout period.
        /// </summary>
        /// <param name="expectedOutput">The output string to wait for.</param>
        /// <param name="timeoutSeconds">The maximum number of seconds to wait for the expected output.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating whether the expected output was found.</returns>
        /// <exception cref="Exception">Thrown when the expected output is null.</exception>
        public async Task<bool> WaitForOutputAsync(string expectedOutput, int timeoutSeconds = 30)
        {
            if (expectedOutput == null)
            {
                throw new Exception($"{expectedOutput} cannot be null");
            }

            var tcs = new TaskCompletionSource<bool>();

            void CheckOutput(object sender, DataReceivedEventArgs e)
            {
                if (e.Data?.Contains(expectedOutput, StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    tcs.TrySetResult(true);
                }
            }

            FuncHostProcess.OutputDataReceived += CheckOutput;

            var timeoutTask = Task.Delay(timeoutSeconds * 1000).ContinueWith(_ => tcs.TrySetResult(false));

            var result = await tcs.Task;

            FuncHostProcess.OutputDataReceived -= CheckOutput;

            return result;
        }
    }
}
