using FlaxEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ProceduralGraph.Client;

/// <summary>
/// ProceduralGraph class.
/// </summary>
internal sealed class ProceduralGraph
{
    [Serialize, ShowInEditor, JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
    public Node[] Nodes { get; private set; }

    public ProceduralGraph()
    {
        Nodes = [];
    }

    public ProceduralGraph(HashSet<Node> nodes)
    {
        Nodes = [.. nodes];
    }
}
