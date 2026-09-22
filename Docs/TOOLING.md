# Development Tooling

## Required local stack

- Unity Hub;
- Unity 6000.5.9f1 editor with Windows Build Support (D-010);
- Git available on `PATH`;
- Git LFS;
- GitHub CLI (`gh`) authenticated for the `scootsy/wheels` remote;
- an MCP-capable primary coding agent (Claude Code);
- Unity MCP connected to the open editor.

Blender and Blender MCP are not required before the human playtest gate. Unity primitives are the authorized M1 art source.

## Unity MCP bridge

The approved development bridge is Unity's own AI Assistant package, `com.unity.ai.assistant`, which exposes the editor over MCP (D-011). It is a development tool, not shipped gameplay code. The installed version is pinned in `Packages/manifest.json` and `Packages/packages-lock.json`; do not let it upgrade unreviewed.

The package communicates with the local editor only. Client configuration (for example `UserSettings/mcp.json`) is machine-local and must not be committed; `UserSettings/` is ignored by Git.

Setup path on a new machine:

1. Open the project in Unity Editor 6000.5.9f1, not only Unity Hub.
2. Let Package Manager restore `com.unity.ai.assistant` from the manifest.
3. Enable the MCP server in the AI Assistant settings and register the coding agent as a client.
4. Approve the client connection when the editor asks.
5. Keep the Unity project open while the coding agent works.

## Connection verification

Before implementation, the primary agent must successfully:

- read the active Unity editor/project identity;
- inspect the current scene hierarchy;
- inspect installed packages and project settings;
- read Console state;
- enter and exit Play Mode;
- discover test and screenshot capabilities.

Use non-destructive inspection for the initial check where possible.

## Tool responsibilities

| Tool | Authorized responsibility |
|---|---|
| Primary coding agent | Orchestration, C#, tests, docs, diagnosis, and handoff |
| Unity MCP | Scenes, GameObjects, prefabs, assets, editor settings, Console, Play Mode, tests, builds, screenshots |
| Filesystem/Git/GitHub | Source and documentation edits, diffs, commits, pushes to `origin`, and repository inspection |
| Blender MCP | Production modeling only after explicit post-gate approval |

The primary agent remains responsible for validating work performed through every tool. A successful tool call is not evidence that Unity compiled or the match worked.

## Source control workflow

- `origin` is `https://github.com/scootsy/wheels`; `main` is the working branch.
- The agent commits coherent milestones/fixes and pushes them so the local project and GitHub stay in sync. The creative director does not need to operate Git.
- Never force-push or rewrite published history.

## Safety and recovery

- Create a clean Git checkpoint before broad editor changes.
- Keep the MCP endpoint local.
- Do not store credentials or absolute user paths in project files.
- Preserve existing project settings unless the task requires a documented change.
- If the MCP connection fails, check the AI Assistant MCP settings in the editor, confirm the client connection was approved, and restart the client or editor.
- Restarting Unity or the MCP client is acceptable. Changing editor versions, render pipelines, or the MCP bridge is not an automatic troubleshooting step; record any such change in `DECISIONS.md`.
- Never use Blender or generated production assets to work around an unproven match loop.
