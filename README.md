> [!NOTE]
> This project is obsolete. You no longer need to install the Procedural Graph Client plugin to use Procedural Graph.
> [Procedural Graph Core](https://github.com/will11600/Procedural-Graph-Core) now includes all the features of this plugin.

# Procedural Graph Client

**Procedural Graph Client** is a [Flax Engine](https://flaxengine.com/) Editor Plugin designed to manage and execute procedural graph generation in real-time. It serves as the runtime execution layer for the Procedural Graph system, handling the lifecycle of graph nodes, listening for scene changes, and managing asynchronous generation tasks to ensure editor responsiveness.

## Features

- **Real-Time Generation**: Automatically triggers graph generation when compatible actors are spawned or modified in the scene.
- **Live Parameter Updates**: Listens for `Node.ParametersChanged` events and updates the graph in real-time with a debounce mechanism (0.2s) to prevent performance stuttering.
- **Asynchronous Execution**: Utilizing `Task` and `CancellationToken`, graph generation runs asynchronously, keeping the Flax Editor UI responsive.
- **Scene State Management**: Tracks nodes per scene and handles serialization/deserialization of graph data (`SceneData/{SceneName}/Procedural Graph.json`).
- **Actor Lifecycle Hooks**: Automatically registers nodes when actors are spawned and cleans up resources when actors are deleted.

## Installation

1. **Download**: Clone this repository into the `Plugins` folder of your Flax project.
2. **Submodules**: Ensure you fetch the required submodules (specifically [the Core](https://github.com/will11600/Procedural-Graph-Core)).
   ```bash
   git submodule update --init --recursive
   ```
   _See `.gitmodules` for details._

## Usage

Once installed and enabled in the Flax Editor, the **Procedural Graph Client** runs automatically in the background.

1. **Actor Integration**: When you create or spawn an actor that is compatible with the Procedural Graph system, the client detects it via `Level.ActorSpawned`.
2. **Node Conversion**: It uses the `ProceduralGraphBuilder` to convert the actor into a processing `Node`.
3. **Editing**: As you modify public variables or parameters on the node/actor, the client detects the change and schedules a regeneration task.
4. **Saving**: Graph data is automatically managed and saved to `SceneData/<SceneName>/Procedural Graph.json` upon scene serialization.

## Architecture

- **`ProceduralGraphClient.cs`**: The main `EditorPlugin` entry point. It manages the dictionary of active `NodeGeneratorHandle`s and orchestrates the event loops for spawning, updating, and deleting nodes.
- **`NodeGeneratorHandle.cs`**: A wrapper that manages the threading synchronization (`SemaphoreSlim`) and cancellation tokens for a specific node's generation task.
- **`SceneInfo.cs`**: Handles the loading and saving of the graph JSON data relative to the current scene.

## License

This project is licensed under the **PolyForm Shield License 1.0.0**.

> **Summary:** You are free to use, modify, and distribute this software, provided you do not use it to build a product that competes with the software itself.

Please see the [LICENSE.md](LICENSE.md) file for the full legal text.
