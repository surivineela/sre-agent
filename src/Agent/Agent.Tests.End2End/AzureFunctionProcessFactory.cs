// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using Xunit.Abstractions;

namespace E2ETests
{
    public class AzureFunctionProcessFactory
    {
        public AzureFunctionProcessFactory() { }

        private bool CanCreate()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "func",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    return output.Contains("Azure Functions Core Tools");
                }
            }
            catch
            {
                return false;
            }
        }

        public AzureFunctionProcess Create(string functionAppFolder, int port, IMessageSink _sink, bool useShellExecute = false)
        {
            if (!CanCreate())
            {
                throw new Exception("Install azure-functions-core-tools and add them to path for this test to work");
            }

            return new AzureFunctionProcess(functionAppFolder, port, _sink, useShellExecute);
        }
    }
}
