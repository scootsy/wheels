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

### D-023: Readability pass with simple placeholder assets (pre-gate)

- **Date:** 2026-09-23
- **Status:** Accepted (creative director request: "could we start building some simple assets?" because the first build was hard to read)
- **Decision:** Treated as a playtest-readability fix allowed before the gate, not as production art. Added: an original flat icon set generated by `Tools/Art/make_icons.py` (energy gems, hammer, XP star, crown, wall, padlock, bomb, unit portraits) imported as sprites and referenced through `Assets/Game/Art/IconSet.asset`; recognizable primitive figures (Striker = knight with sword and shield, Caster = wizard with pointed hat and staff), a real crown, a brick wall that grows with Barrier, energy rods, and camera-facing labels on the 3D table; floating +/- numbers at each impact; a symbol legend; reel captions that name which unit each symbol feeds; a larger table view.
- **Terminology:** player-facing text calls the Barrier the **Wall**. Rules documents and code keep "Barrier".
- **Not done (still post-gate):** production models, textures, VFX, audio, Blender work.
- **Files updated:** `Docs/IMPLEMENTATION_STATUS.md`, presentation code, `Tools/Art/make_icons.py`

### D-024: Board material asset and visual build verification

- **Date:** 2026-09-23
- **Status:** Accepted (defect fix)
- **Problem:** The Windows build showed the whole 3D table as magenta. Pieces are created at runtime with `GameObject.CreatePrimitive`; in the editor they receive URP's default material, but in a player they fall back to the built-in Standard material, which URP cannot draw. Editor Play Mode tests and the headless (`-batchmode`) build launch could not see this.
- **Decision:** Every placeholder piece uses `Assets/Game/Art/Materials/BoardPlaceholder.mat` (URP/Lit), referenced by `MatchApp.boardMaterial` so builds include its shader. A PlayMode regression test asserts every board renderer uses it.
- **Verification rule:** after every build, run `bash Tools/selfcheck_build.sh`. It launches the real player windowed with `-tabletopSelfCheck`, which starts a match automatically, saves screenshots of what the player actually renders, logs renderer/shader diagnostics, and quits. A build is not reported as working until those screenshots are inspected.
- **Files updated:** `Docs/TOOLING.md`, `Docs/IMPLEMENTATION_STATUS.md`

### D-025: Creative director opens the world slice (W1) past the playtest gate

- **Date:** 2026-09-24
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction:** "remember, this is supposed to be a game with a 2.5D view where you explore the world, interact with people, challenge them, then sit down at a table to play. lets start to build that. make the starting village. then make a path north the player has to traverse. then a village he reaches there with people and buildings, several who he can challenge and interact with and then a building where the towns wheels champion lives and he goes in there and sits down and challenges him."
- **Decision:** This is explicit approval to cross the human playtest gate **for this world slice only** (called W1, a focused form of the M3 "tiny world wrapper"). Scope is defined in `Docs/WORLD_SPEC.md`: Hearthmoor → North Road → Brindlecross → Champion's Hall, six opponents, talk/challenge/return flow, title and pause menu.
- **Still gated (not approved):** saving, rewards, collection, currency, shops, quests, NPC schedules, additional player-facing units (the player still picks only Striker and Caster), production art, audio, Blender work.
- **Implementation decisions (agent, overridable by the creative director):**
  - Names, dialogue, and village layouts are original placeholders.
  - Opponents use existing reference units and AI profiles (Learner, Standard, Expert); the champion is Expert with Shade + Caster.
  - The champion is not locked behind the other opponents.
  - Wins live in memory only (`GameFlow`), matching the no-save rule.
  - Challenging skips the practice setup screen and opens unit selection directly; leaving the table early records no result.
  - The original free match remains reachable as PRACTICE TABLE from the title and the pause menu.
