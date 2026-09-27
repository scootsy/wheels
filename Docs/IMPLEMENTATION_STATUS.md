# Implementation Status

**Current authorized target:** W3 (D-033, `WORLD_SPEC.md`): coins, stakes and favours, errands, stalls, charms and wheels, pack art and sound, the source's names, and the Stonemasons' Outpost. Then stop for the creative director to play it.  
**Last updated:** 2026-09-26  

**W3 COMPLETE - waiting for the creative director to play it.** Nothing beyond `WORLD_SPEC.md` is authorized.

| Stage | Status | Verification |
|---|---|---|
| Technical bootstrap | Complete | Unity 6000.5.9f1 (D-010), MCP bridge (D-011), URP, Input System only, assemblies, Git/LFS |
| M0 deterministic engine | Complete | 92/92 EditMode tests, passed on 3 consecutive runs; replay reproduction verified |
| M1 ugly playable match | Complete | 21/21 PlayMode tests; keyboard, gamepad, and mouse paths; screenshots; Windows build |
| Human playtest gate | Opened for W1 only (D-025) | Creative director asked for the explorable world on 2026-09-24 |
| W1 world slice | Complete | 97/97 EditMode, 27/27 PlayMode; Windows build self-check with world screenshots |
| W1 playtest stop | Passed | Creative director played W1 and asked for W2 / W3 |
| W2 journey + table (D-027..D-032) | Complete | Save/continue, first person, deck, 3D table, iOS builds, touch pad |
| W3 journey economy, look, sound, Outpost (D-033) | Complete | 136/136 EditMode, 40/40 PlayMode; Windows build self-check (world + table screenshots, 20/20 animated people, no unsupported shaders) |
| W3 playtest stop | **ACTIVE** | Waiting for the creative director to play W3 |
| M2 progression | Not authorized | Untouched |
| M3 tiny world (beyond W1) | Not authorized | Untouched |
| M4 production presentation | Not authorized | Untouched |

## How to play (creative director)

**Fastest:** double-click `Builds/Windows/TabletopReels.exe` (Windows development build; not committed to Git, rebuild with **Tabletop → Build → Windows x64 Development Build**). It opens in the world: see `WORLD_SPEC.md` for places, opponents, and world controls (walk WASD/arrows/left stick, talk E/A, menu Esc/Start). The table below is for the match itself.

**Published builds (D-030):** every published test build is on the GitHub repo's **Releases** page (Windows zip + iOS Xcode zip). To make one: **Tabletop → Build → All (Windows + iOS)**, commit, then `bash Tools/publish_builds.sh "what changed"`.

**iPhone / iPad (test builds, D-029):** in Unity choose **Tabletop → Build → iOS Xcode Project (iPhone + iPad)**. It writes `Builds/TabletopReels-iOS-Xcode.zip`. On a Mac:
1. Unzip it and open `TabletopReels/Unity-iPhone.xcodeproj` in Xcode.
2. Select the **Unity-iPhone** target, then **Signing & Capabilities**, and choose your Apple ID team. If Xcode says the bundle id is taken, change it (e.g. add your initials).
3. Connect the iPhone/iPad, trust the computer, turn on **Developer Mode** (Settings → Privacy & Security), then press **Run**.
4. With a free Apple ID, the app expires after 7 days; re-run from Xcode to refresh it.

On a touchscreen the world shows a thumb stick (walk) and A (talk/jump), RUN, VIEW and MENU buttons (D-032). Tap works for menus, reels, pieces and the lever. A Bluetooth controller or keyboard still works too. If Xcode ever shows `Sandbox: ... deny` or `umbrella header` errors, the build predates D-032: rebuild from Unity.

**In Unity:** open `Assets/Game/Scenes/World.unity` (the journey) or `Assets/Game/Scenes/MatchPrototype.unity` (practice table) and press Play.

