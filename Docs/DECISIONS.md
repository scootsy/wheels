# Decision Log

This log records decisions that affect rules, player experience, architecture, scope, or production. It is not a substitute for updating the authoritative specification.

## Accepted decisions

### D-001: Unity baseline

- **Date:** 2026-09-22
- **Status:** Superseded by D-010 (editor version only; URP and Windows x86-64 still apply)
- **Decision:** Use Unity 6.3 LTS with Universal Render Pipeline and Windows x86-64 as the first standalone target.
- **Reason:** Stable long-lived editor baseline and a suitable path to fixed-camera stylized low-poly 3D with crisp 2D UI.

### D-002: Unity Input System from the first implementation

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** Use `com.unity.inputsystem`, action maps, control schemes, and `InputSystemUIInputModule`. Prohibit legacy `Input` APIs and direct keycode polling.
- **Reason:** Keyboard/mouse and controller must be first-class rather than retrofitted after gameplay code hardens.

### D-003: Deterministic headless domain

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** Match rules compile without `UnityEngine`. All randomness is seeded and replayable. Unity adapts input, authoring data, and presentation around the domain.
- **Reason:** Reliable tests, reproducible bugs, clean AI, replay capture, and protection against animation becoming hidden game logic.

### D-004: Data-driven Unity authoring with immutable runtime definitions

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** Use ScriptableObjects for content authoring, validate and map them into immutable domain definitions before a match.
- **Reason:** Designer-friendly Unity workflow without coupling the rules engine to Unity assets.

### D-005: Original intellectual property

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** Reference games inform mechanics research and visual comprehension only. The shipped project uses original names, art, characters, setting, dialogue, icons, effects, audio, and UI.
- **Reason:** The goal is an original game, not a clone or fan recreation.

### D-006: First playable before progression, world, or art

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** Complete the deterministic engine and ugly playable match, then stop at a mandatory human playtest gate.
- **Reason:** The core match must earn further investment.

### D-007: Fair AI command surface

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** AI uses the same legal commands and reel definitions as the human. It cannot inspect the human's current-round faces or locks. Difficulty changes evaluation, not luck.
- **Reason:** Maintain understandable fairness and deterministic diagnosis.

### D-008: First player-facing content

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** Normal M1 play exposes only Striker and Caster, Copper reels, and Standard AI. Generic engine capabilities and additional definitions may remain developer-only.
- **Reason:** Enough content to evaluate the loop without turning the prototype into a content-production project.

### D-009: No persistence before the playtest gate

- **Date:** 2026-09-22
- **Status:** Accepted
- **Decision:** No collection save, campaign save, or mid-match resume before M1 approval. Replay capture is diagnostic data, not a save system.
- **Reason:** Persistence would serve unapproved progression scope and create migration work before the loop is validated.

### D-010: Editor baseline is Unity 6000.5.9f1

- **Date:** 2026-09-22
- **Status:** Accepted (creative director)
- **Decision:** Pin the project to Unity 6000.5.9f1, the editor installed on the development machine. Universal Render Pipeline and Windows x86-64 from D-001 remain in force.
- **Reason:** The originally assumed 6.3 LTS editor was not installed; the project had already been opened and upgraded in 6000.5.9f1 while still empty.
- **Consequences:** Shorter support window than an LTS stream. Future version changes are an agent decision under D-012 but must be recorded here.
- **Supersedes:** D-001 (editor version)
- **Files updated:** `AGENTS.md`, `FIRST_TASK.md`, `Docs/TOOLING.md`, `Docs/TECHNICAL_ARCHITECTURE.md`, `Docs/MILESTONES.md`, `Docs/IMPLEMENTATION_STATUS.md`

### D-011: Unity MCP bridge is Unity AI Assistant

- **Date:** 2026-09-22
- **Status:** Accepted (creative director)
- **Decision:** Use Unity's `com.unity.ai.assistant` package as the editor MCP bridge instead of the CoplayDev package originally listed. Commit the package reference; keep client configuration local.
- **Reason:** Already installed and verified working (editor state, scenes, packages, Console, GameObject operations).
- **Files updated:** `Docs/TOOLING.md`, `Packages/manifest.json`

### D-012: Technical tooling choices are delegated to the agent

- **Date:** 2026-09-22
- **Status:** Accepted (creative director)
- **Decision:** Editor versions, packages, tooling, and Git/GitHub mechanics are engineering decisions for the agent. The agent chooses what is best for the project, records the choice here, updates affected documents, and does not ask the creative director. The agent keeps the local repository and GitHub `origin/main` in sync.
- **Reason:** The creative director is nontechnical and the original tooling assumptions were placeholders.
- **Consequences:** Creative/player-experience decisions and the human playtest gate still require the creative director.

## Open decisions

The rules-level open questions and temporary prototype behaviors are maintained in `RULES_SPEC.md` Section 15. They do not block M0 or M1.

No additional creative decision is currently required to begin implementation.

## Entry template

```text
### D-XXX: Short title

- Date:
- Status: Proposed | Accepted | Superseded
- Decision:
- Reason:
- Consequences:
- Supersedes:
- Files updated:
```

