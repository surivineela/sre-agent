// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;

namespace Microsoft.OperationalAgent.Core.Extensions;

public static class StringExtensions
{
    public static DateTimeOffset? RecognizeAsDateTime(this string? str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            return null;
        }

        // Try to parse directly using DateTimeOffset.TryParse
        // This is efficient and covers many standard ISO 8601 and other common formats.
        // DateTimeStyles.AssumeUniversal ensures that if no offset is specified, it's treated as UTC.
        if (DateTimeOffset.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset result))
        {
            return result;
        }

        // If direct parsing fails, use Microsoft.Recognizers.Text for more complex or natural language date/time strings.
        var recognizeResult = DateTimeRecognizer.RecognizeDateTime(str, Culture.English, DateTimeOptions.None, DateTime.UtcNow);
        var dateTimeResult = recognizeResult?.FirstOrDefault()?.Resolution["values"] as List<Dictionary<string, string>>;

        // The recognizer can return multiple possible interpretations. We take the first one.
        // The "value" field in the resolution dictionary contains a string representation of the recognized date/time.
        if (dateTimeResult?.FirstOrDefault()?.TryGetValue("value", out var value) == true && value is string dateTimeString)
        {
            // Try to parse the string value obtained from the recognizer.
            // Again, use InvariantCulture and AssumeUniversal/AdjustToUniversal for consistency.
            if (DateTimeOffset.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            {
                return result;
            }
        }

        // If no valid DateTimeOffset could be parsed, return null.
        return null;
    }
}
