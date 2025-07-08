// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;

namespace Agent.Plugins.DataConnectors.KustoMetadata
{
    public static class KustoMetadataExtensions
    {
        public static DurableTaskRegistry AddAllGeneratedTasks(DurableTaskRegistry builder)
        {
            return builder.AddAllGeneratedTasks();
        }
    }
}