Flow: setup (see the opponent) → choose two units (Striker, Caster) for slots A/left and B/right → each round spin up to three times, locking reels you want to keep → the opponent spins → both sides are revealed → the round resolves step by step → repeat until a Crown breaks → Victory / Defeat / Tie → Rematch, Change Units, Copy Replay, or Exit.

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Move focus | Arrow keys / WASD, mouse hover | D-pad / left stick |
| Confirm, or lock/unlock the focused reel | Enter / Space, click | A (south) |
| Spin (pull the lever) | R, or click the lever | Y (north), or focus the lever and press A |
| Lock in with all five locked | R, or click the lever (it reads LOCK IN) | Y, or A on the lever |
| Lock reel 1-5 directly | 1-5 | focus the reel + A |
| Next / previous element | Tab / Shift+Tab | RB / LB |
| Skip the current step while the round plays out | Tab | RB |
| Inspect focused unit | I (or click a piece) | X (west) |
| Help | H | View / Select |
| Hold to speed up animations (x4) | hold Space | hold A or RT |
| Pause / close overlay | Esc | Menu / Start (B closes overlays) |

Developer mode (setup screen button) adds: fixed seed, reel tier (Copper-Platinum), AI profile (Learner/Standard/Expert), any opponent unit, forced scenarios (Victory / Defeat / Tie next round, Rank-up and bomb, Barrier wall), replay verification from the clipboard, all units in selection, "Skip all presentation" in pause, and "Replay same seed" on the result screen.

## Latest verification

```text
Date/time: 2026-09-22 14:20 (local)
Unity editor version: 6000.5.9f1 (D-010)
Render pipeline: URP 17.5.0 (Assets/Game/Settings/URP_Pipeline.asset) on Graphics + all quality levels
Unity MCP connection: Unity AI Assistant (com.unity.ai.assistant 2.19.0-pre.2), verified throughout
Input System package/version: com.unity.inputsystem 1.20.0
Active Input Handling: Input System Package only (activeInputHandler = 1; EditMode test asserts it)
Test Framework: com.unity.test-framework 1.7.0
EditMode tests: 92 passed, 0 failed (Tabletop.Tests.EditMode; 3 consecutive clean runs)
PlayMode tests: 21 passed, 0 failed (Tabletop.Tests.PlayMode; virtual keyboard/gamepad/mouse via Input System test fixture)
Console status: 0 errors, 0 warnings in a clean Play Mode session
Development build: Succeeded, 0 errors, 0 warnings -> Builds/Windows/TabletopReels.exe (~168 MB, dev build); headless launch loads the scene with no exceptions
Replay verification: seed 1019410037, 62 accepted commands, final state hash 1b741590fb70ab2a reproduced exactly
Frame rate: ~58 fps in the editor Game view at 1920x1080 against a 60 fps cap
```

Screenshots (1920x1080 unless noted), in `Docs/Screenshots/M1/`:

- `00_setup.png` — setup: opponent and its units visible first
- `01_setup_selection.png` — unit selection with A/B assigned
- `02_spin_decision_mixed_locks.png` — spin decision, reels 1 and 3 locked, live preview
- `03_resolution.png` — round resolution in progress, final totals, event log
- `04_match_result.png` — result overlay (Defeat) with rematch / change units / copy replay / exit
- `05_ui150_1280x720.png` — 150% UI scale at 1280x720

## What was built

