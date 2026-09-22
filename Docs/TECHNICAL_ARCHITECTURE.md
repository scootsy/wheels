# Technical Architecture

## 1. Baseline

| Area | Decision |
|---|---|
| Engine | Unity 6000.5.9f1, pinned in `ProjectVersion.txt` (D-010) |
| Render pipeline | Universal Render Pipeline, 3D project baseline |
| Initial target | Windows x86-64 and Unity Editor |
| Input | `com.unity.inputsystem`; legacy Input Manager prohibited |
| Tests | Unity Test Framework, EditMode and PlayMode |
| Version control | Git from project creation; Git LFS for large binary source assets |
| Match model | Pure deterministic C# without `UnityEngine` dependency |
| Authoring | ScriptableObjects mapped into validated immutable domain definitions |
| Presentation | Fixed-camera 3D board with 2D UI; primitives before the playtest gate |

Do not silently change the editor stream, render pipeline, input backend, or build target.

## 2. Project structure

```text
Assets/
  Game/
    Domain/
      State/
      Commands/
      Events/
      Rules/
      Random/
      AI/
      Replay/
    Application/
      Controllers/
      ViewModels/
      Validation/
    Infrastructure/
      UnityAuthoring/
      Serialization/
      Logging/
    Presentation/
      Board/
      UI/
      Animation/
      Accessibility/
    Input/
      GameInput.inputactions
      Adapters/
    Content/
      Units/
      Reels/
      Opponents/
    Scenes/
      MatchPrototype.unity
    Tests/
      EditMode/
      PlayMode/
Docs/
Packages/
ProjectSettings/
```

Do not create `World`, `Dialogue`, `Inventory`, `Save`, or production-art systems before gate approval.

## 3. Assembly boundaries

Use assembly definitions to enforce direction rather than relying on convention.

```text
Tabletop.Domain
  dependencies: standard C# only

Tabletop.Application
  dependencies: Tabletop.Domain

Tabletop.Infrastructure.Unity
  dependencies: Tabletop.Domain, Tabletop.Application, Unity packages

Tabletop.Presentation
  dependencies: Tabletop.Domain read models, Tabletop.Application, Unity UI/Input

Tabletop.Tests.EditMode
  dependencies: Tabletop.Domain, Tabletop.Application, Unity Test Framework

Tabletop.Tests.PlayMode
  dependencies: all runtime assemblies, Unity Test Framework, Input System test support
```

`Tabletop.Domain` must not reference `UnityEngine`, MonoBehaviours, scenes, ScriptableObjects, animation, audio, time, frame count, or device input.

### 3.1 As built (M1)

| Assembly | Folder | Engine references | Notes |
|---|---|---|---|
| `Tabletop.Domain` | `Assets/Game/Domain` | none (`noEngineReferences`) | Rules, state, commands, events, RNG, AI, replay |
| `Tabletop.Application` | `Assets/Game/Application` | none (`noEngineReferences`) | `MatchSession` UX state machine, unit selection, preview, narration, dev scenarios |
| `Tabletop.Infrastructure.Unity` | `Assets/Game/Infrastructure` | Unity | `ContentCatalogAsset` ScriptableObject mapped to domain definitions |
| `Tabletop.Input` | `Assets/Game/Input/Adapters` | Unity, Input System | `GameInputRouter`: actions -> abstract intents, active control scheme |
| `Tabletop.Presentation` | `Assets/Game/Presentation` | Unity, uGUI, Input System | `MatchApp` composition root, presenter, views, primitive diorama |
| `Tabletop.Editor` | `Assets/Game/Editor` | Editor only | Menu: content/URP/scene setup, test runner summary, Windows build |
| `Tabletop.Tests.EditMode` | `Assets/Game/Tests/EditMode` | Editor only | Rules, AI, replay, content, application tests |
| `Tabletop.Tests.PlayMode` | `Assets/Game/Tests/PlayMode` | Input System test framework | Scene, input, flow, presentation, screenshots |

Content authoring is a single `Assets/Game/Content/ContentCatalog.asset` (units and reel sets). `Tabletop/Setup/1. Create or Refresh Content Catalog` regenerates it from `ReferenceContent`; an EditMode test proves it maps to the same content hash.

## 4. Runtime data flow

```text
Device
  -> Input System action
  -> Input adapter
  -> UX/match controller
  -> validated domain command
  -> deterministic simulation
  -> authoritative state + ordered immutable events
  -> read model and event presenter
  -> board/UI/audio
```

The only route into match state is a validated command. The only route out is a snapshot, read model, event list, rejection, or replay record.

The presenter may visually trail the already-computed authoritative result while animating, but it must reconcile at each event boundary. Skipping presentation consumes the same events in the same order and applies their final visual values immediately.

