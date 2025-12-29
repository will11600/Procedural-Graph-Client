#nullable enable
using FlaxEditor;
using FlaxEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ProceduralGraph.Client;

/// <summary>
/// ProceduralGraphClient EditorPlugin.
/// </summary>
[PluginLoadOrder(InitializeAfter = typeof(ProceduralGraphBuilder))]
internal sealed class ProceduralGraphClient : EditorPlugin
{
    private static readonly TimeSpan DebouncePeriod = TimeSpan.FromSeconds(0.2);

    private readonly Dictionary<Node, NodeGeneratorHandle> _handles;
    private readonly Dictionary<Guid, SceneInfo> _scenes;
    private readonly Dictionary<Guid, ActorNode> _actors;

    private CancellationTokenSource? _stoppingCts;

    private readonly ProceduralGraphBuilder _nodeConverterFactory;

    public ProceduralGraphClient()
    {
        _nodeConverterFactory = PluginManager.GetPlugin<ProceduralGraphBuilder>();

        _handles = [];
        _scenes = [];
        _actors = [];

        _description = new PluginDescription()
        {
            Name = "Procedural Graph Client",
            Author = "William Brocklesby",
            AuthorUrl = "https://william-brocklesby.com",
            Category = "Procedural Graph",
            Version = new(1, 0, 0)
        };
    }

    /// <inheritdoc/>
    public override void Initialize()
    {
        try
        {
            _stoppingCts = new CancellationTokenSource();

            Node.ParametersChanged += OnParametersChanged;

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
            Node.ParametersChanged -= OnParametersChanged;

            Level.SceneLoaded -= OnSceneLoaded;
            Level.SceneUnloaded -= OnSceneUnloaded;

            Level.ActorSpawned -= OnActorSpawned;
            Level.ActorDeleted -= OnActorDeleted;

            _stoppingCts!.Dispose();

            base.Deinitialize();
        }
    }

    private void OnActorSpawned(Actor actor)
    {
        if (_nodeConverterFactory.TryConvertFirst(actor, out ActorNode? node) && _actors.TryAdd(actor.ID, node))
        {
            NodeGeneratorHandle handle = new(node, _stoppingCts!.Token);
            _handles.Add(node, handle);

            ref SceneInfo? info = ref CollectionsMarshal.GetValueRefOrAddDefault(_scenes, actor.Scene.ID, out bool exists);
            if (exists)
            {
                info!.Nodes.Add(node);
                return;
            }
            info = new SceneInfo(actor.Scene);
            info.Nodes.Add(node);
        }
    }

    private async void OnActorDeleted(Actor actor)
    {
        if (_actors.Remove(actor.ID, out ActorNode? node))
        {
            await TryRemoveNodeAsync(node);
        }
    }

    private void OnSceneLoaded(Scene scene, Guid guid)
    {
        SceneInfo info = new(scene);
        _scenes.Add(guid, info);
        SpawnActors(scene);
    }

    private async void OnSceneUnloaded(Scene scene, Guid guid)
    {
        if (!_scenes.Remove(guid, out SceneInfo? info))
        {
            return;
        }

        List<Task> removals = new(info.Nodes.Count);
        CancellationToken stoppingToken = _stoppingCts!.Token;
        foreach (Node node in info.Nodes)
        {
            if (_handles.Remove(node, out NodeGeneratorHandle? handle))
            {
                Task removal = DisposeHandleAsync(handle, stoppingToken);
                removals.Add(removal);
            }
        }

        try
        {
            await Task.WhenAll(removals);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
    }

    private void OnParametersChanged(Node sender)
    {
        NodeGeneratorHandle asyncInfo = _handles[sender];
        Interlocked.Exchange(ref asyncInfo.isDirty, true);
        if (asyncInfo.semaphore.Wait(0))
        {
            BeginGenerating(sender, asyncInfo);
        }
    }

    private void SpawnActors(Actor parent)
    {
        OnActorSpawned(parent);
        foreach (Actor actor in parent.Children)
        {
            SpawnActors(actor);
        }
    }

    private async ValueTask<bool> TryRemoveNodeAsync(Node node)
    {
        if (_handles.Remove(node, out NodeGeneratorHandle? handle))
        {
            await DisposeHandleAsync(handle, _stoppingCts!.Token);
            return true;
        } 

        return false;
    }

    private async void BeginGenerating(Node node, NodeGeneratorHandle handle)
    {
        try
        {
            using PeriodicTimer periodicTimer = new(DebouncePeriod);
            while (Interlocked.Exchange(ref handle.isDirty, false) && await periodicTimer.WaitForNextTickAsync(handle.StoppingToken))
            {
                if (!handle.isDirty)
                {
                    await GenerateAsync(node, handle.StoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
        finally
        {
            handle.semaphore.Release();
        }
    }

    private static async Task GenerateAsync(Node node, CancellationToken cancellationToken)
    {
        IGenerator generator = node.CreateGenerator();

        await generator.BuildAsync(cancellationToken);

        List<Exception>? exceptions = null;

        if ((node.Flags & NodeFlags.PropagateDownwards) != 0)
        {
            foreach (Node child in node.Children)
            {
                try
                {
                    child.OnParentStateChanged();
                }
                catch (Exception ex)
                {
                    exceptions ??= [];
                    exceptions.Add(ex);
                }
            }
        }

        if ((node.Flags & NodeFlags.PropagateUpwards) != 0)
        {
            try
            {
                node.Parent?.OnChildStateChanged(node);
            }
            catch (Exception ex)
            {
                exceptions ??= [];
                exceptions.Add(ex);
            }
        }

        if (exceptions is not null)
        {
            throw new AggregateException(exceptions);
        }
    }

    private static async Task DisposeHandleAsync(NodeGeneratorHandle handle, CancellationToken cancellationToken)
    {
        try
        {
            await handle.CancelAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            handle.Dispose();
        }
    }
}
