using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
