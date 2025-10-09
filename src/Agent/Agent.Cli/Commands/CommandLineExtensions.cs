// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Helpers
{
    public static class CommandLineCompatExtensions
    {
        /// <summary>
        /// Recursively adds the given options to the command and all sub-commands.
        /// Use this on beta builds that don't expose AddGlobalOption(...).
        /// </summary>
        public static void AddGlobalOptionsCompat(this Command command, params Option[] options)
        {
            foreach (var o in options)
                command.Add(o);

            foreach (var sub in command.Children.OfType<Command>())
                sub.AddGlobalOptionsCompat(options);
        }
    }
}
