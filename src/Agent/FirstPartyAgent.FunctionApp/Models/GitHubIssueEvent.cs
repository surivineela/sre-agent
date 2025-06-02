// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.FunctionApp
{
    public class GitHubIssueEvent
    {
        public string? Action { get; set; }
        public Issue? Issue { get; set; }
        public Repository? Repository { get; set; }
        public User? Sender { get; set; }
    }

    public class Issue
    {
        public string? Url { get; set; }
        public int? Number { get; set; }
    }

    public class Repository
    {
        public string? Name { get; set; }
    }

    public class User
    {
        public string? Login { get; set; }
    }
}
