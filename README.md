# 3D Bin Packing Game

Unity prototype for an interactive 3D bin-packing scene. The project loads package dimensions from JSON, lets the player place boxes into a container, tracks placement state, and renders package previews and placed objects in the Unity scene.

## Requirements

- Unity `2022.3.62f2c1`
- Git with SSH access to `git@github.com:3DBP-Lab/3D-Bin-Packing-Game.git`

## Project Layout

- `Assets/Scripts/` contains the runtime C# scripts for simulation flow, player input, UI rendering, camera control, and shared data structures.
- `Assets/Scenes/SampleScene.unity` is the current playable scene.
- `Assets/Prefabs/` stores package and effector prefabs.
- `Assets/Materials/` stores package and ghost-placement materials.
- `Assets/StreamingAssets/packages.json` provides package dimensions loaded at runtime.
- `Packages/` and `ProjectSettings/` contain Unity package and project configuration.

Generated Unity folders such as `Library/`, `Logs/`, and `UserSettings/` are intentionally ignored.

## Getting Started

1. Clone the repository.
2. Open the project folder in Unity Hub with Unity `2022.3.62f2c1`.
3. Open `Assets/Scenes/SampleScene.unity`.
4. Press Play to run the simulation.

## Development Notes

Keep Unity `.meta` files with their assets when moving or renaming files. Update `Assets/StreamingAssets/packages.json` to change the package queue used by the scene. Add tests under `Assets/Tests/EditMode` or `Assets/Tests/PlayMode` when introducing logic that should be validated outside manual Play Mode checks.
