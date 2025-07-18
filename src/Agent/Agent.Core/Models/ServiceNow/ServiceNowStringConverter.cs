using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Agent.Core.Models.ServiceNow
{
    public class ServiceNowStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle null
            if (reader.TokenType == JsonTokenType.Null)
                return string.Empty;
            
            // Handle string directly
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString() ?? string.Empty;

            // If it's an object (like {"value":"something"}), try to extract string value
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
                {
                    // Try to extract 'value' property if present
                    if (doc.RootElement.TryGetProperty("value", out JsonElement valueElement) && 
                        valueElement.ValueKind == JsonValueKind.String)
                    {
                        return valueElement.GetString() ?? string.Empty;
                    }
                    
                    // Try to get any string property as fallback
                    foreach (var property in doc.RootElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String)
                        {
                            return property.Value.GetString() ?? string.Empty;
                        }
                    }
                    
                    // Return serialized JSON as last resort
                    return doc.RootElement.ToString();
                }
            }
            
            // For any other type, convert to string using the extension method
            return reader.GetRawStringValue();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
