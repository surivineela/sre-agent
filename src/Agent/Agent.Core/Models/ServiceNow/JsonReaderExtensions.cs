using System.Text.Json;

namespace Agent.Core.Models.ServiceNow
{
    public static class JsonReaderExtensions
    {
        public static string GetRawStringValue(this Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    if (reader.TryGetInt64(out long longValue))
                        return longValue.ToString();
                    return reader.GetDouble().ToString();
                case JsonTokenType.True:
                    return "true";
                case JsonTokenType.False:
                    return "false";
                default:
                    return string.Empty;
            }
        }
    }
}
