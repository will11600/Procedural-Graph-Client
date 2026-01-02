#nullable enable
using FlaxEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ProceduralGraph.Client;

internal sealed class NodeModel
{
    [Serialize, ShowInEditor]
    public Guid ActorID { get; set; }

    [Serialize, ShowInEditor, JsonProperty(ItemTypeNameHandling = TypeNameHandling.Auto)]
    public Model Model { get; set; }

    public NodeModel(Guid actorID, Model model)
    {
        ActorID = actorID;
        Model = model;
    }

    public NodeModel()
    {
        ActorID = Guid.Empty;
        Model = default!;
    }
}