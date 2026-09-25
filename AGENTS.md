# Project Agent Instructions

## Mission

Build a small, original Unity game under the direction of a nontechnical creative director. The immediate product is a deterministic, fully playable tabletop match inspired by the appeal of in-world minigames, not a recreation of any existing game's art, characters, setting, dialogue, or interface.

Make routine, reversible engineering decisions independently. Explain results in plain language and tell the creative director exactly how to run and test the work. Ask only when a missing creative decision would materially change the player experience, when permission is required, or when a blocker cannot be resolved safely.

## Read before acting

Read these files before making project changes:

1. `AGENTS.md`
2. `Docs/README.md`
3. `Docs/PROJECT_VISION.md`
4. `Docs/TOOLING.md`
5. `Docs/RULES_SPEC.md`
6. `Docs/MATCH_UX_SPEC.md`
7. `Docs/TECHNICAL_ARCHITECTURE.md`
8. `Docs/MILESTONES.md`
9. `Docs/DECISIONS.md`
10. `Docs/IMPLEMENTATION_STATUS.md`

`Docs/VISUAL_REFERENCE.md` and its images are reference material for spatial comprehension. They do not override rules or authorize reuse of the original art.

If two documents conflict, do not silently choose one. Apply this order of authority:

1. the creative director's latest explicit instruction;
2. the human playtest gate in this file and `Docs/MILESTONES.md`;
3. `Docs/RULES_SPEC.md` for simulation behavior;
4. `Docs/MATCH_UX_SPEC.md` for interaction and presentation;
5. `Docs/TECHNICAL_ARCHITECTURE.md` for implementation boundaries;
6. `Docs/PROJECT_VISION.md` and `Docs/MILESTONES.md` for scope and direction.

Record a resolved conflict in `Docs/DECISIONS.md` and update every affected source-of-truth document in the same change.

## Human playtest gate

The current authorized scope ends when Milestone M1, the ugly but complete playable match, satisfies its acceptance criteria.

At that point, **STOP AND WAIT FOR THE CREATIVE DIRECTOR TO PLAY IT**.

**Gate status (2026-09-24, D-025):** the creative director explicitly approved one world slice, W1, defined in `Docs/WORLD_SPEC.md`: two villages, the road north, opponents to talk to and challenge, and the champion's hall. W1 work is authorized. Everything else in the list below remains gated. When W1 is verified, stop again and wait for the creative director to play it.

**Imported art (2026-09-25, D-026):** creative-director-supplied models may replace W1 placeholders through `Docs/ART_WORKFLOW.md`. Keep the project original: no recognizable existing characters, nothing sexual, nothing that reads as underage in a revealing outfit. Producing new art (Blender, VFX, audio) remains gated.

Do not begin any of the following without an explicit instruction approving continuation past the human playtest gate:

- collection or meta-progression;
- unlocks, rewards, currency, shops, or save data;
- a tavern, overworld, exploration, dialogue, quests, or NPC schedules;
- additional player-facing units beyond Striker and Caster;
- additional player-facing opponents or encounters;
- production models, textures, VFX, music, voice, or final sound design;
- Blender asset production;
- online multiplayer, achievements, localization production, or platform services;
- broad refactors intended only for hypothetical future scale.

Before approval, you may fix crashes, compiler errors, broken controls, deterministic-rule defects, unreadable UI, failed tests, build failures, or other problems that prevent a fair playtest. You may not disguise post-gate feature work as a fix.

Approval must be explicit. A successful test run, a positive comment, or silence is not approval.

## Unity requirements

- Use the Unity editor version pinned by `ProjectSettings/ProjectVersion.txt`. The approved baseline is **Unity 6000.5.9f1** (see `Docs/DECISIONS.md` D-010). Do not change editor versions casually; version and tooling choices are delegated to the agent (D-012) but must be recorded in `Docs/DECISIONS.md` and every affected document.
- Use Universal Render Pipeline for the eventual fixed-camera, stylized low-poly presentation. The M1 build uses primitives and placeholder materials.
- Use `com.unity.test-framework` for EditMode and PlayMode tests.
- Use `com.unity.inputsystem`. The legacy Input Manager is prohibited.
- Do not use `Input.GetKey`, `Input.GetButton`, `Input.GetAxis`, `KeyCode`, or direct device polling in gameplay code.
- Use an `InputActionAsset`, action maps, control schemes, and `InputSystemUIInputModule`.
- Support keyboard/mouse and gamepad from the first playable. Pointer support must not compromise keyboard or controller focus.
- Keep Unity scenes, prefabs, assets, and their `.meta` files together. Never regenerate or discard stable GUIDs without cause.
- Do not edit serialized scene or prefab YAML by hand when the Unity editor or Unity MCP can make and validate the change.

### Required input actions

The versioned input-actions asset must provide:

