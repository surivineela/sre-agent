// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Helpers
{
    public static class ModelSelectionHelper
    {
        public static bool IsReasoningModel(string modelName)
        {
            return !string.IsNullOrEmpty(modelName) && modelName.Contains("o1") || modelName.Contains("o3");
        }
    }
}

