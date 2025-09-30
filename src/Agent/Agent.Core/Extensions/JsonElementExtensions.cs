using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent.Core.Extensions;
public static class JsonElementExtensions
{
    public static JsonElement TryGet(this JsonElement root, params string[] propertyNames)
    {
        JsonElement currentElement = root;
        foreach (var propertyName in propertyNames)
        {
            if (currentElement.ValueKind == JsonValueKind.Object && currentElement.TryGetProperty(propertyName, out JsonElement nextElement))
            {
                currentElement = nextElement;
            }
            else
            {
                return default;
            }
        }

        return currentElement;
    }
}
