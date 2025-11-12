// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using Microsoft.OperationalAgent.Core.Extensions;
using Shouldly;

namespace Agent.Tests.Unit;
public class StringExtensionsTests
{
    public static TheoryData<string?, string?, string> RecognizeAsDateTimeTestData =>
        new()
        {
                { "2024-07-31T10:30:00Z", "2024-07-31T10:30:00.0000000+00:00", "Valid UTC DateTimeString" },
                { "not-a-date", null, "Invalid DateTimeString" },
                { "", null, "Empty DateTimeString" },
                { null, null, "Null DateTimeString" },
                { "07/31/2024", "2024-07-31T00:00:00.0000000+00:00", "DateString MM/dd/yyyy" },
                { "2024-08-01 14:30:00 -07:00", "2024-08-01T14:30:00.0000000-07:00", "DateTimeString with Offset" },
                { "1 hour ago", DateTime.UtcNow.AddHours(-1).ToString(), "Relative DateTimeString" },
                { "2 days ago", DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd 00:00:00 zzz"), "Relative DateTimeString" },
        };

    [Theory]
    [MemberData(nameof(RecognizeAsDateTimeTestData))]
    public void RecognizeAsDateTime(string? inputString, string? expectedDateTimeString, string testCaseName)
    {
        // Arrange
        DateTimeOffset? expectedDateTimeOffset = null;
        if (expectedDateTimeString != null)
        {
            expectedDateTimeOffset = DateTimeOffset.Parse(expectedDateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        }

        // Act
        DateTimeOffset? actualResult = inputString.RecognizeAsDateTime();

        // The testCaseName parameter is used here to make the InlineData more readable and can be helpful for debugging.
        // If not used directly in assertions, it's good practice to acknowledge its purpose.
        _ = testCaseName;

        // Assert
        if (expectedDateTimeOffset == null)
        {
            actualResult.ShouldBeNull();
        }
        else
        {
            actualResult.ShouldNotBeNull();
            actualResult.Value.ShouldBe(expectedDateTimeOffset.Value, TimeSpan.FromMinutes(1));
        }
    }
}
