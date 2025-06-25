// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record SearchDocument(
    string Id,
    string Content,
    string Title,
    string Url = ""
    );
