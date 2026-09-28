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

### D-031: History rewrite to erase the excluded models' textures (creative director, 2026-09-25)

- **Date:** 2026-09-25
- **Status:** Accepted (explicit creative-director approval: "yes go agead and do the hsotory rewrite and erase those textures")
- **Decision:**
  - `main` was rewritten with `git filter-branch` to remove the 22 unpacked texture and material files of the four excluded models from every commit. The model files themselves had never been committed.
  - The rewrite was force-pushed with a lease on the old tip. The project's current files are unchanged (same tree). Every commit hash from D-026 onward changed.
  - The test-build release was deleted and republished on the new commit, so no tag keeps the old commits reachable.
  - Locally the old history, reflog and LFS copies were purged. A backup bundle of the pre-rewrite history is kept outside the project (`../wheels-history-backup-2026-09-25.bundle`; it holds Git history only, not the LFS texture files).
- **Limits:**
  - GitHub keeps LFS objects that were ever uploaded until the repository is deleted or GitHub Support removes them.
  - GitHub may also keep the orphaned commits reachable by their old hash for a while.
  - Nothing in the repository's history or on its pages references them any more.
  - Any other clone must be re-cloned.
- **Files updated:** `Docs/IMPLEMENTATION_STATUS.md`

### D-032: iOS Xcode build fix and stop-gap touch pad (creative director, 2026-09-26)

- **Date:** 2026-09-26
- **Status:** Accepted (creative director asked to fix the failing iPhone build and add "a little virtual joystick button pad" without full touch controls)
- **Decision:**
  - The Xcode build failed on Xcode 27 beta for two reasons: user script sandboxing blocked Unity's IL2CPP build script (`Sandbox: mkdir/chmod deny`), and module verification rejected UnityFramework's headers (`umbrella header ... does not include`, `expected a type`). `Assets/Game/Editor/IosXcodeFixups.cs` sets `ENABLE_USER_SCRIPT_SANDBOXING = NO` and `ENABLE_MODULE_VERIFIER = NO` on the generated project after every iOS build.
  - `Assets/Game/World/WorldTouchPad.cs` adds an on-screen stick (bottom-left) and A / RUN / VIEW / MENU buttons (bottom-right) in the world when a touchscreen is present. They use the Input System's `OnScreenStick`/`OnScreenButton`, which press a virtual gamepad, so the existing gamepad bindings drive Move, Interact/Jump, Sprint, ToggleView and Pause. No gameplay code reads touch directly. The pad hides during dialogue, menus, the deck and at the table.
- **Verified on Windows before publishing (2026-09-26):**
  - The first version compiled on nothing: `Application.isMobilePlatform` resolved to the project's `Tabletop.Application` namespace. It is now `UnityEngine.Application`.
  - The first Xcode fix sat behind `#if UNITY_IOS` and used the iOS-only `PBXProject` API. `BuildIOS` runs while the editor is still compiled for Windows, so it would have been skipped silently. It now edits `project.pbxproj` as text, with no platform switch, and is covered by `IosXcodeFixupsTests`. The published Xcode project was checked: all 20 build configurations have both settings set to `NO`.
  - The pad was forced on in Play Mode: dragging the stick walked the player north. Screenshot: `Docs/Screenshots/World/touchpad.png`.
- **Limits:** no touch camera look in first-person view (use VIEW to return to the overhead camera). The match table already works by tapping. Placeholder look only.
- **Files updated:** `Docs/IMPLEMENTATION_STATUS.md`

### D-033: Coins, errands, stalls, pack art and sound, the source's names, and the Stonemasons' Outpost (creative director, 2026-09-26)

