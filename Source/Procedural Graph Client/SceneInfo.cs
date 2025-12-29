#nullable enable
using FlaxEditor;
using FlaxEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace ProceduralGraph.Client;

internal sealed class SceneInfo
{
    public string AssetPath { get; }

    public Scene Scene { get; }

    public HashSet<Node> Nodes { get; }

    public SceneInfo(Scene scene)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));

        AssetPath = Path.Combine(Globals.ProjectContentFolder, "SceneData", scene.Name, "Procedural Graph.json");

        Nodes = [];
        JsonAsset asset = Content.LoadAsync<JsonAsset>(AssetPath);
        if (asset != null)
        {
            ProceduralGraph graph = asset.GetInstance<ProceduralGraph>();
            foreach (Node node in graph.Nodes)
            {
                Nodes.Add(node); 
            }
        }
    }

    public void Flush()
    {
        ProceduralGraph graph = new(Nodes);
        Editor.SaveJsonAsset(AssetPath, graph);
    }
}