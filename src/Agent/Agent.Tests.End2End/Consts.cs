// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace E2ETests
{
    public static class Consts
    {
        internal const string Prefix = "operations-agent-e2e";
        public const string RgName = $"{Prefix}-rg";
        public const string AppServicePlanName = $"{Prefix}-plan";

        public const string ApprovalUrlFormatString = @"runtime/webhooks/durabletask/instances/{0}/raiseEvent/{1}?code={2}";
    }
}