- **Date:** 2026-09-26
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction:** "1. Introduce the plan with coins. Several errands per town to earn you bonus coins. 2. I've added a bunch of assets to Unity... make the world look better. Instead of a flatscape, give it variety, three dimensional, interesting. make the characters not just polygons/shapes. 3. If you can and have access to sound effects and music, add them. 4. Build the vendor and consumable lists. Try and do small things that could benefit a player but not break balance too significantly. 5. We got a licensing agreement in place and are free to use the source names/assets if we want, so update all of the names/assets where you can to match the source. 6. Lastly, build out a new area... just build it all out, and then test/bug find at the end." Characters: "Download free packs". Audio downloads: "Yes, download all".
- **Coins and stakes:** start with 20. Coin tables: winner takes the opponent's stake. Friendly table (Gran): free, a win pays 3. Short of coins: play for a favour (win half the stake; lose a short chore, no coins). Rematches stake again, falling back to a favour. Coins never go below 0. `GameFlow.SettleMatch` settles each finished match; `CompleteEncounter` still settles a lone result for older callers.
- **Errands:** 14 (Hearthmoor 4, North Road 2, Brindlecross 5, Outpost 3), delivery or fetch, paid by the receiver or the giver, one reward each; journal in the pause menu.
- **Stalls and items:** Ada's Stall and Anvara's Forge. Charms (one per match, used up when it ends): Crown Tonic 8 (Crown 12), Bag of Mortar 10 (Bulwark 2), Spark Flint 10 (2 energy each, never ready), Square / Diamond Medal 14 (one figurine starts Silver). Wheels (kept): Bronze 35, Silver 90, Gold 200; the player always brings their best.
- **Rules changes (RULES_SPEC 13.2 / 13.2a):** each side brings its own fifth wheel (tiers may differ); sides may carry boons that change only the starting state, encoded in the configuration and replays (absent boons encode as before). The test that rejected mixed tiers now asserts they are legal (approved change).
- **Names:** the creative director states a licence permits the source's names and assets. Player-facing text now uses Wheels / Wheel / Bulwark / figurine / Square and Diamond, and Warrior, Mage, Archer, Engineer, Assassin, Priest, Warlock. Code ids, saves and replays keep the neutral ids. Towns and people stay original. The source's art and audio are not in the project (none was supplied), so "assets where you can" covers names only; the table's pieces are now figurines as in the source.
- **New area:** the Quarry Path climbs from Brindlecross's east side to the Stonemasons' Outpost (plateau at 12 m, quarry pit, lookout knoll): three challengers on better wheels and bigger stakes, and Master Dorran Hale at the ledge table (Engineer + Warrior, stake 40, Silver wheel, prize: the Engineer). The name follows the source's second Champion location, whose champion also plays Engineer + Warrior.
- **Land and look:** a height field (`WorldGround`) and a runtime Unity Terrain painted by rule; Fantasy Kingdom, Polytope and ithappy art placed by `WorldBuilder`; KayKit (CC0) characters, recoloured and animated through Playables (`CharacterRig`); sky, fog and colour grading. Everything the world uses is listed in `WorldLook.asset` (built by Tabletop → Art → Build World Look and Sounds), which also caps pack textures at 2K (1K on iOS) so builds stay small. The table's pieces are KayKit figurines cast in the table metal (their finish still shows rank).
- **Sound:** OpenGameArt music by RandomMind (CC0), Kenney casino / RPG / UI audio (CC0), Fantasy Kingdom ambience; `AudioDirector` cross-fades music and ambience per place; every button clicks; the table has event sounds. SOUND ON/OFF in the pause menu.
- **Asset Store packs stay out of Git:** the repository is public and the Asset Store EULA does not allow publishing pack files. `.gitignore` excludes `Assets/Art`, `Assets/Audio`, `Assets/Terrain`, `Assets/TerrainTexturesPackFree`, `Assets/Polytope Studio`, `Assets/ithappy`, `Assets/Shaders`, `Assets/TextMesh Pro`; builds contain what they use. A checkout without them falls back to placeholders. CC0 downloads are committed under `Assets/Game/ThirdParty` (credits in `CREDITS.md`).
- **Import clean-up:** the Fantasy Kingdom package overwrote ProjectSettings and added packages. Our settings were restored from Git (the pack's versions are backed up in `../wheels-unused-packs/FantasyKingdom-settings-backup`). Its demo scripts, scenes, camera and URP assets, the Polytope demo scripts, and the Genies Avatar SDK (an online avatar service needing Genies accounts; not usable for villagers) were moved to `../wheels-unused-packs/`, not deleted. Polytope's own URP 17 shader package was imported so its art renders.
- **Controls:** Cancel (Esc / B) now closes any conversation, like NOT NOW (at a champion's table it stands you up); lists close with Esc / B.
- **Files updated:** `WORLD_SPEC.md`, `RULES_SPEC.md` (2, 13.2, 13.2a), `MATCH_UX_SPEC.md` (4.1, 4.8), `ART_WORKFLOW.md`, `IMPLEMENTATION_STATUS.md`, `AGENTS.md` (gate status).

### D-034: Quiet Xcode 27's libc++ warning flood in iOS builds (creative director, 2026-09-26)

- **Date:** 2026-09-26
- **Status:** Accepted (creative director asked to find out why the iPhone build failed, fix it and publish a working build)
- **Finding:** the Xcode log the creative director sent (Xcode 27 beta, iOS 27 SDK) contains no error. It ends with "Build stopped" after 265 seconds, at step 598 of 643 of Unity's IL2CPP compile, meaning the build was cancelled, not failed. The D-032 fixes worked: the IL2CPP script ran and compiled. The log did carry 1,644 copies of one warning, "The selected platform is no longer supported by libc++": Unity's IL2CPP compiles the game code with `-mios-version-min=11.0` whatever the app's iOS 15 target, and Xcode 27's libc++ warns for anything below its minimum. The flood hides real problems and makes Xcode look stuck.
- **Decision:** `IosXcodeFixups` adds `--compiler-flags="-Wno-#warnings"` to the IL2CPP arguments in the generated Xcode script phase. It silences only `#warning` directives in the IL2CPP compile; real compiler warnings and errors still show. Raising IL2CPP's own minimum was rejected: IL2CPP offers no option for it, and a second `-mios-version-min` would add an "overriding option" warning per file. The app's iOS 15 minimum is unchanged.
- **Verified on Windows (2026-09-26):** `IosXcodeFixupsTests` covers the new flag (added once, idempotent). The rebuilt Xcode project and the published zip contain the flag once, and all 20 build configurations still turn sandboxing and module verification off. EditMode 137/137, PlayMode 40/40; the Windows self-check is clean. The Xcode build itself can only run on a Mac.
- **Files updated:** `Docs/IMPLEMENTATION_STATUS.md`

### D-035: iOS build survives Xcode's "Update to recommended settings" (creative director, 2026-09-26)

- **Date:** 2026-09-26
- **Status:** Accepted (creative director reported "so many errors causing failure to build on iOS")
- **Finding:** the second Xcode log failed at `VerifyModule` for UnityFramework with the D-032 errors (`umbrella header ... does not include`, `double-quoted include`, `expected a type`). In that build the iOS minimum was 17.0, but the published project says 15.0. That means the project's settings had been changed in Xcode, most likely by accepting "Update to recommended settings", which turns module verification back on. The ~100 `ld: warning: no platform load command found in lib_burst_generated.a` lines are harmless warnings: Burst code is compiled on Windows, and it does not stop the build.
- **Decision:** `IosXcodeFixups` now also builds UnityFramework without a module (`DEFINES_MODULE = NO`). The verifier only checks frameworks that declare a module, so it has nothing to reject even if `ENABLE_MODULE_VERIFIER` is switched back on. Nothing needs the module: the app imports `<UnityFramework/UnityFramework.h>` directly and there is no Swift code. The IL2CPP script phase is also marked `alwaysOutOfDate = 1`, the same as unchecking "Based on dependency analysis". That removes the "will be run during every build" warning. Running on every build is intended, because IL2CPP tracks its own changes.
- **Advice to the creative director:** unzip a fresh copy, do not accept "Update to recommended settings", and change only the signing team.
- **Verified on Windows (2026-09-26):** `IosXcodeFixupsTests` covers both changes. The rebuilt project and the zip have `DEFINES_MODULE = NO` in all 4 UnityFramework configurations (none left at YES) and one `alwaysOutOfDate`. EditMode 138/138, PlayMode 40/40; the Windows self-check is clean. The Xcode build itself can only run on a Mac.
- **Files updated:** `Docs/IMPLEMENTATION_STATUS.md`

### D-036: iOS build no longer rewrites the signed UnityFramework (creative director, 2026-09-26)

- **Date:** 2026-09-26
- **Status:** Accepted (creative director reported a fifth failed iPhone build and sent the log)
- **Finding:** the third Xcode log shows real progress: the IL2CPP game code, UnityFramework and the app all compiled and linked, and UnityFramework's own target signed it successfully. The build then failed at the last step: when Xcode copied UnityFramework into the app, it ran `bitcode_strip` over the already signed binary ("not stripping binary because it is signed"), and re-signing the rewritten file failed with `internal error in Code Signing subsystem`. That copy step is the only thing that touched the file between the successful signature and the failed one. The warnings in the log (missing 1024x1024 App Store icon, 76x76 iPad icon notice, Burst "no platform load command") do not stop a build to a phone.
- **Decision:** `IosXcodeFixups` also sets `STRIP_BITCODE_FROM_COPIED_FILES = NO` in every build configuration. Bitcode has not existed since Xcode 14 and the project already has `ENABLE_BITCODE = NO`, so there is nothing to strip; the embedded copy now stays byte-identical to the file codesign produced, and Xcode only re-signs it with the team's profile. Considered and not done: turning off UnityFramework's own signing (more moving parts, and Xcode's signing UI can turn it back on).
- **Advice to the creative director:** unzip the new zip into a fresh folder, choose Product → Clean Build Folder once, change only the signing team, and do not accept "Update to recommended settings".
- **Verification limit:** this Windows machine cannot run Xcode or codesign, so the fix is checked in the generated `project.pbxproj` only; the next Mac build confirms it.
- **Files updated:** `Assets/Game/Editor/IosXcodeFixups.cs`, `Docs/IMPLEMENTATION_STATUS.md`

### D-037: Touch that works on iPhone, a pad that gives way to a controller, no corner panels (creative director, 2026-09-27)

- **Date:** 2026-09-27
- **Status:** Accepted (creative director played the first iPhone build that installed and reported: the on-screen pad did nothing and stayed up with a controller connected; the bottom-middle and bottom-right buttons on the piece-selection screen could not be selected, so no match could start; error pop-ups were hard to dismiss; the always-on info panels in the top corners should move to menus)
- **Findings:**
  - The UI's `Point` and `Click` actions were bound to the mouse only. A phone has no mouse, so no tap reached any button, and the on-screen stick and buttons (which work through taps) were decoration. D-032's claim that tapping worked had only been checked in the editor, where a mouse is always present.
  - On the piece-selection screen a greyed-out CONFIRM cannot hold controller focus, and BACK / LEAVE TABLE could only be reached through CONFIRM. With fewer than two pieces chosen, a controller could reach neither.
  - Development builds show Unity's pop-up developer console when anything logs an error; on a phone it covers the bottom of the screen and its close control is tiny.
- **Decision:**
  - `GameInput.inputactions` gains a `Touch` control scheme and binds UI `Point`/`Click` to `<Touchscreen>/touch*/position` / `<Touchscreen>/touch*/press` (and a pen's position/tip). Several fingers are tracked separately, so the stick and a button can be held together. Touch shows gamepad prompt names, which match the pad's A / RUN / VIEW / MENU labels.
  - The on-screen pad hides whenever a real controller is connected and returns when it is disconnected. The pad's own virtual gamepad does not count.
  - The piece-selection bottom row links around a greyed-out CONFIRM, so BACK is always reachable. CONFIRM stays disabled until two pieces are chosen, as `MATCH_UX_SPEC.md` requires.
  - The top-left (place, coins, wins) and top-right (controls) panels are gone from the world screen. The pause menu shows the place, coins and wins under PAUSED and the controls list below the buttons; HOW TO PLAY still shows the full help. The title screen keeps its controls line, and the area name still fades in when you enter a place.
  - On phones and tablets the pop-up developer console is switched off (`Debug.developerConsoleEnabled = false`); errors still go to the Xcode / device log.
- **Verified in the editor (2026-09-27):** new `TouchDeviceTests` run with a touchscreen only (no mouse or keyboard): tapping BEGIN, dragging the stick to walk, the pad hiding for a connected controller and returning, the MENU button opening the pause menu with place/coins/controls, and choosing two pieces, confirming and spinning by taps alone. A controller-only test reaches BACK while CONFIRM is greyed and then starts a match. The iPhone itself can only be checked by the creative director.
- **Screenshots:** `Docs/Screenshots/World/pause_menu.png`, `Docs/Screenshots/World/world_1_hearthmoor_no_corner_panels.png` (Windows build self-check).
- **Not changed:** no touch camera-look in first-person view yet (use VIEW for the overhead camera).
- **Files updated:** `Docs/TECHNICAL_ARCHITECTURE.md`, `Docs/IMPLEMENTATION_STATUS.md`, `Docs/WORLD_SPEC.md`

### D-038: The rest of the journey, the Grand Tournament, and wandering off the paths (creative director, 2026-09-28)

- **Date:** 2026-09-28
- **Status:** Accepted (explicit creative-director instruction)
- **Instruction (summary):** "flesh out the rest of the game, meaning we need areas/bosses to acquire all the final champion pieces, and then the grand tournament. build first, and do all tests at the end." And: "remove the invisible walls from everywhere except true no go areas. i want to be able to wander. not off cliffs or the edge of the map ... still a small sandbox, but its annoying to hit a barrier where it doesn't feel like there should be one."
- **Pieces still to win:** the Priest, the Assassin and the Warlock (the engine has had all seven since M0). Each gets a town and a champion; the source's reward order (Priest, then Assassin) is kept in the names but the towns can be played in any order.
  - **Lanternmere** on Mirrorwater (the lake the Willow Stream comes from), by the Stream Path: Mother Seraphine Vell, Priest + Archer (the source's first champion pair), Gold wheel, 60 coins, table at the end of a pier.
  - **Duskhollow**, a hollow in the pinewood, by the Hollow Path from Brindlecross: Silas Thorne, the Nightjar, Assassin + Mage, Gold wheel, 80 coins, table in a ring of standing stones. Darker, closer fog there.
  - **Ironbell** on the eastern moor, by the Bell Road from the North Road, with the Moor Track north to the Outpost: Magister Orlan Vey, Warlock + Engineer, Diamond wheel, 100 coins, table under the great bell.
  - Three challengers per town (Standard/Expert, Silver to Diamond wheels, 18-40 coins), three errands per town, two new stalls (Maudie's Lanterns; the Bellfoundry, which sells the Gold and a new Diamond wheel at 400).
- **The Grand Tournament at Crownhold** (north of Brindlecross, past the hall, on a walled hill):
  - Open to a champion of all five towns; Herald Aubrey enters the player. Three rounds in a row: Dame Ottilie Frane (Assassin + Priest, Diamond), Lord Casimir Vane (Warlock + Archer, Diamond), and Aldric Mourne, Grand Champion of the Realm (Platinum; his pair changes with each entry and never repeats twice in a row, after the source's randomised final opponent, deterministic so a save meets the final it was promised).
  - No stakes: purses of 60 / 90 / 250. A loss ends the run (enter again from round one any time, free); a tie replays the round; only the round the player is due to play counts (a rematch from the result screen is an exhibition).
  - Winning: Grand Champion of the Realm, the Platinum Wheel (never sold), and a short epilogue; the journey continues and the title can be defended again.
  - `GameFlow` holds the run (`TournamentRound`, `TournamentAttempts`); save version 3 stores it (older saves load outside the tournament).
- **Wandering (no more invisible walls):**
  - The old walkable area is now only the level ground (paths, villages, bridges, the pier, interiors) and still shapes the land. The player may go anywhere else within 40 m of it (`RoamArea`).
  - True no-go areas remain: water (stream and lake, except bridge and pier), ground steeper than 34° or a step steeper than 1.4 m per metre, and the map's edge (14 m inside the terrain).
  - Things block with colliders instead: buildings and walls (as before), and now fences, tree trunks, big rocks and standing stones.
  - Off the paths the feet follow the visible land.
- **Found while building:** the visible land had hidden creases where the nearest path changed (the "camera side is kept low" rule switched abruptly). Nobody could stand there before; once wandering was allowed they felt like invisible walls. The rule now measures "ground to the north" continuously; a test scans the land within reach for sudden steps.
- **Presentation:**
  - The overhead camera rises over any hill between it and the player.
  - Trees and buildings between the camera and the player drop to shadow-only until the player moves on.
  - Area names, music and ambience cover the new places. Music reuses the four CC0 tracks; Lanternmere adds the Fantasy Kingdom lakeshore ambience already in the project. Nothing new was downloaded.
- **Not changed:** match rules (`RULES_SPEC.md`), the table, controls.
- **Files updated:** `Docs/WORLD_SPEC.md`, `Docs/MILESTONES.md` (W3, W4), `Docs/IMPLEMENTATION_STATUS.md`, `AGENTS.md` (gate status)

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

