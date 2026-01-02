#nullable enable
using FlaxEditor;
using FlaxEngine;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProceduralGraph.Client;

/// <summary>
/// NodeLifecycleManager EditorPlugin.
/// </summary>
[PluginLoadOrder(InitializeAfter = typeof(ProceduralGraphBuilder))]
internal sealed class NodeLifecycleManager : EditorPlugin, IProceduralGraphLifecycle
{
    private readonly Dictionary<Scene, GraphInstance> _graphs = [];

    private CancellationTokenSource? _stoppingCts;
    private ImmutableArray<INodeConverter> _converters;
    public CancellationToken StoppingToken => _stoppingCts!.Token;

    public NodeLifecycleManager()
    {
        _graphs = [];

        _description = new PluginDescription()
        {
            Name = "Procedural Graph Client",
            Author = "William Brocklesby",
            AuthorUrl = "https://william-brocklesby.com",
            Category = "Procedural Graph",
            Description = "Procedural Graph Client is a Flax Engine Editor Plugin designed to manage and execute procedural graph generation in real-time. It serves as the runtime execution layer for the Procedural Graph system, handling the lifecycle of graph nodes, listening for scene changes, and managing asynchronous generation tasks to ensure editor responsiveness.",
            RepositoryUrl = "https://github.com/will11600/Procedural-Graph-Client.git",
            Version = new(1, 0, 0)
        };
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        try
        {
            ProceduralGraphBuilder builder = PluginManager.GetPlugin<ProceduralGraphBuilder>();
            _converters = ImmutableCollectionsMarshal.AsImmutableArray(builder.BuildConverters(this));

            _stoppingCts = new CancellationTokenSource();

            Level.SceneSaving += OnSceneSaving;

            Level.SceneLoaded += OnSceneLoaded;
            Level.SceneUnloaded += OnSceneUnloaded;

            Level.ActorSpawned += OnActorSpawned;
            Level.ActorDeleted += OnActorDeleted;
        }
        finally
        {
            base.Initialize();
        }
    }

    /// <inheritdoc/>
    public override void Deinitialize()
    {
        try
        {
            _stoppingCts!.Cancel();
        }
        finally
        {
            Level.SceneSaving -= OnSceneSaving;

            Level.SceneLoaded -= OnSceneLoaded;
            Level.SceneUnloaded -= OnSceneUnloaded;

            Level.ActorSpawned -= OnActorSpawned;
            Level.ActorDeleted -= OnActorDeleted;

            _stoppingCts?.Dispose();

            foreach (GraphInstance graph in _graphs.Values)
            {
                graph.Dispose();
            }

            base.Deinitialize();
        }
    }

    internal bool TryFindNode(Actor? actor, [NotNullWhen(true)] out INode? node)
    {
        if (actor != null && _graphs.TryGetValue(actor.Scene, out GraphInstance? graph))
        {
            return graph.TryGetValue(actor, out node);
        }

        node = default;
        return false;
    }

    private void OnActorSpawned(Actor actor)
    {
        Scene scene = actor.Scene;
        ref GraphInstance? sceneInfo = ref CollectionsMarshal.GetValueRefOrAddDefault(_graphs, scene, out bool exists);
        if (!exists)
        {
            sceneInfo = new GraphInstance(scene, _converters);
        }

        sceneInfo!.Add(actor);
    }

    private async void OnActorDeleted(Actor actor)
    {
        if (!_graphs.TryGetValue(actor.Scene, out GraphInstance? graph) || !graph.Remove(actor, out INode? node))
        {
            return;
        }

        try
        {
            await node.StopAsync(_stoppingCts!.Token);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
        finally
        {
            node.Dispose();
        }
    }

    private void OnSceneLoaded(Scene scene, Guid guid)
    {
        GraphInstance sceneInfo = new(scene, _converters);
        _graphs.Add(scene, sceneInfo);
    }

    private async void OnSceneUnloaded(Scene scene, Guid guid)
    {
        if (!_graphs.Remove(scene, out GraphInstance? graph))
        {
            return;
        }

        try
        {
            await graph.StopAsync(_stoppingCts!.Token);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
        finally
        {
            graph.Dispose();
        }
    }

    private void OnSceneSaving(Scene scene, Guid guid)
    {
        if (_graphs.TryGetValue(scene, out GraphInstance? graph))
        {
            graph.Save();        
        }
    }
}