- **Rules engine (M0):** pure C# (`Tabletop.Domain`, no UnityEngine reference). Exact player reels and all six fifth-reel tiers; three spins, lock/unlock, third-spin and all-locked finalization; symbol formula; XP, ranks, bombs; energy, readiness, activation; Barrier and height targeting; multi-hit, direct damage, delay, healing, energy grant, self-damage; the twelve-stage resolution order; win/loss/tie after the full round; all Section 15 decisions (RULES_SPEC 15.1). Immutable events carry an authoritative snapshot; structured rejections; replay payload (rules version, content hash, seed, sides, commands, final hash).
- **AI:** Learner / Standard / Expert over the same command surface; input is only the public round-start state plus its own rolls (tested structurally and behaviorally).
- **Match (M1):** `MatchPrototype.unity` with primitives and placeholder UI; full MATCH_SETUP → UNIT_SELECT → ROUND_READY → SPINNING → SPIN_DECISION → AI_COMMIT → REVEAL → RESOLVING → MATCH_RESULT flow; ordered event presentation with a desync check after every event; acceleration, reduced motion, pause, help, inspection, UI scale, screen-shake toggle (off by default), non-color cues (shapes, text, glyphs).

## Known defects and deliberate limits

- No audio or haptics yet (the spec lists cues as SHOULD). No state is communicated only by sound.
- Hovering a unit focuses its plaque, which already shows every current-rank statistic. The full inspection card (all ranks) opens with Inspect / click rather than on hover.
- Bindings are centralized in `GameInput.inputactions` and ready for remapping, but there is no in-game remapping screen yet.
- Accessibility labels are surfaced as a visible "FOCUS:" description line, not through a screen reader.
- The 3D table is deliberately crude: block/cone figures, stacked Barrier bricks, crown cylinders, a single projectile ball.
- Frame rate was measured in the editor only; the built player was smoke-tested headless.
- Standard AI balance is untuned; the playtest should judge whether it feels fair.

## Update 2026-09-23 — readability pass (D-023)

```text
Date/time: 2026-09-23 11:05 (local)
Milestone: M1 (playtest-readability fix; gate still active)
Completed: original icon set (gems, hammer, XP star, crown, wall, padlock, bomb, unit portraits);
  knight/wizard figures, real crown, growing brick wall, energy rods, 3D name/number labels,
  floating +/- numbers on impact, symbol legend, reel captions naming the unit each symbol feeds,
  larger table view, crown/wall readouts over the table, portraits on plaques and selection cards,
  player-facing "Wall" wording for Barrier
Tests: EditMode 92/92, PlayMode 21/21 (bootstrap test now also checks the icon set)
Console/build: 0 errors / 0 warnings; Windows build succeeded (0 errors, 0 warnings), headless launch clean
Screenshots/build path: Docs/Screenshots/M1/*.png refreshed; Builds/Windows/TabletopReels.exe
Known defects: figures are still crude primitive shapes; no audio
Blockers: none
Next authorized work: playtest fixes only
Gate status: HUMAN PLAYTEST GATE ACTIVE — awaiting creative-director approval.
```

## Update 2026-09-23 — magenta board in the Windows build (D-024)

```text
Date/time: 2026-09-23 12:35 (local)
Milestone: M1 (defect fix; gate still active)
Completed: reproduced in the real player (116 of 116 board renderers had an unsupported shader);
  all board pieces now use the URP material asset BoardPlaceholder.mat; added build self-check mode
  (-tabletopSelfCheck) and Tools/selfcheck_build.sh; regression PlayMode test
Tests: EditMode 92/92, PlayMode 22/22
Console/build: Windows build succeeded (0 errors, 0 warnings); build self-check: renderers=116,
  unsupportedShader=0, notBoardMaterial=0; screenshots inspected and correct
Screenshots/build path: Logs/BuildSelfCheck/*.png (local); Builds/Windows/TabletopReels.exe
Known defects: none new
Blockers: none
Gate status: HUMAN PLAYTEST GATE ACTIVE — awaiting creative-director approval.
```

## Update 2026-09-24 — W1 world slice (D-025)