- **Architecture:** new `Tabletop.World` assembly (Unity) builds the world procedurally at load, like the match diorama. The world ↔ table handoff (`EncounterCatalog`, `GameFlow`) lives in `Tabletop.Application` with no engine references. `World.unity` is the first scene in the build; `MatchPrototype.unity` second. Movement uses the existing `WorldReserved/Move` and `Interact` actions.
- **Input note:** the match router enables `InputSystem.settings.shortcutKeysConsumeInput` (so Shift+Tab does not also fire Tab). In the world that made the UI Navigate composite swallow WASD/arrows, so `Move` read zero. The world router passes `consumeShortcuts: false`; a PlayMode test walks with the real Input System.
- **New stop point:** after W1 is verified, stop and wait for the creative director to play it before any further expansion.
- **Files updated:** `AGENTS.md`, `Docs/WORLD_SPEC.md` (new), `Docs/README.md`, `Docs/MILESTONES.md`, `Docs/TECHNICAL_ARCHITECTURE.md`, `Docs/IMPLEMENTATION_STATUS.md`

### D-026: Creative director's imported models in the world, and the art workflow

- **Date:** 2026-09-25
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction:** "I created a folder in Assets > Game called Characters and I put several fbx models in there. could you swap out some of the characters in the game currently for these models? i did the same in a directory called Buildings and it has two building models. Could you try to incorporate these into the game and then tell me what a good workflow for this actually should be?"
- **Decision:** Imported, creative-director-supplied models may replace W1 placeholders. This is a scoped exception to the gate's "production models" line, limited to models the creative director provides. No Blender production work, VFX, or audio is started.
- **Pipeline (agent decision):**
  - `Tabletop → Art → 1. Prepare Imported Models` unpacks embedded textures, creates one URP/Lit material per model, and remaps the FBX's materials to it. It also turns off rig/animation import for static models and camera/light import for all.
  - `Assets/Game/Art/WorldArt.asset` (`WorldArtSet`) maps people (by display name, plus `Player`) and buildings (`Area/Name`) to models, with optional pose clip, loop, turn and size. Empty slots keep placeholders.
  - `WorldKit.PlaceModel` scales a model to a height (people) or footprint (buildings), grounds it, and centres it. Rigged models are posed through Playables (`ModelPose`).
  - The full workflow is in `Docs/ART_WORKFLOW.md`.
- **Defect found by the build self-check:** `AnimationClip.SampleAnimation` on non-legacy clips only works in the editor, so the player build left rigged models in their rest pose and logged an error that the editor never showed. Fixed with Playables. A PlayMode test requires the runtime path, and `Tools/selfcheck_build.sh` now prints any player-reported problem.
- **Placed:**
  - Wren = the clothed wolf-eared character, holding the first frame of its only clip.
  - Mira Tallow = the clothed figure on a round base, 2.0 m including the base.
  - Brindlecross Inn and Chandlery = the steampunk house, 6 m wide.
  - Hearthmoor Cottage = the mushroom house.
- **Not placed:** these stay on the creative director's machine and are ignored by Git.
  - Four character models are excluded outright: one fully nude, one apparently unclothed, one that recreates a well-known existing franchise character (a child in the source material), and one youthful-looking character in a sexualized state. The project is original work, and these are not suitable content.
  - Three swimwear/bikini-style models are **held back pending the creative director's confirmation** that they are original characters and that this is the intended tone. Their prepared materials exist locally, so they can be dropped into slots if confirmed.
- **Performance note:** the supplied characters are 180k-300k triangles each. The workflow asks for 10k-30k and an idle animation.
- **Files updated:** `AGENTS.md`, `Docs/ART_WORKFLOW.md` (new), `Docs/README.md`, `Docs/WORLD_SPEC.md`, `Docs/TECHNICAL_ARCHITECTURE.md`, `Docs/TOOLING.md`, `Docs/IMPLEMENTATION_STATUS.md`, `.gitattributes`, `.gitignore`

### D-027: Journey features, piece progression, and the table overhaul (creative director, 2026-09-25)

