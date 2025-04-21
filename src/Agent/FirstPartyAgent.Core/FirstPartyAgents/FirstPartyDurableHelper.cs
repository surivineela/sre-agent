// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.DurableTask;

namespace FirstPartyAgent.Core.FirstPartyAgents;
public static class FirstPartyDurableHelper
{
    public static DurableTaskRegistry AddAllGeneratedTasks(DurableTaskRegistry builder)
    {
        return builder.AddAllGeneratedTasks();
    }
}
