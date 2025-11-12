using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Core.Models.ServiceNow
{
    public class ServiceNowDateTimeConverter : JsonConverter<DateTime>
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dateString = reader.GetString();

            if (string.IsNullOrEmpty(dateString))
                return DateTime.MinValue;

            // ServiceNow API sometimes returns dates with timezone info
            if (dateString.Contains('+') || dateString.Contains('-'))
            {
                if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dateTime))
                {
                    return dateTime;
                }
            }

            // Try standard format
            if (DateTime.TryParseExact(dateString, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
            {
                return parsedDateTime;
            }

            // Fallback to flexible parsing
            return DateTime.Parse(dateString, CultureInfo.InvariantCulture);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
        }
    }
}