- **Date:** 2026-09-25
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction:** "yes, build the table. and i actually want it to look identical to the art style of the source, just in 3d instead of pixelated. also, we need to add a way to save/resume progress. separately, names shouldn't always be above people, maybe they fade in as you get close? separately, i'd like to be able to switch between overhead pov and first person so that people have the choice. then i'd like to make it so all opponents have like the same wheels in a town, but then the boss adds one more, and our character wins it from them when he beats em. then i'd like movement to be a little more fun, so maybe add an option to sprint and jump, just so getting around isn't so tedious. then i'd like also a way to inspect your own "deck" look at your wheels and see their stats, etc."
- **Clarification (asked and answered):** "wheels" here means the pieces. Everyone in a town plays the Striker and the Caster, practice for the player. The town champion adds one more piece (Brindlecross: the Ranger). Beating him wins it. The next town's champion will hold the Mason, and so on.
- **Now authorized (overrides the gate items it names):** save data; the player collecting new pieces (Ranger first); the production table (M4 direction, including animated pieces); first-person view.
- **Decisions:**
  - **Save/resume:** one JSON file, `journey.json`, in the player's data folder, written atomically. It holds who you have beaten, losses, pieces owned, position, facing and view choice.
    - Autosave: when a journey starts, on entering an area, every 30 s, before a challenge, when you get back from a table, and on quit.
    - The title offers CONTINUE JOURNEY. NEW JOURNEY over an existing save needs a second press.
    - A match in progress is not saved; resuming puts you back at the table's spot.
    - Tests and the build self-check write to scratch folders only.
  - **Name tags** fade in between 11 m and 7 m. The challenge gem stays visible from afar so challengers can still be found. The floating "YOU" label is gone.
  - **First person:** toggled with V / gamepad Y, or from the menu; the choice is saved.
    - Mouse or right-stick look. Walk where you look. Interaction targets what is in front of you.
    - Your body casts a shadow but isn't drawn. The mouse is captured only while exploring.
    - Sitting at the champion's table keeps the table camera.
  - **Sprint and jump:** Shift / LT / L3 sprints at 1.8x. Space / A jumps (about 1.1 m); next to someone, A talks instead.
  - **Deck:** I / gamepad X, or the menu. It shows every piece, owned or locked, with Bronze/Silver/Gold stats. Locked pieces say who holds them.
  - **Opponent tuning (agent choice, easy to change):** Gran and Wren use the Learner AI; Mira, Tobin and Halvey use Standard; Corvin uses Expert with Ranger + Striker.
  - **Unit selection** offers exactly the pieces you own, laid out as 3 cards once the Ranger is won.
  - **Input note:** a test found that two key presses queued in the same frame by the Input System test fixture overwrite each other. Tests press keys on separate frames; real keyboards are unaffected. Test runs now reload the code-generated scenes first, so Unity's "save changes?" dialog can't stall an unattended run.
  - **Table art style:** the creative director wants the table to match the source game's look, rendered in 3D. We match the style: warm carved bronze and stone, gold crown housings with red gems, a grey brick wall arc, framed reel tiles, clockwork. The shapes, ornaments and layout details are drawn fresh rather than traced or copied from the reference art (`RULES_SPEC.md` Section 2).
- **Files updated:** `Docs/WORLD_SPEC.md`, `Docs/IMPLEMENTATION_STATUS.md`, `AGENTS.md`, `Docs/MILESTONES.md`

### D-028: The board is the interface — sequence, locked-reel energy, confirm after locking (creative director, 2026-09-25)