## 5. Deterministic simulation

The domain contains:

- rules-versioned immutable definitions;
- `MatchState` and nested side/reel/unit states;
- explicit match phases;
- commands defined in `RULES_SPEC.md`;
- structured command rejections;
- ordered events defined in `RULES_SPEC.md` and `MATCH_UX_SPEC.md`;
- seeded PRNG state or deterministic draw index;
- final-state hashing;
- content hashing;
- replay serialization.

Do not use floating-point calculations where integer state is sufficient. Do not depend on dictionary iteration order, system time, frame rate, locale, or platform-specific hash implementations.

The simulation receives definitions and commands as data. It does not load assets, inspect scene objects, or call Unity services.

## 6. Authoring and content

ScriptableObjects are authoring adapters, not the rules engine.

At match startup:

1. load selected Unity authoring assets;
2. map them into domain definitions;
3. validate IDs, ranks, actions, reel faces, counts, and constraints;
4. calculate a stable content hash;
5. refuse to start on invalid content;
6. pass only immutable domain data to the simulation.

The first player-facing content is:

- Striker using the reference Warrior values;
- Caster using the reference Mage values;
- Copper fifth reel;
- Standard AI;
- two units per side and five eight-face reels.

The domain may implement the generic action types required by the full rules spec. Unapproved units remain developer-only.

## 7. Unity Input System

The project uses one versioned asset at `Assets/Game/Input/GameInput.inputactions`.

### Action maps

| Map | Required actions |
|---|---|
| UI | `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `ScrollWheel` |
| Match | `Spin`, `LockSlot1`–`LockSlot5`, `FocusNext`, `FocusPrevious`, `Inspect`, `Help`, `AcceleratePresentation`, `Pause` |
| WorldReserved | `Move`, `Interact` |

### Control schemes

- `KeyboardMouse`
- `Gamepad`

The EventSystem uses `InputSystemUIInputModule`. UI focus is authoritative for controller navigation. Direct keyboard lock actions are optional conveniences; focus plus Submit must always provide the same function.

Enable only the maps appropriate to the current UX state. Avoid one action performing two operations in the same state. If the same physical control is reused across states, map switching determines its meaning.

Never use legacy input APIs as a fallback. If a package or sample requires them, replace or isolate it rather than switching Active Input Handling to `Both` without approval.

## 8. AI

AI implements the same controller interface and dispatches the same commands as the human.

- It receives only the public round-start snapshot and its own subsequent reel results.
- It cannot inspect the human's current-round faces, locks, or pending result.
- Search and rollout randomness uses a derived deterministic seed and fixed budget.
- Difficulty changes evaluation quality, not legal commands or face probabilities.
- Presentation delay never enters AI logic.

## 9. Scenes and lifecycle

The pre-gate project needs one scene: `MatchPrototype.unity`.

It contains composition roots and Unity adapters, not authoritative rule state spread across unrelated objects. Scene reload, rematch, and test setup must dispose old controllers and input subscriptions cleanly.

Use explicit serialized references or validated discovery at the composition root. Avoid global `FindObjectOfType` dependencies and persistent singleton state that survives tests unpredictably.

## 10. Testing strategy

### EditMode

- exact reel definitions and seeded draws;
- locks and spin limits;
- resource formulas;
- energy, XP, ranks, bombs, Barrier, projectiles, and all unit effect types;
- resolution priority and zero-HP continuation;
- ties and Priest rescue cases;
- open-question implementation decisions;
- AI legality and information isolation;
- replay reproduction and stable state hash;
- content validation.

### PlayMode

- scene wiring and clean startup;
- Input System actions and control-scheme switching;
- keyboard-only and gamepad-only match completion;
- focus, overlays, pause, help, and rejected-command feedback;
- spin/lock/finalize UX;
- reveal and event presentation order;
- skip/reduced motion reaching the same state;
- result, rematch, and replay copy;
- no double subscriptions or duplicated event application after reload.

Tests should call domain APIs directly when testing rules and real Input System actions when testing controls.

## 11. Logging and diagnostics

Development builds record:

- rules version and content hash;
- seed and accepted commands;
- command rejections;
- event IDs and state hashes at event boundaries;
- presenter reconciliation failures;
- AI profile and fixed evaluation budget;
- test/build version.

Never log secrets or machine-specific configuration. Replay output must be compact and safe to copy.

## 12. Performance and build

The first playable targets 60 frames per second at 1920×1080 on the agreed baseline PC. Deterministic simulation should be fast enough to run large EditMode batches without frames or coroutines.

Build output belongs under `Builds/` and is never committed. The first handoff should attempt a Windows x86-64 development build when the platform module is installed.

