#nullable enable
using FlaxEditor;
using FlaxEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProceduralGraph.Client;

internal sealed class GraphInstance : IReadOnlyDictionary<Actor, INode>, IDisposable
{
    public string AssetPath { get; }

    public Scene Scene { get; }

    public int Count => _nodes.Count;

    public IEnumerable<Actor> Keys => _nodes.Keys;

    public IEnumerable<INode> Values => _nodes.Values;

    public INode this[Actor key] => _nodes[key];

    private readonly ImmutableArray<INodeConverter> _converters;
    private readonly Dictionary<Actor, INode> _nodes;
    private readonly Dictionary<Guid, List<Model>> _models;
    private bool _disposed;

    public GraphInstance(Scene scene, ImmutableArray<INodeConverter> converters)
    {
        AssetPath = Path.Combine(Globals.ProjectContentFolder, "SceneData", scene.Name, "Procedural Graph.json");
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _converters = converters;

        _nodes = [];
        _models = Load(AssetPath);
        AddNodesRecurively(scene);
    }

    public void Save()
    {
        GraphAsset graph = new(_nodes.Values.SelectMany(NodeModels));
        Editor.SaveJsonAsset(AssetPath, graph);
    }

    public bool ContainsKey(Actor key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _nodes.ContainsKey(key);
    }

    public bool TryGetValue(Actor key, [MaybeNullWhen(false)] out INode value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _nodes.TryGetValue(key, out value);
    }

    public IEnumerator<KeyValuePair<Actor, INode>> GetEnumerator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _nodes.GetEnumerator();
    }

    public bool Add(Actor key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ref INode? node = ref CollectionsMarshal.GetValueRefOrAddDefault(_nodes, key, out bool exists);
        if (!exists && TryGetConverter(key, out INodeConverter? converter))
        {
            node = Convert(key, converter);
            return true;
        }
        return false;
    }

    public bool Remove(Actor key, [NotNullWhen(true)] out INode? value)
    {
        return _nodes.Remove(key, out value); 
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        List<Task> stoppings = new(_nodes.Count);

        foreach (INode node in _nodes.Values)
        {
            Task stopping = node.StopAsync(cancellationToken);
            stoppings.Add(stopping);
        }

        await Task.WhenAll(stoppings);
    }

    private void AddNodesRecurively(Actor parent)
    {
        if (TryGetConverter(parent, out INodeConverter? converter))
        {
            INode node = Convert(parent, converter);
            _nodes.Add(parent, node);
        }

        foreach (Actor actor in parent.Children)
        {
            AddNodesRecurively(actor);
        }
    }

    private INode Convert(Actor actor, INodeConverter converter)
    {
        if (_models.Remove(actor.ID, out List<Model>? models))
        {
            return converter.Convert(actor, models);
        }

        return converter.Convert(actor, []);
    }

    private bool TryGetConverter(Actor actor, [NotNullWhen(true)] out INodeConverter? result)
    {
        foreach (INodeConverter converter in _converters)
        {
            if (converter.CanConvert(actor))
            {
                result = converter;
                return true;
            }
        }

        result = default;
        return false;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (INode node in _nodes.Values)
            {
                node.Dispose();
            }
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private static Dictionary<Guid, List<Model>> Load(string path)
    {
        if (Content.LoadAsync<JsonAsset>(path)?.GetInstance<GraphAsset>() is not GraphAsset proceduralGraph)
        {
            return [];
        }

        Dictionary<Guid, List<Model>> nodes = [];
        foreach (NodeModel nodeModel in proceduralGraph.Nodes)
        {
            ref List<Model>? models = ref CollectionsMarshal.GetValueRefOrAddDefault(nodes, nodeModel.ActorID, out bool exists);

            if (exists)
            {
                models!.Add(nodeModel.Model);
                continue;
            }

            models = [nodeModel.Model];
        }

        return nodes;
    }

    private static IEnumerable<NodeModel> NodeModels(INode node)
    {
        Guid actorId = node.Actor.ID;
        foreach (Model model in node.Models)
        {
            yield return new NodeModel(actorId, model);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
