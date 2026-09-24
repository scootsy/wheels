# World Slice Spec (W1)

Source of truth for the explorable world approved by the creative director on 2026-09-24 (D-025). Match rules stay in `RULES_SPEC.md`; the table itself stays in `MATCH_UX_SPEC.md`.

## Player fantasy

A 2.5D walk north: leave your home village, cross open country, arrive in a bigger village full of people who play Reels, then walk into the great hall and sit down across from the town champion.

## Presentation

- Fixed 3/4 top-down follow camera (offset 0, 17, -17; FOV 42) that eases after the player. No camera control.
- Placeholder art only: primitive shapes on real URP material assets (never the built-in default material, see D-024). Camera-facing name tags.
- A gem over a person's head means they will play Reels with you. After you beat them, their tag says **BEATEN**.
- The area name fades in when you enter a new area.

## Places (south to north)

| Area | What is there |
|---|---|
| **Hearthmoor** (start) | Plaza with a well, five houses, fences, trees. Gran Oddly (challenger), Kit and Hollis (villagers). Sign pointing to the North Road. |
| **North Road** | Winding dirt road through woods, Wren's camp with a campfire just off the road, a stream crossed by a single bridge. Wren (challenger). |
| **Brindlecross** | Plaza with a fountain, six houses, two market stalls, lamps, benches. Mira Tallow, Tobin Reed, Sister Halvey (challengers); Bram and Peg (villagers). |
| **Champion's Hall** | Large hall at the north end of Brindlecross. Inside: one table, the champion Corvin Vale seated, one empty chair. |

The player can only walk on the villages, the road, the camp, the bridge, and the hall interior. Woods, water, and the space behind the hall block movement.

## Opponents

Every opponent plays the normal match with Copper reels. The player always chooses from Striker and Caster (the gate's player-unit limit still applies); opponents may field other reference units.

| Opponent | Where | AI | Units |
|---|---|---|---|
| Gran Oddly | Hearthmoor | Learner | Striker + Caster |
| Wren | North Road camp | Learner | Ranger + Striker |
| Mira Tallow | Brindlecross | Standard | Mason + Striker |
| Tobin Reed | Brindlecross | Standard | Ranger + Caster |
| Sister Halvey | Brindlecross | Standard | Mender + Striker |
| **Corvin Vale** (champion) | Champion's Hall | Expert | Shade + Caster |

The champion is not locked behind the other wins; he mentions how many you have beaten.

## Flow

1. **Title** (first launch only): BEGIN YOUR JOURNEY, PRACTICE TABLE, QUIT.
2. **Explore**: walk; the nearest usable thing shows a prompt (`[E] Talk to Wren`).
3. **Talk**: dialogue pages with CONTINUE; challengers end on CHALLENGE / NOT NOW. Villagers just talk.
4. **Champion**: go through the hall door, walk to the empty chair, press Interact to sit. The camera moves to the table; Corvin speaks; CHALLENGE / STAND UP.
5. **Challenge**: fade to the table scene, already on unit selection (no practice setup screen). LEAVE TABLE or Pause → Exit returns without a result.
6. **Return**: after the result screen, RETURN TO THE VILLAGE puts you back where you stood, and the opponent reacts (win or lose line). A win is counted in the HUD.
7. **Menu** (Esc / Start): Resume, How to play, Practice table (the original free match), Quit.

## Controls

| Action | Keyboard / mouse | Gamepad |
|---|---|---|
| Walk | WASD / arrow keys | Left stick / D-pad |
| Talk / use / advance | E (Enter/Space on focused buttons) | A |
| Menu | Esc | Start |
| Close menu / end a conversation | Esc | B (choice pages: pick NOT NOW / STAND UP) |

## Deliberate limits

- No saving: wins last until you quit the game.
- No rewards, collection, currency, quests, schedules, or additional player units.
- Placeholder art and no audio.
