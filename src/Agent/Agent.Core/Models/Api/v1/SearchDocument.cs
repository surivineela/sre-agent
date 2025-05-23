// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record SearchDocument(
    string id,
    string? content,
    string? title,
    string? Url
    );
