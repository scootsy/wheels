# Development Tooling

## Required local stack

- Unity Hub;
- Unity 6.3 LTS editor with Windows build support;
- Git available on `PATH`;
- Git LFS;
- Python 3.10 or newer;
- `uv`/`uvx`;
- an MCP-capable primary coding agent such as Codex;
- MCP for Unity connected to the open editor.

Blender and Blender MCP are not required before the human playtest gate. Unity primitives are the authorized M1 art source.

## Unity MCP bridge

The approved development bridge is the open-source CoplayDev MCP for Unity package. It is a development tool, not shipped gameplay code.

Install through Unity Package Manager using:

```text
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main
```

For a long-lived project, record the installed release or commit in `Packages/manifest.json`/`packages-lock.json` rather than allowing unreviewed upgrades.

Current setup path:

1. Open the actual project in Unity Editor, not only Unity Hub.
2. Open **Window → Package Manager**.
3. Choose **Add package from git URL** and enter the URL above.
4. Open **Window → MCP for Unity**.
5. Confirm Python and `uv` dependencies are green.
6. Configure the detected Codex or other MCP client.
7. Keep the Unity project open while the coding agent works.

The default local HTTP endpoint is:

```text
http://localhost:8080/mcp
```

Client configuration is machine-local and must not be committed. Do not bind the MCP server to a LAN or public interface for this project.

## Connection verification

Before implementation, the primary agent must successfully:

- read the active Unity editor/project identity;
- inspect the current scene hierarchy;
- inspect installed packages and project settings;
- read Console state;
- enter and exit Play Mode;
- discover test and screenshot capabilities.

Use non-destructive inspection for the initial check. A generated object is not necessary merely to prove connection.

## Tool responsibilities

| Tool | Authorized responsibility |
|---|---|
| Primary coding agent | Orchestration, C#, tests, docs, diagnosis, and handoff |
| Unity MCP | Scenes, GameObjects, prefabs, assets, editor settings, Console, Play Mode, tests, builds, screenshots |
| Filesystem/Git | Source and documentation edits, diffs, commits, and repository inspection |
| Blender MCP | Production modeling only after explicit post-gate approval |

The primary agent remains responsible for validating work performed through every tool. A successful tool call is not evidence that Unity compiled or the match worked.

## Safety and recovery

- Create a clean Git checkpoint before broad editor changes.
- Keep the MCP endpoint local.
- Do not store credentials or absolute user paths in project files.
- Preserve existing project settings unless the task requires a documented change.
- If the MCP connection fails, inspect **Window → MCP for Unity**, confirm the server is running, confirm `uv --version`, and verify the client's endpoint.
- Restarting Unity or the MCP client is acceptable. Upgrading Unity, changing render pipelines, or replacing the MCP bridge is not an automatic troubleshooting step.
- Never use Blender or generated production assets to work around an unproven match loop.

