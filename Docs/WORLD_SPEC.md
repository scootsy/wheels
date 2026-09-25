# World Slice Spec (W1)

Source of truth for the explorable world approved by the creative director on 2026-09-24 (D-025). Match rules stay in `RULES_SPEC.md`; the table itself stays in `MATCH_UX_SPEC.md`.

## Player fantasy

A 2.5D walk north: leave your home village, cross open country, arrive in a bigger village full of people who play Reels, then walk into the great hall and sit down across from the town champion.

## Presentation

- Fixed 3/4 top-down follow camera (offset 0, 17, -17; FOV 42) that eases after the player, or first person (FOV 70, mouse/right-stick look), switchable any time (D-027).
- Placeholder art: primitive shapes on real URP material assets (never the built-in default material, see D-024). Camera-facing name tags that fade in as you approach (D-027); challenge gems stay visible from afar.
- Imported models from the creative director replace placeholders slot by slot through `Assets/Game/Art/WorldArt.asset` (D-026, `ART_WORKFLOW.md`). Currently: Wren, Mira Tallow, the Brindlecross Inn and Chandlery, and the Hearthmoor Cottage (mushroom house).
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

## Opponents and pieces (D-027)

Everyone in a town plays the same practice pair, the **Striker** (knight) and the **Caster** (mage). Each town's **Champion** adds one more piece; beating him wins it for your deck. Every opponent uses Copper reels.

| Opponent | Where | AI | Units | Prize |
|---|---|---|---|---|
| Gran Oddly | Hearthmoor | Learner | Striker + Caster | - |
| Wren | North Road camp | Learner | Striker + Caster | - |
| Mira Tallow | Brindlecross | Standard | Striker + Caster | - |
| Tobin Reed | Brindlecross | Standard | Striker + Caster | - |
| Sister Halvey | Brindlecross | Standard | Striker + Caster | - |
| **Corvin Vale** (champion) | Champion's Hall | Expert | **Ranger** + Striker | **Ranger** |

Planned for later towns (not built): the next champion holds the Mason, and so on through the remaining pieces. The champion is not locked behind the other wins; he mentions how many you have beaten.

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
| Sprint (hold) | Shift | LT or L3 |
| Jump | Space | A (when nobody is close enough to talk to) |
| Look (first person) | Mouse | Right stick |
| Switch overhead / first person | V | Y |
| Your deck | I | X |
| Talk / use / advance | E (Enter/Space on focused buttons) | A |
| Menu | Esc | Start |
| Close menu / end a conversation | Esc | B (choice pages: pick NOT NOW / STAND UP) |

## Deliberate limits

- The journey saves itself (D-027): CONTINUE JOURNEY on the title resumes it. A match in progress is not saved.
- Pieces are the only reward so far: no currency, shops, quests or schedules.
- Placeholder art and no audio.
