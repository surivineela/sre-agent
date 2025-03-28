// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public sealed record RepoUrlStatus(
    [Description("The GitHub repo url, taking the format https://github.com/..... ex. https://github.com/{ORG_NAME}/{REPO_NAME}.")]
    string RepoUrl);
