// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Gremlin.Net.Structure.IO.GraphSON;
using System.Text.Json;

namespace GremlinMetricsExporter
{

    // workaround for Numberic types
    // https://stackoverflow.com/questions/68092798/gremlin-net-deserialize-number-property/72316108#72316108
    public class CustomGraphSON2Reader : GraphSON2Reader
    {
        public override dynamic ToObject(JsonElement graphSon) =>
            graphSon.ValueKind switch
            {
                // numbers
                JsonValueKind.Number when graphSon.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when graphSon.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when graphSon.TryGetDecimal(out var decimalValue) => decimalValue,


                _ => base.ToObject(graphSon)
            };
    }
}

