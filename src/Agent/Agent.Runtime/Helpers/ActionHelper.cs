using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Attributes;
using Agent.Runtime.SubAgents;

namespace Agent.Runtime.Helpers;
public static class ActionHelper
{
    public static bool ToolShouldBeRecorded(IToolFunction tool)
    {
        bool requiresApproval = ApprovalHelper.ToolRequiresApproval(tool);

        var attribute = tool.ToolFunction.UnderlyingMethod?.GetCustomAttribute<RecordActionAttribute>();

        return requiresApproval || attribute != null;
    }
}