- **Date:** 2026-09-25
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction (summary):** show how much energy the LOCKED reels will give, so pieces are not overfilled; a modern UX overhaul of the board with no text dumps; slow the round down into a readable sequence (spins revealed, XP, energy, wall, each action) with a key to skip; make the action order visible instead of a guess; require a confirm after locking so an accidental fifth lock does not end the turn; remove the curved inlay that looked like the wall slot; put everything on the board (the boards are purely mechanical devices in the lore), with no info boxes in the corners; production-ready look; move character names so they do not block the characters, and make them look finished.
- **Decisions:**
  - **Locked-reel energy.** While you decide, each piece's nameplate shows a gem tally counting only the gems on locked reels. Two cells prime the count, and each cell after that is marked +1. Beside the tally the plate says exactly what you will get: "+2 ENERGY", "1 MORE = +1", or in red "+3 (1 WASTED)".
    - The podium's energy ring shows what is stored, pulses the segments the locked reels will fill, and shows red segments past the ring for any overflow.
    - The wall label works the same way: "WALL 2 > 4 (1 WASTED)".
    - Unlocked reels are not counted, because they will spin again. `OutcomePreview.Compute(..., lockedOnly: true)`.
  - **Confirm after locking (interface only; the rule is unchanged).** Locking the fifth reel no longer ends the turn by itself.
    - The sign reads "LOCK IT IN?" and the lever plate turns green and reads LOCK IN. Focus moves to the lever.
    - Pulling it (R / Y / click) confirms. Unlocking any reel returns to spinning.
    - The simulation's early finalize (`RULES_SPEC.md` 4.3, IMPL-03) is untouched; only the moment the controller issues it changed.
  - **The round is a sequence.** The presenter groups events into steps: opponent choosing, reveal, 1 XP, 2 WALL, 3 ENERGY, 4 ACTIONS, round over.
    - Each resolution step opens with a short beat. Resolution events play 1.6x slower than before.
    - A flip-sign in the centre of the table names the step. The strip beneath it lights the steps in turn.
    - Symbols fly from the reels to where they apply: stars to the XP diamonds, gems to the podiums, hammers to the wall.
    - **Tab / RB skips the current step** (it is presented instantly, in order). Holding Space / A / RT still fast-forwards. Skipping never changes the result (test-covered).
  - **Action order is shown.** Before a round resolves, pieces that will be ready wear a green READY tag. When the actions step starts, every piece that will act gets a numbered token (1, 2, 3...) in the exact order it will act, read ahead from the event queue. The token of the acting piece glows and finished ones dim.
  - **Everything on the board.** Gone: the corner panels, the event log, the legend, the banner, the preview text and the SPIN/speed buttons.
    - In their place are table parts: four nameplates (rank badge, name, XP diamonds, crown and wall damage, the energy tally), the step sign, the round dial, and the SPIN lever with three lamps for spins left and a label plate that says what the lever will do.
    - Invisible hit areas over the drums, podiums and lever keep keyboard, gamepad and mouse focus working (focus is drawn on the table as a gold ring).
    - What remains on screen: a bottom-left prompt bar with key caps for what you can do right now, a short toast for rejections, and two round buttons for Help and Menu.
  - **Removed the curved inlay** (the arch in front of each crown); the wall rises from its own slot next to the crown. The plaza oval was widened to seat the sign.
  - **Nameplates, not floating yellow text.**
    - On the table, names are on flat plates in front of each podium, never over a piece.
    - In the world, people carry a small dark card with a gilt edge, the name and a caption: CHALLENGER, CHAMPION, BEATEN, or their title. It floats about 0.75 m above the head instead of 0.45 m.
  - **Result screen and world HUD restyled.** The result screen is a scoreboard: VICTORY / DEFEAT / TIE, "vs opponent · n rounds", both final crowns side by side with each team's pieces, then the buttons. Reel tier, AI and seed move to a small footer. The world HUD, prompt and dialogue boxes use the same warm, gilt-edged cards, and the area title is cream instead of yellow.
  - **Production UI kit.** Inter (SIL Open Font License, bundled with its licence in `Assets/Game/Resources/Fonts`) replaces the default font. The warm dark theme is gilt-edged. Rounded nine-slice panels, key caps, rings and shadows are generated by `Tools/Art/make_ui.py` into `Assets/Game/Art/UI` and imported through the icon set.
- **Files updated:** `Docs/MATCH_UX_SPEC.md` (4.4, 4.5, 5, 7.2, 7.3), `Docs/RULES_SPEC.md` (IMPL-03 note), `Docs/IMPLEMENTATION_STATUS.md`, `Docs/TECHNICAL_ARCHITECTURE.md`

