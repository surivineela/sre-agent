// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record GitHubAccessToken(
    string AccessToken,
    DateTime? ExpiresOn,
    string? RefreshToken = null,
    DateTime? RefreshTokenExpiresOn = null
);

public record AzureDevOpsAccessToken(
    string AccessToken,
    DateTime? ExpiresOn,
    string? RefreshToken = null
);
