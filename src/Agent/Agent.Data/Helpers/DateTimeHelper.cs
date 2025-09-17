// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.Helpers;

public class DateTimeHelper
{
    public static DateTimeOffset ParseDateTimeOffset(string? value)
    {
        DateTimeOffset createdAt;
        if (!string.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, out var parsedDate))
        {
            createdAt = parsedDate;
        }
        else
        {
            createdAt = DateTimeOffset.UtcNow;
        }

        return createdAt;
    }
}
