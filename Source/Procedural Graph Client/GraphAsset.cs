using System.Collections.Generic;

namespace ProceduralGraph.Client;

internal sealed class GraphAsset
{
    public NodeModel[] Nodes { get; set; }

    public GraphAsset()
    {
        Nodes = [];
    }

    public GraphAsset(IEnumerable<NodeModel> nodeModels)
    {
        Nodes = [.. nodeModels];
    }
}