### D-029: iPhone / iPad test builds (creative director, 2026-09-25)

- **Date:** 2026-09-25
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction:** "lets not worry about touch yet. i just want to test it on my iphone/ipad as we iterate. so if you could create those builds here on this; that would be great. then i can just download/build the ipa manually on my macbook with xcode and sideload it."
- **Decisions:**
  - **What gets built here:** Unity on Windows can't make a finished iOS app. **Tabletop → Build → iOS Xcode Project (iPhone + iPad)** (`TestAndBuildTools.BuildIOS`) writes an Xcode project to `Builds/iOS/TabletopReels` and zips it to `Builds/TabletopReels-iOS-Xcode.zip`. The creative director opens it in Xcode on a Mac, picks a signing team, and installs it.
  - **Settings:** development build; iPhone and iPad; iOS 15+; landscape only; full screen on iPad; bundle id `com.scootsy.tabletopreels`; automatic signing with the team chosen in Xcode.
    - A free Apple ID may need a different bundle id if that one is taken; change it in Xcode.
    - After building, the editor switches back to Windows, so tests and the Windows build are unaffected.
  - **Controls:** there are no touch controls yet (deferred by the creative director). Tapping menus, reels, pieces and the lever works through the existing pointer support. Walking in the world, skipping steps and speeding up need a Bluetooth game controller or a keyboard.
  - **Screen shapes (needed for the iPad to be testable):**
    - The interface frame used to scale by screen height, which cut off the sides on narrower-than-16:9 screens. `FrameFitter` now scales by width on those screens, leaving margins above and below.
    - The table camera widens its vertical field of view on narrower screens (`MechanicalTable.FieldOfViewFor`), so the lever and the round dial stay in view.
    - At 16:9 and wider (PC, iPhone) nothing changes.
    - This also fixes non-widescreen Windows windows.
  - **Not included:** App Store / TestFlight distribution, touch controls, and a macOS build. Each waits for its own go-ahead.
- **Files updated:** `Docs/TOOLING.md`, `Docs/IMPLEMENTATION_STATUS.md`, `Docs/MATCH_UX_SPEC.md` (section 5 screen shapes)

### D-030: Shelved people models, build-all, published test builds (creative director, 2026-09-25)

- **Date:** 2026-09-25
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction:** "could you upload the builds? id lkke them in the repo. also, im curious if you can manage all the different buolds at the same rime? ... also, last thing... fhe models we jsed are a little goonery. any chance we could keep them in there, bur like switch back to the origials? even if its just temporary. like commentinf somethjng out. ... i dont want to hse those models in the build im sending out to testers"
- **Decisions:**
  - **Shelving models:** `ArtShelf` (Tabletop → Art → Shelve / Restore) moves model assignments between `WorldArt.asset` and an editor-only shelf asset. A shelved model has no reference from the game, so Unity leaves it out of builds.
    - The two imported people (Wren, Mira Tallow) are shelved. The imported buildings stay; they were not the concern. They can be shelved the same way.
    - Every build reports how many imported model files it contains, so this is checked, not assumed.
  - **Builds go to GitHub Releases, not Git history:**
    - `AGENTS.md` keeps build output out of Git.
    - Each Windows + iOS pair is about 0.5 GB, so committing builds through Git LFS would use up GitHub's free LFS allowance in a couple of builds, and every old build would stay in history.
    - A Release attaches the zips to a tagged commit on the repo's Releases page instead. The repo stays small and each build is downloadable from GitHub.
    - The process: `Tabletop → Build → All (Windows + iOS)`, then `bash Tools/publish_builds.sh "note"`. It refuses to publish uncommitted work.
  - **Several platforms:** building both targets is one command (about 3 minutes), so there is no cost to keeping them in step. Windows remains the everyday verification target. iOS is rebuilt whenever a build is published.
- **Files updated:** `Docs/ART_WORKFLOW.md`, `Docs/IMPLEMENTATION_STATUS.md`

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