```text
Date/time: 2026-09-24 14:45 MDT
Milestone: W1 world slice (approved past the gate by the creative director)
Completed: World.unity (first scene): title; Hearthmoor, North Road with Wren's camp and bridge, Brindlecross,
  Champion's Hall with interior table; 6 opponents (Gran, Wren, Mira, Tobin, Halvey, champion Corvin) plus 4 villagers;
  talk / CHALLENGE / NOT NOW; sit at the champion's table; match opens on unit select vs the encounter's units and AI;
  RETURN TO THE VILLAGE puts you back where you stood and the opponent reacts; wins counted in the HUD (memory only).
Fixes found by the new tests: WASD/arrows did not move the player (the match's Shift+Tab shortcut-consumption setting
  let UI Navigate swallow the keys; the world router now turns it off); the title screen reappeared after every match.
Tests: EditMode 97/97 (5 new WorldDataTests); PlayMode 27/27 (5 new WorldTests: title + keyboard/gamepad walking + pause,
  walkable bounds, villager talk with gamepad, challenge Gran -> win at the table -> return, hall door + champion chair).
Console/build: 0 compile errors; Windows build 0 errors / 0 warnings; self-check 1312 world renderers, 0 unsupported shaders.
Screenshots/build path: Docs/Screenshots/World/ (from the real player); Builds/Windows/TabletopReels.exe
Known defects: placeholder art; champion portrait is his unit icon; area title briefly overlaps the scene when entering an area;
  one self-check launch immediately after a build stalled at startup and a rerun passed (not reproduced).
Blockers: none
Next authorized work: fixes only, until the creative director plays W1.
Gate status: W1 PLAYTEST STOP ACTIVE.
```

## Update 2026-09-25 — imported models (D-026)

```text
Date/time: 2026-09-25 11:10 MDT
Milestone: W1 world slice + creative-director models
Completed: Art pipeline (Tabletop > Art menu, WorldArt.asset slots, automatic fit/grounding, runtime posing).
  In the world: Wren and Mira Tallow use supplied character models; the Brindlecross Inn and Chandlery use the
  steampunk house; the Hearthmoor Cottage is the mushroom house. Trees no longer hide Wren's camp.
  Workflow written up in Docs/ART_WORKFLOW.md.
Not placed: 4 supplied character models excluded (nudity / existing franchise character / youthful + sexualized);
  3 swimwear models held back pending the creative director's confirmation (see D-026). All stay local, git-ignored.
Tests: EditMode 100/100 (WorldArtTests: fit/grounding, rigged-model scale, art-set names + URP materials);
  PlayMode 28/28 (imported models in place, grounded, URP, posed via the runtime path).
Console/build: Windows build 0 errors / 0 warnings; self-check modelBuildings=3 modelPeople=2, 0 unsupported shaders,
  PLAYER PROBLEMS: none. (The first model build logged an editor-only animation error; fixed and now checked.)
Screenshots/build path: Docs/Screenshots/World/ (refreshed from the real player); Builds/Windows/TabletopReels.exe
Known defects: supplied characters are 180k-300k triangles (build grew ~55 MB); Mira's model stands on a figurine base;
  rigged models hold one frame of an attack clip (no idle animation supplied); name tags overlap model heads slightly.
Blockers: none
Next authorized work: fixes only, until the creative director plays W1 with the new models.
Gate status: W1 PLAYTEST STOP ACTIVE.
```

## Update 2026-09-25 — journey features, piece progression, and the new table (D-027)

