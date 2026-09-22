# Project Documentation Index

This directory is the persistent source of truth for the Unity tabletop project. `FIRST_TASK.md` is intentionally outside this directory because it is a one-time execution prompt, not permanent project policy.

## Required reading order

| Order | Document | Authority |
|---:|---|---|
| 1 | `../AGENTS.md` | Agent behavior, workflow, Unity requirements, and hard scope gate |
| 2 | `PROJECT_VISION.md` | Product intent and long-term boundaries |
| 3 | `TOOLING.md` | Local Unity MCP, Git, Python/uv, and tool-safety setup |
| 4 | `RULES_SPEC.md` | Deterministic mechanics and rules tests |
| 5 | `MATCH_UX_SPEC.md` | Match screens, controls, presentation, accessibility, and end-to-end acceptance |
| 6 | `TECHNICAL_ARCHITECTURE.md` | Unity packages, assemblies, data flow, Input System, and project structure |
| 7 | `MILESTONES.md` | Authorized implementation sequence and playtest gate |
| 8 | `DECISIONS.md` | Accepted decisions and unresolved choices |
| 9 | `IMPLEMENTATION_STATUS.md` | Current progress, verification, and blockers |
| 10 | `VISUAL_REFERENCE.md` | Research screenshots and spatial interpretation only |

## Change rules

- Mechanics change: update code, deterministic tests, `RULES_SPEC.md`, and `DECISIONS.md` together.
- Interaction change: update PlayMode tests, `MATCH_UX_SPEC.md`, and `DECISIONS.md` together.
- Architecture change: update assembly/package documentation and `DECISIONS.md` before broad migration.
- Milestone completion: update `IMPLEMENTATION_STATUS.md` with test and build evidence.
- A screenshot is never mechanical evidence by itself.

## First execution

After placing this package at the Unity project root, give the implementation agent the contents of `../FIRST_TASK.md`. That prompt authorizes work only through the human playtest gate.
