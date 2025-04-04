// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;

namespace Agent.Runtime.SubAgents.Core
{
    public static class DurableHelper
    {
        // For some reason, the helper is internal

        public static DurableTaskRegistry AddAllGeneratedTasks(DurableTaskRegistry builder)
        {
            return builder.AddAllGeneratedTasks();
        }
    }
}

