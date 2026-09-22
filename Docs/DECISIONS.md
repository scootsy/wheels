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

### D-013: Per-side reel draw streams

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision)
- **Decision:** Each reel face is drawn from a stateless SplitMix64 hash of (seed, side, round, spin number, reel index). There is no shared draw counter.
- **Reason:** Reproducible from seed and "draw index" as RULES_SPEC 5.4 requires, and the human's rerolls cannot change the AI's faces (or vice versa), which keeps the AI boundary and replays easy to reason about.
- **Files updated:** `RULES_SPEC.md` 15.1 (IMPL-01)

### D-014: Panel XP is one grant per unit per round

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision)
- **Decision:** A unit's matching XP faces are summed into one grant, so overflow past 6 is discarded (the RULES_SPEC 7.2 "5/6 + 3 XP = next rank at 0/6" example can only arise this way).
- **Files updated:** `RULES_SPEC.md` 15.1 (IMPL-02)

### D-015: UI scale grows text within fixed boxes

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision)
- **Decision:** The 100% / 125% / 150% UI scale setting enlarges text using best-fit inside the fixed 1920x1080 layout rather than enlarging panels, so nothing overlaps or leaves the safe area at 1280x720.
- **Reason:** MATCH_UX_SPEC 8 requires both scaling and no overlap at 1280x720/150%.

### D-016: Finalize flow

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision)
- **Decision:** The simulation finalizes automatically on the third spin. `FinalizeSpin` is legal only when all five reels are locked; the match controller sends it right after the fifth lock is accepted, so both appear in the replay command log. The normal UI has no separate finalize control.
- **Files updated:** `RULES_SPEC.md` 15.1 (IMPL-03)

### D-017: Remove the unused AI inference package

- **Date:** 2026-09-22
- **Status:** Accepted (agent, under D-012)
- **Decision:** Removed `com.unity.ai.inference` from the manifest. Nothing depends on it (the AI Assistant MCP bridge does not), and it added DirectML binaries, ~30 MB, and 485 shader warnings to every player build.

### D-018: Placeholder names

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision; creative director may rename)
- **Decision:** Player-facing units are Striker and Caster. Developer-only definitions use original placeholder names: Ranger (archer values), Mason (engineer), Shade (assassin), Mender (priest), Hexer (warlock). The prototype opponent is labelled "The Tinkerer". The game is titled "Tabletop Reels" on the setup screen.

### D-019: AI profiles

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision)
- **Decision:** Standard AI scores each possible lock mask by its exact expected RULES_SPEC 14.2 utility over every reroll outcome (integer math, no randomness). Expert uses the same search with heavier lethal/threat/waste weights. Learner locks whatever feeds its most-populated channel. All profiles use the same legal commands and reels; the AI only sees the public round-start state and its own rolls.

### D-020: Placeholder presentation technology

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision)
- **Decision:** M1 UI is built in code with uGUI (built-in font, no prefabs or imported art) over a small URP scene of Unity primitives. The scene contains only a camera pair, a light, the EventSystem (InputSystemUIInputModule), and the `MatchApp` composition root.
- **Reason:** Fast to iterate and test; nothing here is intended as production presentation.

### D-021: Default bindings beyond the UX table

- **Date:** 2026-09-22
- **Status:** Accepted (implementation decision)
- **Decision:** `Spin` = R (keyboard) / North button (gamepad); `LockSlot1-5` = number keys 1-5; accelerate = hold Space, South button, or right trigger. Gamepad players lock reels with Navigate + Submit. All bindings live only in `GameInput.inputactions`.

### D-022: Git LFS policy

- **Date:** 2026-09-22
- **Status:** Accepted (agent, under D-012)
- **Decision:** `.gitattributes` routes future large binary source assets (models, Blender/PSD files, audio, HDR/EXR/TGA/TIF textures, video) to Git LFS. PNG/JPG stay in normal Git so existing reference images and review screenshots remain readable on GitHub.

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