```text
Date/time: 2026-09-25 13:30 MDT
Milestone: W2 (journey features + production table), authorized by the creative director
Completed:
  World: save/continue (journey.json; CONTINUE JOURNEY on the title), name tags fade in within 7-11 m,
    overhead / first-person toggle (V / Y), sprint (Shift / LT / L3) and jump (Space / A), deck screen (I / X).
  Progression: town villagers play Striker + Caster; Corvin plays Ranger + Striker and beating him wins the
    Ranger, which then appears at unit selection and in the deck.
  Table: full-screen 3D MechanicalTable in the source's style (carved stone, bronze, gold). Eight-sided reel
    drums that spin and clamp when locked; flip-digit crown counters; brick walls that rise from slots and
    shed bricks when hit; podium energy rings, gem pillars, stat plaques; pieces travel along grooves to attack.
    Reel buttons overlay the drums; panels moved to the corners; SPIN bottom right.
Fixes found on the way: excluded models' unpacked copies were committed by the Prepare step (moved the models
  out of Assets and removed the copies); a Unity "save changes?" dialog could stall unattended test runs.
Tests: EditMode 104/104; PlayMode 35/35 (new WorldFeatureTests; hidden-reel test now checks the drum shutters;
  material test accepts Table_ variants of the board material).
Console/build: Windows build 0 errors / 0 warnings; self-check world 1245 renderers, table 399 renderers,
  0 unsupported shaders, attack captured, PLAYER PROBLEMS: none.
Screenshots/build path: Docs/Screenshots/Table/, Docs/Screenshots/World/; Builds/Windows/TabletopReels.exe
Known defects: table figures are still primitive miniatures (blobby from above); the earlier commit ff17d5c still
  contains unpacked textures of the excluded models in Git history (removing them needs a history rewrite).
Blockers: none
Next authorized work: fixes only, until the creative director plays this build.
Gate status: PLAYTEST STOP ACTIVE (waiting for the creative director).
```

## Update 2026-09-25 — the board is the interface (D-028)

```text
Date/time: 2026-09-25 21:20 MDT
Milestone: W2 polish, requested by the creative director
Completed:
  Locked-reel energy: nameplate gem tally + exact result ("+2 ENERGY", "1 MORE = +1", red "(1 WASTED)"),
    pulsing ring segments, red overflow; wall label "WALL a > b (n WASTED)". Only locked reels count.
  Confirm after locking: all five locked no longer ends the turn; the lever reads LOCK IN and confirms.
  Sequence: opponent, reveal, 1 XP, 2 WALL, 3 ENERGY, 4 ACTIONS, each with a beat and a flip-sign; symbols fly
    from reels to where they apply; Tab / RB skips a step; hold Space still fast-forwards.
  Action order: READY tags before resolution, numbered order tokens during actions.
  Board-only UI: corner panels, log, legend, banner and on-screen buttons removed; nameplates, step sign,
    lever with spin lamps, round dial; key-cap prompt bar, toast, round Help / Menu buttons.
  Removed the curved arch inlay. Inter font (OFL) and a generated UI kit. Result screen is a scoreboard.
  World: people carry dark gilt-edged nameplates (name + CHALLENGER / CHAMPION / BEATEN / title) well above
    their heads; world HUD restyled to match.
Tests: EditMode 108/108 (locked-only preview, next-point helper, confirm-after-lock);
  PlayMode 37/37 (ordered sequence + Tab skip, nameplate not clipped and clear of the head).
Console/build: Windows build 0 errors / 0 warnings; self-check world 1245 renderers, table 440 renderers,
  0 unsupported shaders, attack captured, PLAYER PROBLEMS: none.
Screenshots/build path: Docs/Screenshots/Table/, Docs/Screenshots/World/; Builds/Windows/TabletopReels.exe
Known defects: the enemy crown's back spike clips the top corner of enemy reel 3's window (the face stays
  readable); table figures are still primitive miniatures. (Excluded models' textures erased from Git
  history on 2026-09-25, D-031.)
Gate status: PLAYTEST STOP ACTIVE (waiting for the creative director).
```

## Active blockers

None.

## Next authorized work

Only fixes that prevent or distort the W1 playtest (crashes, broken controls, rule defects, unreadable UI, build/test failures). Everything beyond `WORLD_SPEC.md` (saving, rewards, more player units, production art, M2-M5) waits for explicit creative-director approval.

## Update format

For each material update, append:

```text
Date/time:
Milestone:
Completed:
Tests:
Console/build:
Screenshots/build path:
Known defects:
Blockers:
Next authorized work:
Gate status:
```
