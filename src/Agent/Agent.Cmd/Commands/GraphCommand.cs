// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using System.Xml;
using Agent.Core.Configuration;
using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Path = System.IO.Path;

namespace Agent.Cmd
{
    public class GraphCommand
    {
        private readonly ILogger<GraphCommand> _logger;
        private readonly CosmosDBSettings _cosmosDBSettings;

        public GraphCommand(ILogger<GraphCommand> logger, CosmosDBSettings cosmosDBSettings)
        {
            _logger = logger;
            _cosmosDBSettings = cosmosDBSettings;
        }

        public void ExportGraph(CommandLineApplication command)
        {
            command.Description = "Test crawling subscription";
            command.HelpOption("-?|-h|--help");
            var exportPath = command.Argument("ExportPath", "File location to store");

            command.OnExecute(async () =>
            {
                await Export(exportPath.Value);

                return 0;
            });
        }

        private async Task Export(string path)
        {
            using (var gremlinClient = new GremlinClient(new GremlinServer($"{_cosmosDBSettings.Graph.AccountName}.gremlin.cosmos.azure.com", 443, enableSsl: true,
                    username: $"/dbs/{_cosmosDBSettings.Graph.Database}/colls/{_cosmosDBSettings.Graph.Collection}",
                    password: _cosmosDBSettings.Graph.ApiKey), messageSerializer: new Gremlin.Net.Structure.IO.GraphSON.GraphSON2MessageSerializer(new CustomGraphSON2Reader())))
            {
                try
                {
                    // Get vertices
                    var vertices = await gremlinClient.SubmitAsync<Dictionary<string, object>>("g.V().valueMap(true)");

                    // Get edges
                    var edges = await gremlinClient.SubmitAsync<Dictionary<string, object>>(
                        "g.E().project('id','source','target','label')" +
                        ".by(id()).by(outV().id()).by(inV().id()).by(label())");

                    // Create GEXF document
                    var doc = new XmlDocument();
                    var declaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                    doc.AppendChild(declaration);

                    // Create root element
                    var gexf = doc.CreateElement("gexf");
                    gexf.SetAttribute("xmlns", "http://gexf.net/1.3");
                    gexf.SetAttribute("xmlns:viz", "http://www.gephi.org/gexf/viz");
                    gexf.SetAttribute("version", "1.3");
                    doc.AppendChild(gexf);

                    // Create graph element
                    var graph = doc.CreateElement("graph");
                    graph.SetAttribute("mode", "static");
                    graph.SetAttribute("defaultedgetype", "directed");
                    gexf.AppendChild(graph);

                    var attributes = doc.CreateElement("attributes");
                    attributes.SetAttribute("class", "node");
                    attributes.SetAttribute("mode", "static");
                    graph.AppendChild(attributes);
                    HashSet<string> attrs = new HashSet<string>();

                    // Create nodes element
                    var nodes = doc.CreateElement("nodes");
                    graph.AppendChild(nodes);

                    // Add vertices
                    foreach (var vertex in vertices)
                    {
                        var json = JsonSerializer.Serialize(vertex);
                        var jsonNode = JsonSerializer.Deserialize<JsonElement>(json);
                        var node = doc.CreateElement("node");
                        node.SetAttribute("id", jsonNode.GetProperty("id").ToString());
                        node.SetAttribute("label", jsonNode.GetProperty("label").ToString());

                        // Add attributes as XML elements
                        var attvalues = doc.CreateElement("attvalues");
                        foreach (var prop in jsonNode.EnumerateObject())
                        {
                            if (prop.Name != "id" && prop.Name != "label")
                            {
                                var attvalue = doc.CreateElement("attvalue");
                                if (!attrs.Contains(prop.Name))
                                {
                                    var attr = doc.CreateElement("attribute");
                                    attr.SetAttribute("id", prop.Name);
                                    attr.SetAttribute("title", prop.Name);
                                    attr.SetAttribute("type", "string");
                                    attributes.AppendChild(attr);
                                    attrs.Add(prop.Name);
                                }
                                attvalue.SetAttribute("for", prop.Name);
                                // Assuming it's an array
                                attvalue.SetAttribute("value", string.Join(',', prop.Value.EnumerateArray().ToArray()));
                                attvalues.AppendChild(attvalue);
                            }
                        }
                        node.AppendChild(attvalues);
                        nodes.AppendChild(node);
                    }

                    // Create edges element
                    var edgesElement = doc.CreateElement("edges");
                    graph.AppendChild(edgesElement);

                    // Add edges
                    foreach (var edge in edges)
                    {
                        var edgeElement = doc.CreateElement("edge");
                        edgeElement.SetAttribute("id", edge["id"].ToString());
                        edgeElement.SetAttribute("source", edge["source"].ToString());
                        edgeElement.SetAttribute("target", edge["target"].ToString());
                        edgeElement.SetAttribute("label", edge["label"].ToString());
                        edgesElement.AppendChild(edgeElement);
                    }

                    // Save the document
                    path = Path.Join(path, "graph.gexf");
                    doc.Save(path);
                    Console.WriteLine($"Graph exported successfully to {path}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }
        }

        public void ExportGraphML(CommandLineApplication command)
        {
            command.Description = "Export graph data in GraphML format. Provide just a filename without a path to export it to the GremlinEmulator/Graphs.";
            command.HelpOption("-?|-h|--help");
            var exportPath = command.Argument("Filename", "Name of the GraphML file");

            command.OnExecute(async () =>
            {
                if (string.IsNullOrEmpty(exportPath.Value))
                {
                    Console.WriteLine("Error: Filename must be provided.");
                    return 1;
                }

                var outputPath = exportPath.Value;

                if (!Path.IsPathFullyQualified(outputPath))
                {
                    outputPath = Path.Combine(Assembly.GetExecutingAssembly().Location, @"..\..\..\..\..\..\GremlinEmulator\graphs\", Path.GetFileNameWithoutExtension(outputPath) + ".graphml");
                    outputPath = Path.GetFullPath(outputPath);
                }

                await ExportGraphMLAsync(outputPath);
                return 0;
            });
        }

        // this code was heavily AI generated, sorry.
        private async Task ExportGraphMLAsync(string path)
        {
            using (var gremlinClient = new GremlinClient(new GremlinServer($"{_cosmosDBSettings.Graph.AccountName}.gremlin.cosmos.azure.com", 443, enableSsl: true,
                    username: $"/dbs/{_cosmosDBSettings.Graph.Database}/colls/{_cosmosDBSettings.Graph.Collection}",
                    password: _cosmosDBSettings.Graph.ApiKey), messageSerializer: new Gremlin.Net.Structure.IO.GraphSON.GraphSON2MessageSerializer(new CustomGraphSON2Reader())))
            {
                try
                {
                    _logger.LogInformation("Starting GraphML export...");
                    // Get vertices and edges
                    var verticesList = await gremlinClient.SubmitAsync<Dictionary<string, object>>("g.V().valueMap(true)");
                    _logger.LogInformation($"Retrieved {verticesList.Count()} vertices.");
                    var edgesList = await gremlinClient.SubmitAsync<Dictionary<string, object>>(
                        "g.E().project('id','source','target','label').by(id()).by(outV().id()).by(inV().id()).by(label())");
                    _logger.LogInformation($"Retrieved {edgesList.Count()} edges.");

                    var doc = new XmlDocument();
                    var declaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                    doc.AppendChild(declaration);

                    var graphml = doc.CreateElement("graphml", "http://graphml.graphdrawing.org/xmlns");
                    graphml.SetAttribute("xmlns:xsi", "http://www.w3.org/2001/XMLSchema-instance");
                    graphml.SetAttribute("xsi:schemaLocation", "http://graphml.graphdrawing.org/xmlns http://graphml.graphdrawing.org/xmlns/1.0/graphml.xsd");
                    doc.AppendChild(graphml);

                    // --- Dynamic Key Generation ---
                    // Key for vertex labels (TinkerPop standard is 'labelV')
                    // This allows g.V().hasLabel('some_label') to work after import.
                    var keyForVertexLabel = doc.CreateElement("key");
                    keyForVertexLabel.SetAttribute("id", "labelV");
                    keyForVertexLabel.SetAttribute("for", "node");
                    keyForVertexLabel.SetAttribute("attr.name", "label");
                    keyForVertexLabel.SetAttribute("attr.type", "string");
                    graphml.AppendChild(keyForVertexLabel);                    // Discover and define keys for other vertex properties with proper types
                    var vertexPropertyKeys = new Dictionary<string, string>(); // property name -> GraphML type
                    foreach (Dictionary<string, object> vertexDict in verticesList)
                    {
                        foreach (var kvp in vertexDict)
                        {
                            string keyStr = kvp.Key;
                            if (!keyStr.Equals("id", StringComparison.OrdinalIgnoreCase) &&
                                !keyStr.Equals("label", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!vertexPropertyKeys.ContainsKey(keyStr))
                                {
                                    string graphMLType = GetGraphMLDataType(kvp.Value);
                                    vertexPropertyKeys[keyStr] = graphMLType;
                                }
                            }
                        }
                    }

                    foreach (var propKeyEntry in vertexPropertyKeys)
                    {
                        var keyElement = doc.CreateElement("key");
                        keyElement.SetAttribute("id", $"prop_{propKeyEntry.Key}");
                        keyElement.SetAttribute("for", "node");
                        keyElement.SetAttribute("attr.name", propKeyEntry.Key);
                        keyElement.SetAttribute("attr.type", propKeyEntry.Value);
                        graphml.AppendChild(keyElement);
                    }
                    _logger.LogInformation($"Defined {vertexPropertyKeys.Count} additional vertex property keys.");

                    // Key for edge label
                    var keyForEdgeLabel = doc.CreateElement("key");
                    keyForEdgeLabel.SetAttribute("id", "edge_label");
                    keyForEdgeLabel.SetAttribute("for", "edge");
                    keyForEdgeLabel.SetAttribute("attr.name", "label");
                    keyForEdgeLabel.SetAttribute("attr.type", "string");
                    graphml.AppendChild(keyForEdgeLabel);

                    var graph = doc.CreateElement("graph");
                    graph.SetAttribute("id", "G");
                    graph.SetAttribute("edgedefault", "directed");
                    graphml.AppendChild(graph);

                    // Add nodes
                    foreach (Dictionary<string, object> vdict in verticesList)
                    {
                        var nodeElement = doc.CreateElement("node");

                        string nodeIdStr = vdict.TryGetValue("id", out var idVal) ? GetStringValue(idVal) : string.Empty;
                        nodeElement.SetAttribute("id", nodeIdStr);

                        // Add vertex type (Cosmos 'label')
                        string nodeLabelStr = vdict.TryGetValue("label", out var labelVal) ? GetStringValue(labelVal) : string.Empty;

                        if (!string.IsNullOrEmpty(nodeLabelStr))
                        {
                            var dataElement = doc.CreateElement("data");
                            dataElement.SetAttribute("key", "labelV");
                            dataElement.InnerText = nodeLabelStr;
                            nodeElement.AppendChild(dataElement);
                        }                        // Add other properties
                        foreach (var propKeyEntry in vertexPropertyKeys)
                        {
                            string propKeyName = propKeyEntry.Key;
                            string propGraphMLType = propKeyEntry.Value;
                            
                            if (vdict.TryGetValue(propKeyName, out object? propValueObj) && propValueObj != null)
                            {
                                // This timestamp will constantly dirty the exported graph, so strip it.
                                if (propKeyName == "updateTs")
                                    continue;

                                var dataElement = doc.CreateElement("data");
                                dataElement.SetAttribute("key", $"prop_{propKeyName}");
                                dataElement.InnerText = FormatValueForGraphML(propValueObj, propGraphMLType);
                                nodeElement.AppendChild(dataElement);
                            }
                        }
                        graph.AppendChild(nodeElement);
                    }
                    _logger.LogInformation("Vertices processed for GraphML.");

                    // Add edges
                    foreach (Dictionary<string, object> edict in edgesList)
                    {
                        var edgeElement = doc.CreateElement("edge");
                        edgeElement.SetAttribute("id", edict.TryGetValue("id", out var edgeIdVal) ? GetStringValue(edgeIdVal) : string.Empty);
                        edgeElement.SetAttribute("source", edict.TryGetValue("source", out var edgeSourceVal) ? GetStringValue(edgeSourceVal) : string.Empty);
                        edgeElement.SetAttribute("target", edict.TryGetValue("target", out var edgeTargetVal) ? GetStringValue(edgeTargetVal) : string.Empty);

                        if (edict.TryGetValue("label", out object? labelObj) && labelObj != null)
                        {
                            var dataElement = doc.CreateElement("data");
                            dataElement.SetAttribute("key", "edge_label");
                            dataElement.InnerText = GetStringValue(labelObj);
                            edgeElement.AppendChild(dataElement);
                        }
                        graph.AppendChild(edgeElement);
                    }
                    _logger.LogInformation("Edges processed for GraphML.");
                    
                    doc.Save(path);
                    Console.WriteLine($"GraphML exported successfully to {path}");
                    _logger.LogInformation($"GraphML export completed: {path}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error exporting graph to GraphML: {e.Message}");
                    _logger.LogError(e, "Error exporting graph to GraphML");
                }
            }
        }

        private string GetStringValue(object propertyValue)
        {
            if (propertyValue == null) return string.Empty;

            // Handle strings early to prevent IEnumerable<char> processing by the IEnumerable handler later
            if (propertyValue is string s)
            {
                return s;
            }

            // Handle Gremlin.Net specific types first
            if (propertyValue is VertexProperty vp)
            {
                return GetStringValue(vp.Value);
            }
            if (propertyValue is Property p)
            {
                return GetStringValue(p.Value);
            }            // Handle Newtonsoft.Json.Linq types
            if (propertyValue is Newtonsoft.Json.Linq.JArray jArray)
            {
                if (jArray.Count > 0 && jArray.First != null)
                {
                    return GetStringValue(jArray.First);
                }
                return string.Empty;
            }

            if (propertyValue is Newtonsoft.Json.Linq.JValue jValue)
            {
                return jValue.Value?.ToString() ?? string.Empty;
            }

            // Handle System.Text.Json.JsonElement
            if (propertyValue is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in jsonElement.EnumerateArray())
                    {
                        return GetStringValue(item);
                    }
                    return string.Empty;
                }
                else if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return jsonElement.GetString() ?? string.Empty;
                }
                return jsonElement.ToString();
            }            // Handle generic System.Collections.IList (this should come after specific list types like JArray)
            if (propertyValue is System.Collections.IList list)
            {
                if (list.Count > 0 && list[0] != null)
                {
                    object firstItem = list[0]!;
                    return GetStringValue(firstItem);
                }
                return string.Empty;
            }
            // Handle other IEnumerable types that are not IList or string (e.g., the LINQ iterator)
            else if (propertyValue is System.Collections.IEnumerable enumerable)
            {
                System.Collections.IEnumerator? enumerator = null;
                try
                {
                    enumerator = enumerable.GetEnumerator();
                    if (enumerator.MoveNext() && enumerator.Current != null)
                    {
                        object firstItem = enumerator.Current;
                        return GetStringValue(firstItem);
                    }
                    else
                    {
                        return string.Empty;
                    }
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
            }

            return propertyValue?.ToString() ?? string.Empty;
        }        private string GetGraphMLDataType(object value)
        {
            if (value == null) return "string";

            // Handle Gremlin.Net specific types first
            if (value is VertexProperty vp)
            {
                return GetGraphMLDataType(vp.Value);
            }
            if (value is Property p)
            {
                return GetGraphMLDataType(p.Value);
            }

            // Handle Newtonsoft.Json.Linq types
            if (value is Newtonsoft.Json.Linq.JArray jArray)
            {
                if (jArray.Count > 0 && jArray.First != null)
                {
                    return GetGraphMLDataType(jArray.First);
                }
                return "string";
            }

            if (value is Newtonsoft.Json.Linq.JValue jValue)
            {
                return jValue.Value != null ? GetGraphMLDataType(jValue.Value) : "string";
            }

            // Handle System.Text.Json.JsonElement
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.True or JsonValueKind.False => "boolean",
                    JsonValueKind.Number => DetermineNumberType(jsonElement),
                    JsonValueKind.String => "string",
                    JsonValueKind.Array => jsonElement.GetArrayLength() > 0 
                        ? GetGraphMLDataType(jsonElement.EnumerateArray().First()) 
                        : "string",
                    _ => "string"
                };
            }            // Handle collections - take type from first element
            if (value is System.Collections.IList list && list.Count > 0 && list[0] != null)
            {
                return GetGraphMLDataType(list[0]!);
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                System.Collections.IEnumerator? enumerator = null;
                try
                {
                    enumerator = enumerable.GetEnumerator();
                    if (enumerator.MoveNext() && enumerator.Current != null)
                    {
                        return GetGraphMLDataType(enumerator.Current);
                    }
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
                return "string";
            }

            // Handle primitive types
            return value switch
            {
                bool => "boolean",
                byte or sbyte or short or ushort or int or uint => "int",
                long or ulong => "long",
                float => "float",
                double or decimal => "double",
                _ => "string"
            };
        }

        private string DetermineNumberType(JsonElement jsonElement)
        {
            if (jsonElement.TryGetInt32(out _))
                return "int";
            if (jsonElement.TryGetInt64(out _))
                return "long";
            if (jsonElement.TryGetSingle(out _))
                return "float";
            if (jsonElement.TryGetDouble(out _))
                return "double";
            return "string";
        }        private string FormatValueForGraphML(object value, string graphMLType)
        {
            if (value == null) return string.Empty;

            // Handle strings early to prevent IEnumerable<char> processing by collection handlers
            if (value is string str)
            {
                return str;
            }

            // Handle Gremlin.Net specific types first
            if (value is VertexProperty vp)
            {
                return FormatValueForGraphML(vp.Value, graphMLType);
            }
            if (value is Property p)
            {
                return FormatValueForGraphML(p.Value, graphMLType);
            }

            // Handle Newtonsoft.Json.Linq types
            if (value is Newtonsoft.Json.Linq.JArray jArray)
            {
                if (jArray.Count > 0 && jArray.First != null)
                {
                    return FormatValueForGraphML(jArray.First, graphMLType);
                }
                return string.Empty;
            }

            if (value is Newtonsoft.Json.Linq.JValue jValue)
            {
                return jValue.Value != null ? FormatValueForGraphML(jValue.Value, graphMLType) : string.Empty;
            }

            // Handle System.Text.Json.JsonElement
            if (value is JsonElement jsonElement)
            {
                return graphMLType switch
                {
                    "boolean" => jsonElement.GetBoolean().ToString().ToLowerInvariant(),
                    "int" => jsonElement.GetInt32().ToString(),
                    "long" => jsonElement.GetInt64().ToString(),
                    "float" => jsonElement.GetSingle().ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                    "double" => jsonElement.GetDouble().ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                    _ => jsonElement.ValueKind == JsonValueKind.String ? jsonElement.GetString() ?? string.Empty : jsonElement.ToString()
                };
            }            // Handle collections - take first element
            if (value is System.Collections.IList list && list.Count > 0 && list[0] != null)
            {
                return FormatValueForGraphML(list[0]!, graphMLType);
            }            if (value is System.Collections.IEnumerable enumerable)
            {
                System.Collections.IEnumerator? enumerator = null;
                try
                {
                    enumerator = enumerable.GetEnumerator();
                    if (enumerator.MoveNext() && enumerator.Current != null)
                    {
                        return FormatValueForGraphML(enumerator.Current, graphMLType);
                    }
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
                return string.Empty;
            }

            // Handle primitive types with proper formatting
            return graphMLType switch
            {
                "boolean" when value is bool boolVal => boolVal.ToString().ToLowerInvariant(),
                "int" when value is byte or sbyte or short or ushort or int or uint => value.ToString() ?? string.Empty,
                "long" when value is long or ulong => value.ToString() ?? string.Empty,                "float" when value is float floatVal => floatVal.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                "double" when value is double or decimal => value.ToString() ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };
        }
    }
}
