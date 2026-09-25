# Milestones and Approval Gates

Milestones are sequential. Later work may not begin merely because it is technically convenient.

## Technical bootstrap

**Goal:** Establish a safe, inspectable Unity project.

Required:

- Unity 6000.5.9f1 project verified (D-010);
- Unity MCP connection verified;
- Git, Unity `.gitignore`, and Git LFS policy established;
- required packages installed;
- Input System active and legacy input prohibited;
- assemblies, folders, documentation, and test structure present.

This stage does not need a creative review unless a blocker changes product direction.

## M0: Deterministic rules engine

**Goal:** Prove the game exists as rules before building spectacle.

Deliverables:

- headless pure-C# match simulation;
- complete commands, state, events, seeded randomness, content validation, AI boundary, and replay capture;
- generic engine capability required by `RULES_SPEC.md` Section 13.1;
- deterministic EditMode tests for `RULES_SPEC.md` Section 17;
- no Unity scene or animation dependency in the domain.

Exit criteria:

- all EditMode tests pass repeatedly;
- identical inputs reproduce identical events and final hash;
- unresolved behaviors match recorded implementation decisions;
- no rule is hidden in presentation code.

Proceed directly to M1 when these criteria pass.

## M1: Ugly complete playable match

**Goal:** Let the creative director judge the actual game loop before further investment.

Deliverables:

- one complete human-versus-AI match;
- Striker and Caster only in normal player-facing selection;
- Copper reels by default;
- complete setup, selection, spin, lock, reveal, resolution, result, rematch, and replay flow;
- placeholder primitives and readable UI;
- keyboard/mouse and gamepad support through Unity Input System;
- pause, help, inspection, acceleration, reduced motion, and non-color cues;
- PlayMode tests and required screenshots;
- editor-playable result and Windows build when available.

Exit criteria are every item in `MATCH_UX_SPEC.md` Sections 14 and 15.

# HUMAN PLAYTEST GATE

When M1 passes, stop all expansion. The creative director must personally play the build and explicitly approve continuation.

Permitted while waiting:

- fix defects that prevent or materially distort the playtest;
- improve unreadable placeholder UI;
- repair controls, tests, build, or replay capture;
- answer questions and collect feedback.

Prohibited while waiting:

- M2, M3, or M4 work;
- more player-facing content;
- progression or save systems;
- world, dialogue, or quest development;
- production art, audio, or Blender work;
- speculative scale architecture.

## M2: Collection and progression

**Status:** Not authorized until explicit gate approval.

Candidate scope after approval:

- collectible original pieces;
- reel or component upgrades;
- encounter rewards and progression pacing;
- persistent collection data and save/load;
- a small opponent roster and balance telemetry.

Define M2 from actual M1 playtest findings. Do not treat these bullets as approved requirements.

## W1: World slice (approved 2026-09-24, D-025)

**Status:** Authorized by explicit creative-director instruction. Scope is exactly `WORLD_SPEC.md`: starting village, road north, second village with opponents, champion's hall, talk/challenge/return. No saving, rewards, or extra player units.

Exit: EditMode and PlayMode suites pass (including walking, talking, challenging, winning and returning, and sitting at the champion's table), the Windows build passes `Tools/selfcheck_build.sh` with inspected screenshots. Then **stop and wait for the creative director to play W1**.

## W2: Journey features and the production table (approved 2026-09-25, D-027)

**Status:** Authorized. Save/resume, name fade, first person, sprint/jump, piece progression with the deck screen, and the 3D table in the source's art style with animated pieces.

## M3: Tiny world wrapper

**Status:** Not authorized beyond W1.

Candidate vertical slice:

- one compact tavern-like location;
- fixed-camera movement and interaction;
- approximately five distinct opponents;
- short dialogue and match entry/exit;
- collection/progression presented in-world.

## M4: Original production presentation

**Status:** Not authorized.

Candidate direction:

- low-poly fixed-camera environment;
- original static miniature pieces;
- original table, symbols, effects, audio, and UI skin;
- accessibility and performance polish;
- Blender MCP asset pipeline after visual prototypes are approved.

## M5: Release preparation

**Status:** Not authorized.

Platform packaging, localization production, achievements, external playtesting, store material, and release QA are intentionally deferred.

