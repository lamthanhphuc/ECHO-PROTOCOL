---
name: unity
description: Unity 6.3 LTS game development for KLTN project. Use when creating or modifying C# scripts, scenes, GameObjects, prefabs, input, URP materials, or any Unity Editor task. Always prefer unity-editor MCP for Editor operations.
paths: ["KLTN/**/*.cs", "KLTN/**/*.unity", "KLTN/**/*.prefab", "KLTN/**/*.asset"]
---

# Unity 6.3 — ECHO PROTOCOL Project Skill

## Project context
- Unity root: `d:\Bin\KLTN\KLTN`
- Version: 6000.3.19f1, URP, Input System
- Backend: `http://localhost:5042/api`
- MCP: `unity-editor` on port 6400

## Namespaces
- `EchoProtocol.Core`, `EchoProtocol.Api`, `EchoProtocol.Auth`, `EchoProtocol.Networking`

## Editor operations (MCP first)
Before editing scene files manually, use `unity-editor` MCP to:
1. List hierarchy / active scene
2. Create GameObjects (primitives, empty)
3. Add/remove components and set serialized fields
4. Save scene after changes

## C# standards
- `PascalCase` types and methods; `_camelCase` private fields; `camelCase` locals
- One responsibility per MonoBehaviour
- Prefer `[SerializeField] private` over public fields
- Use `com.unity.inputsystem` (not legacy Input)
- Put scripts under `Assets/Scripts/<Feature>/`

## Common patterns
- **WASD movement**: `CharacterController` + Input System `PlayerInput` or `InputActionReference`
- **State machine**: enum + switch or dedicated `State` classes
- **Events**: C# events or UnityEvent for UI; avoid tight coupling
- **Data**: ScriptableObject for stats, items, wave configs

## After code changes
1. Wait for Unity compile (no MCP calls mid-compile)
2. Use MCP to attach scripts to GameObjects if needed
3. Ask user to test in Play mode

## Reference
- Architecture questions → `gamecodex` MCP
- Unity API docs → `context7` MCP
