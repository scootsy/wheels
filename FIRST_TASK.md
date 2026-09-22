# First Task: Build Through the Human Playtest Gate

Work inside the currently open Unity project. Do the work, validate it in Unity, and return a playable result rather than only proposing a plan.

## 1. Read and obey the project sources

Read, in order:

1. `AGENTS.md`
2. every document listed in `Docs/README.md`
3. the visual-reference contact sheets and only the individual screenshots needed to clarify layout

Treat `Docs/RULES_SPEC.md` as authoritative for mechanics and `Docs/MATCH_UX_SPEC.md` as authoritative for screens, controls, presentation, and acceptance. Do not infer rules from screenshots.

Your authorization is to complete technical bootstrap, Milestone M0, and Milestone M1. Continue autonomously between those stages. Do not cross the human playtest gate.

## 2. Preflight the project

1. Inspect the repository and the open Unity editor before changing anything.
2. Confirm the project is pinned to the approved baseline, Unity 6000.5.9f1 (D-010). If it is on another editor version, stop and report the exact mismatch rather than changing it.
3. Confirm Unity MCP can inspect the active project, Console, scenes, packages, tests, Play Mode, and screenshots. If the editor is not reachable, report the exact setup blocker.
4. Preserve all existing work. If this is not a blank project, inventory what exists and integrate without deleting unrelated assets.
5. Verify or install only the required packages:
   - `com.unity.inputsystem`;
   - `com.unity.test-framework`;
   - the Universal Render Pipeline package (`com.unity.render-pipelines.universal`) and anything it requires.
6. Configure Active Input Handling for the Input System package, not the legacy Input Manager. Allow the required editor restart, reconnect, and verify the setting.
7. Ensure Git is initialized, a Unity `.gitignore` is present, and generated folders/builds are excluded. Configure Git LFS patterns for future large binary source assets, but do not place text files in LFS.
8. Update `Docs/IMPLEMENTATION_STATUS.md` with the actual preflight state.

Do not add optional frameworks, dependency-injection containers, tweening libraries, save packages, dialogue systems, or art packages.

## 3. Establish the project structure

Create the folder and assembly structure defined in `Docs/TECHNICAL_ARCHITECTURE.md`. At minimum, isolate:

- deterministic domain rules;
- application/controllers;
- Unity authoring and infrastructure adapters;
- presentation/UI;
- EditMode tests;
- PlayMode tests.

The domain assembly must compile without a `UnityEngine` reference.

Create the versioned Input System asset at:

`Assets/Game/Input/GameInput.inputactions`

It must include these actions:

