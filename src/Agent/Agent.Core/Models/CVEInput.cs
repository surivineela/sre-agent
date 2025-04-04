// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Core.Models;

public class CVEInput
{
    [Description("GitHub repos to scan. Each Repo will contain a url of the format https://github.com/... (ex. https://github.com/{ORG_NAME}/{REPO_NAME})")]
    public List<RepoUrlStatus> ReposToScan { get; set; }
}

