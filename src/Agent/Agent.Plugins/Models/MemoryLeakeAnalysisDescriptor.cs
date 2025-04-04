// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Plugins.Models
{
    public sealed record MemoryLeakeAnalysisDescriptor(
    [Description("Full GitHub repository URL. Can be inferred from app if CI/CD Enabled.Always confirm")] string repoUrl,
    [Description("Base branch name. Can be inferred from app if CI/CD Enabled.Always confirm")] string baseBranch,
    [Description("New branch name for fixes")] string newBranch);
}

