// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;

namespace Agent.Plugins.DataConnectors
{
    public static class PluginsDurableTaskExtensions
    {
        /// <summary>
        /// Registers all DurableTask orchestrators and activities for plugins in the Agent.Plugins assembly.
        /// This includes Kusto, TSG, and any other plugin tasks.
        /// </summary>
        public static DurableTaskRegistry AddAllGeneratedTasks(DurableTaskRegistry builder)
        {
            return builder.AddAllGeneratedTasks();
        }
    }
}
