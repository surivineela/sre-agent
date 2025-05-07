// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record GitHubAccessToken(
    string AccessToken,
    DateTime? ExpiresOn
);
