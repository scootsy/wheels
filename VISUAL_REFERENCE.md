# Wheels Visual Reference Pack

**Companion to:** [`RULES_SPEC.md`](RULES_SPEC.md)  
**Role:** Spatial and presentation reference for a zero-context implementation  
**Research cutoff:** 2026-09-22  
**Status:** Curated reference set; not a license to reuse the original game's art

## How to use this pack

Use these images to understand the table's visual hierarchy, the relationship between reels and figurines, and the readable states a playable implementation must communicate. Use `RULES_SPEC.md` for all mechanics, numeric values, sequencing, and edge cases.

Screenshots are **authoritative for visible relationships only**. Do not infer hidden timing, probabilities, target rules, or numeric formulas from a single frame. Any original project should use its own names, artwork, iconography, layout treatment, animations, and audio.

## A. Board states

![Seven curated table states](wheels_visual_reference/board_states_contact_sheet.jpg)

| File | State captured | What a zero-context builder should learn |
|---|---|---|
| `06_full_board.jpg` | Clean full-board view | Two mirrored sides; two figurines per side; one Crown and one Bulwark lane per side; five reels sit nearest their owner. |
| `01_locked_reels.jpg` | Spin phase with selectable reels | Five independently lockable columns, a visible spin counter, and a clear distinction between selected/locked and rerollable faces. |
| `02_resolved_reels.jpg` | Finalized reel result | Resource faces remain visible while the table prepares to resolve them; the player can audit the result. |
| `03_attack_state.jpg` | Action/attack in progress | A unit's activation is staged on the board while the state it changes remains visible. |
| `04_evolution_state.jpg` | Rank-up state | Figurine material/color changes communicate rank; the XP track remains spatially attached to that figurine. |
| `05_bulwark_state.jpg` | Both Bulwarks raised | Barrier height is a literal, central visual layer between each side's figurines and Crown. |
| `07_late_match_board.jpg` | Mixed-rank late match | Each side can have different unit ranks and action-meter progress at the same time. |

### Board anatomy checklist

A new implementation must make all of the following readable without opening a help screen:

1. Opponent side versus player side.
2. Left/Channel A unit versus right/Channel B unit.
3. Crown HP for each side.
4. Bulwark height for each side.
5. Each unit's current rank, XP, action cost/progress, Crown damage, and Bulwark damage.
6. Five current reel faces and which reels are locked.
7. Spins remaining in the current round.
8. Which action is currently resolving and what changed.
9. Final match result: win, loss, or tie.

## B. Figurines

The six base-game figurines are isolated below so their silhouettes and podium treatment can be understood separately from the full table.

![Six isolated figurines](wheels_visual_reference/figurines_contact_sheet.jpg)

| Figurine | Visual identity in the reference | Mechanical identity; rules remain in `RULES_SPEC.md` |
|---|---|---|
| Warrior | Sword, round shield, armored stance | Straight ground attacker; high damage, fully stopped by any nonzero Bulwark. |
| Mage | Wide hat and staff | Two sequential projectiles; the second clears every normal Bulwark height. |
| Archer | Bow held across the body | Height-three shot; clears low Bulwarks and is stopped by tall ones. |
| Engineer | Stocky silhouette with tool/cannon cues | Damages Bulwark efficiently, then raises its own side's Bulwark. |
| Priest | Robed, upright support silhouette | Heals Crown and advances the partner unit. |
| Assassin | Hooded, crouched silhouette with blades | Direct Crown damage plus delay; resolves at high priority. |

Individual reference crops:

- ![Warrior figurine](wheels_visual_reference/figurines/warrior.jpg)
- ![Mage figurine](wheels_visual_reference/figurines/mage.jpg)
- ![Archer figurine](wheels_visual_reference/figurines/archer.jpg)
- ![Engineer figurine](wheels_visual_reference/figurines/engineer.jpg)
- ![Priest figurine](wheels_visual_reference/figurines/priest.jpg)
- ![Assassin figurine](wheels_visual_reference/figurines/assassin.jpg)

The cropped images include the podium, action rod, and compact stat plaques because those elements are part of how the table communicates unit state. They are reference crops of the unmodified screenshots in `wheels_visual_reference/figurines/*_source.jpg`.

## C. Zero-context implementation audit

### Required standard

Yes: the complete implementation package should let a competent agent build the game without prior knowledge of *Sea of Stars*, without watching a video, and without inventing gameplay rules. “Without context” should mean the agent can:

1. implement a deterministic headless match;
2. build the complete player interaction loop;
3. render every required state clearly;
4. test normal play and edge cases;
5. identify every deliberate implementation decision versus every unresolved reference-game question.

### Current verdict

The three-document package is now sufficient to begin a zero-context first-playable implementation: `RULES_SPEC.md` defines the deterministic engine, this document supplies the visual evidence, and `MATCH_UX_SPEC.md` defines the complete player interaction and presentation contract.

| Area | Current readiness | Why |
|---|---|---|
| Deterministic simulation | Strong | State, commands, events, resolution order, unit data, and tests are explicit. |
| Content configuration | Ready | Exact values, concrete configuration examples, and validation requirements are supplied. |
| Board comprehension | Addressed by this pack | The rules text did not previously show spatial hierarchy or readable table states. |
| Player interaction flow | Ready | `MATCH_UX_SPEC.md` defines every state from setup through result and rematch. |
| Presentation timing | Ready | Event mapping, ordering, timing bounds, acceleration, pause, and reduced-motion behavior are explicit. |
| Input/accessibility | Ready | Keyboard, controller, pointer, focus, scaling, reduced motion, and non-color cues are specified. |
| Failure/illegal commands | Ready | Structured rejection behavior and player-facing feedback are specified. |
| Acceptance definition | Ready | Engine tests and end-to-end first-playable completion criteria are explicit. |
| Open reference questions | Explicit | The spec correctly labels black-box unknowns instead of inventing certainty. |

### Remaining deliberate unknowns

Only the black-box reference-game questions listed in `RULES_SPEC.md` Section 15 remain unresolved. Each has an explicit prototype behavior, so none blocks implementation.

## D. Source and rights ledger

These screenshots are retained for private design research and implementation reference. Original game art and UI remain the property of their respective rights holders. Do not ship, publish, or train project artwork from these images.

- Gamer Guides, “How to Unlock the Wheels Minigame in Sea of Stars”: <https://www.gamerguides.com/sea-of-stars/guide/minigames/wheels/how-to-unlock-the-wheels-minigame-in-sea-of-stars>
- Gamer Guides, “Best Heroes to Use in Wheels in Sea of Stars”: <https://www.gamerguides.com/sea-of-stars/guide/minigames/wheels/best-heroes-to-use-in-wheels-in-sea-of-stars>
- Sportskeeda, “Sea of Stars complete Wheels minigame guide”: <https://www.sportskeeda.com/esports/sea-stars-complete-wheels-minigame-guide>
- TechRaptor, “Sea of Stars Wheels Guide”: <https://techraptor.net/gaming/guides/sea-of-stars-wheels-guide-how-to-win-and-all-rewards>

## E. File inventory

- `wheels_visual_reference/board_states/`: seven full screenshots at distinct gameplay states.
- `wheels_visual_reference/figurines/`: six isolated figurine crops, the six unmodified source screenshots, and a full-roster screenshot.
- `wheels_visual_reference/board_states_contact_sheet.jpg`: compact board-state overview.
- `wheels_visual_reference/figurines_contact_sheet.jpg`: compact figurine overview.