- UI: `Navigate`, `Submit`, `Cancel`, `Point`, `Click`, `ScrollWheel`;
- Match: `Spin`, `LockSlot1`, `LockSlot2`, `LockSlot3`, `LockSlot4`, `LockSlot5`, `FocusNext`, `FocusPrevious`, `Inspect`, `Help`, `AcceleratePresentation`, `Pause`;
- Reserved for the later world milestone: `Move`, `Interact`.

`Move` and `Interact` are consumed by the approved W1 world slice (D-025). Do not extend world gameplay beyond `Docs/WORLD_SPEC.md` merely because the actions exist.

Input code must consume abstract actions. Device-specific bindings belong in the input-actions asset, not rules or presentation code. Automated input tests should use Input System test facilities rather than faking legacy key state.

## Architecture rules

- The match simulation is deterministic, headless-first, and independent of `UnityEngine`.
- The simulation owns rules state. Views and animations never change HP, Barrier, reels, locks, energy, XP, rank, RNG state, phase, or winner directly.
- All randomness comes from the match's seeded PRNG. Never use `UnityEngine.Random` in the simulation or AI.
- Commands enter through one match-controller boundary. Accepted commands produce immutable events and authoritative snapshots.
- Presentation consumes events in order. Animation timing, skipped animation, frame rate, and input device must not change the result.
- The AI is a legal controller using the same command surface and reel definitions as a human. Difficulty may change decision quality, never hidden roll odds.
- Authoring data may use ScriptableObjects, but the Unity adapter must compile or map it into immutable domain definitions before a match begins.
- Prefer small, explicit components and assembly definitions over global managers and hidden scene dependencies.
- Avoid speculative abstractions. Generalize only where the current rule set or an approved next milestone requires it.
- Do not add third-party packages without a specific need, license review, and approval.

## Tool use

- Use Unity MCP for editor state, GameObjects, scenes, prefabs, package/settings verification, Play Mode, Console inspection, tests, builds, and screenshots.
- Use filesystem tools for C# source, tests, Markdown, JSON, and other text assets.
- Blender MCP is reserved for approved art work after the human playtest gate.
- Inspect existing assets and project state before editing. Preserve unrelated work.
- After each coherent change, allow Unity to compile and inspect the Console. Do not stack further changes on an unknown compiler state.
- Run the narrowest relevant tests during iteration and the full EditMode and PlayMode suites before reporting completion.
- A scene that opens is not proof that the feature works. Enter Play Mode and exercise the actual path.
- Capture screenshots at required UX states so the creative director can judge the result without inspecting code.
- If Unity MCP is unavailable, do not guess at editor-only changes or hand-edit opaque scene data. Report the exact connection blocker and continue only with work that can be validated safely.

## Source control

- Use Git from the beginning. Preserve any existing repository and history.
- Use a Unity-appropriate `.gitignore`. Never commit `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, or build output.
- Configure Git LFS for large binary source assets when those assets are introduced. Do not place ordinary C#, Markdown, JSON, YAML, or small UI files in LFS.
- Keep commits scoped to coherent milestones or fixes. Do not rewrite published history, force-push, or discard the user's changes.
- Do not commit secrets, local MCP configuration, machine-specific absolute paths, or generated credentials.

## Testing and quality

- Translate every applicable requirement in `Docs/RULES_SPEC.md` Section 17 into deterministic EditMode tests.
- Use PlayMode tests for scene wiring, Input System behavior, focus, state transitions, animation skipping, and result/rematch flow.
- A bug fix requires a regression test when the failure is deterministic and testable.
- Same rules version, content hash, seed, and accepted command log must reproduce the same event log and final state.
- No known simulation/presenter desynchronization may remain at the playtest gate.
- Do not weaken or delete a test merely to make a build pass. Correct the implementation or document an approved rule change.

## Documentation discipline

- `Docs/RULES_SPEC.md` is the source of truth for mechanics.
- `Docs/MATCH_UX_SPEC.md` is the source of truth for the playable interaction.
- Record assumptions, implementation decisions, and approved changes in `Docs/DECISIONS.md`.
- Update `Docs/IMPLEMENTATION_STATUS.md` after each milestone or material blocker.
- If a rule changes, update code, deterministic tests, `RULES_SPEC.md`, and `DECISIONS.md` together.
- Mark unknown reference behavior as `OPEN` or an explicit `IMPLEMENTATION DECISION`. Never quietly present a guess as a recovered rule.

## Completion reports

When handing work back, state:

- what is now playable;
- what changed at a player-visible level;
- tests run and their results;
- Console/build status;
- exact steps and controls for testing it;
- build location, if created;
- screenshots captured;
- known defects or unresolved decisions;
- current milestone and whether the human playtest gate is active.

Do not bury a blocker or failed test inside a long status report.
