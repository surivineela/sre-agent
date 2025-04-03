// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Xml;
using Agent.Core.Configuration;
using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

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
    }
}