- UI: `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `ScrollWheel`;
- Match: `Spin`, `LockSlot1`, `LockSlot2`, `LockSlot3`, `LockSlot4`, `LockSlot5`, `FocusNext`, `FocusPrevious`, `Inspect`, `Help`, `AcceleratePresentation`, `Pause`;
- reserved: `Move`, `Interact`.

Create `KeyboardMouse` and `Gamepad` control schemes. Configure the scene EventSystem with `InputSystemUIInputModule`. Gameplay and UI code must not use `Input.GetKey`, `Input.GetButton`, `Input.GetAxis`, `KeyCode`, or other legacy input APIs.

Keyboard number keys may provide direct `LockSlot1` through `LockSlot5` shortcuts. Gamepad players must be able to select any reel with `Navigate` and toggle it with `Submit`, even if direct per-slot gamepad shortcuts are not assigned.

## 4. Implement Milestone M0: deterministic match engine

Implement the generic headless engine required by `Docs/RULES_SPEC.md`, including:

- `MatchState`, side state, five reels, two units per side, commands, immutable events, and snapshots;
- a deterministic seeded PRNG and reproducible draw order;
- exact player reel faces and fifth-reel tiers;
- three spins, locking/unlocking, third-spin finalization, and all-five-locked early finalization;
- symbol evaluation, XP, rank, stored energy, activation, Barrier, damage, healing, delay, bombs, multi-hit attacks, self-damage, and caps;
- the exact twelve-stage resolution sequence;
- win, loss, and tie handling after full-round resolution;
- the implementation decisions for every unresolved rule in Section 15;
- a legal AI-controller interface over the same commands as the human;
- the Standard AI profile and its information boundary;
- replay payload containing rules version, content hash, seed, side configurations, accepted commands, and final state hash.

Implement generic action capabilities even though the first player-facing build exposes only Striker and Caster. Do not expose the other reference units in normal UI.

Create deterministic EditMode tests for every requirement in `Docs/RULES_SPEC.md` Section 17. Add targeted tests for AI information isolation and replay reproduction.

Milestone M0 is not complete until:

- the domain assembly has no `UnityEngine` dependency;
- the entire EditMode suite passes repeatedly;
- identical seed and commands produce identical events and final hashes;
- no simulation rule is owned by a MonoBehaviour, animation, or view.

Do not stop for creative approval after M0. Continue directly to M1 unless blocked by a failed acceptance requirement.

## 5. Implement Milestone M1: ugly complete match

Create `Assets/Game/Scenes/MatchPrototype.unity` using Unity primitives, flat placeholder materials, temporary icons, and readable text. Do not create production models or imitate the reference game's artwork.

Implement the full state flow in `Docs/MATCH_UX_SPEC.md`:

`MATCH_SETUP → UNIT_SELECT → ROUND_READY → SPINNING → SPIN_DECISION → AI_COMMIT → REVEAL → RESOLVING → MATCH_RESULT`

The normal first playable must use:

- human versus Standard AI;
- Copper reels for both sides;
- generated seed;
- Striker and Caster as the only player-facing units;
- distinct A/left and B/right assignments;
- no duplicate unit selection.

The scene must provide:

- opponent composition visible before player confirmation;
- complete unit selection and channel assignment;
- full board state for both sides;
- Crown HP, Barrier height, unit rank, XP, energy/cost, damage, and attack information;
- five player reels with focus and three independent lock cues;
- live non-authoritative symbol/resource preview;
- spins-used and spins-remaining display;
- AI results hidden until both sides commit;
- ordered event presentation driven only by simulation events;
- readable deltas and reasons for every state change;
- acceleration, pause, inspection, help, and reduced-motion behavior;
- win, loss, and tie results;
- rematch, change units, copy replay, and exit;
- developer controls for seed, reel tier, AI profile, and forced acceptance scenarios.

Keyboard/mouse and gamepad must each complete the entire match. Focus may not disappear when the active device changes. All color-coded states also require shape, text, or glyph cues.

Implement PlayMode tests for:

- scene bootstrap and valid initial focus;
- Input System bindings and control-scheme switching;
- complete keyboard match flow;
- complete gamepad match flow using Input System tests;
- reel locking, unlocking, and third-spin finalization;
- all-five-locked early finalization;
- AI reveal boundary;
- presentation acceleration reaching the same final state;
- pause/resume during a reel and during an action event without duplicate application;
- win, loss, tie, rematch, and replay-copy paths;
- recoverable command rejection without state mutation or lost focus.

## 6. Validate the first playable

Complete every acceptance scenario in `Docs/MATCH_UX_SPEC.md` Section 14 and every item in its Section 15 completion gate.

Before handoff:

1. Run the full EditMode and PlayMode suites and record totals.
2. Enter Play Mode from a clean editor state and complete at least one human-versus-AI match.
3. Confirm there are no compiler errors, exceptions, missing references, or new unresolved Console warnings.
4. Replay one captured seed and command log and verify the final state hash.
5. Verify keyboard/mouse and gamepad separately.
6. Capture screenshots of setup/selection, spin decision with mixed locks, resolution, and match result.
7. Create a Windows x86-64 development build under `Builds/Windows/` if the installed Unity modules permit it. Do not commit build output. If the module is absent, report that clearly; do not change editor versions.
8. Update `Docs/IMPLEMENTATION_STATUS.md` and record any new implementation decision in `Docs/DECISIONS.md`.

## 7. Stop at the gate

Once the match meets the completion gate, stop. Do not begin progression, rewards, a tavern, exploration, dialogue, more units, production art, Blender work, save/load, or general expansion.

Return a handoff containing:

- a plain-language description of what is playable;
- exact launch and control instructions;
- test counts and results;
- Console and build status;
- build path, if produced;
- links/paths to the four required screenshots;
- replay seed used for verification;
- known defects and deliberately deferred work;
- this exact status line: **HUMAN PLAYTEST GATE ACTIVE — awaiting creative-director approval.**

