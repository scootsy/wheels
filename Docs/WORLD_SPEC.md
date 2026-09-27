# World Spec (W1 + W3)

Source of truth for the explorable world. W1 (D-025) is the valley slice; W3 (D-033) adds coins, stakes, errands,
stalls, charms and wheels, pack art and sound, the source's names, and the Stonemasons' Outpost. Match rules stay in
`RULES_SPEC.md`; the table itself stays in `MATCH_UX_SPEC.md`.

## Player fantasy

A 2.5D journey north and up: leave Hearthmoor, cross the Willow Stream, play for coins in Brindlecross, take on its
Champion, then climb the Quarry Path to the Stonemasons' Outpost, where the stakes and the wheels are bigger.
Villagers pay for small errands, stalls sell charms and wheels, and every town's Champion holds a new figurine.

## Presentation

- Fixed 3/4 follow camera (offset 0, 12.5, -13; FOV 40), or first person (FOV 70), switchable any time (D-027).
- **Land (D-033):** one smooth height field (`WorldGround`). Villages and paths are level or gently sloped; hills rise
  away from anywhere walkable and stay low on the camera (south) side; the Willow Stream runs in a carved bed; the
  highlands climb east to the Outpost plateau (12 m) with a sunken quarry pit and a lookout knoll. Rendered as a
  Unity Terrain painted grass / meadow / dirt / stone / rock by rule (paths dirt, plazas stone, steep ground rock).
- **Art (D-033):** Fantasy Kingdom houses, halls and trees; Polytope fruit and pine trees, rocks, ore, fences and
  flowers; ithappy furniture in the hall; KayKit characters for every person (recoloured clothes, hats, capes and a
  held item per person, animated idle / run / jump / sit / cheer / pick up). Everything is listed in
  `Assets/Game/Art/Look/WorldLook.asset`, built by **Tabletop → Art → Build World Look and Sounds**. Anything missing
  falls back to the primitive placeholders.
- Imported models from the creative director (D-026) still replace a person or building through `WorldArt.asset`.
- Sky (procedural), distance fog, filmic colour grading, soft vignette.
- A golden marker over a person means they play Wheels; a hammer marker means a stall. Name tags fade in as you
  approach and say CHALLENGER / BEATEN / STALL / HAS WORK / DELIVERY HERE / WAITING FOR YOU.
- **Sound (D-033):** music per place, ambience per place, footsteps, menu clicks, coins; table sounds for spins,
  locks, the lever, hits, the Bulwark, rank-ups, and the result. Pause menu: SOUND ON/OFF.

## Places

| Area | What is there |
|---|---|
| **Hearthmoor** (start, level 0) | Square with a well, five houses, orchard, fences. Gran Oddly (challenger, free table), Kit, Hollis, Marta, Old Tam. |
| **The North Road** | Rolling dirt road through woods; Wren's camp; the Willow Stream, crossed only on the bridge. Wren (challenger). |
| **Brindlecross** (level 2.5) | Stone plaza with a fountain, eight houses and a tower, two stalls, hives behind the chandlery. Mira, Tobin, Sister Halvey (challengers); Bram, Peg, Ada Pell (stall). |
| **The Champion's Hall** | Grand hall at the top of Brindlecross. Inside: Corvin Vale seated at the table, one empty chair. |
| **The Quarry Path** | Switchback path climbing east from Brindlecross's east side (sign by the plaza). |
| **Stonemasons' Outpost** (level 12) | Plateau with the foreman's lodge, Anvara's forge (stall), cook shelter, carving shed, huts, lookout tower; the quarry pit with a crane; the ledge table where Master Dorran Hale plays. Ilse Marrow, Kettle, Old Grist (challengers); Foreman Bask, Anvara, Rook. |

The player can walk only on the villages, road, camp, bridge, hall interior, quarry path, plateau, pit and knoll.

## Opponents, stakes and wheels (D-027, D-033)

Everyone in the valley plays the practice pair, the **Warrior** and the **Mage**. Each town's **Champion** adds a new
figurine; beating them wins it. Each opponent brings their own fifth wheel; the player brings their best one.

| Opponent | Where | AI | Figurines | Stake | Their wheel | Prize |
|---|---|---|---|---|---|---|
| Gran Oddly | Hearthmoor | Learner | Warrior + Mage | free (wins pay 3) | Copper | - |
| Wren | North Road camp | Learner | Warrior + Mage | 5 | Copper | - |
| Mira Tallow | Brindlecross | Standard | Warrior + Mage | 8 | Copper | - |
| Tobin Reed | Brindlecross | Standard | Warrior + Mage | 10 | Bronze | - |
| Sister Halvey | Brindlecross | Standard | Warrior + Mage | 10 | Bronze | - |
| **Corvin Vale** (champion) | Champion's Hall | Expert | Archer + Warrior | 25 | Bronze | **Archer** |
| Ilse Marrow | Outpost | Standard | Warrior + Archer | 15 | Silver | - |
| Kettle | Outpost | Standard | Mage + Archer | 12 | Bronze | - |
| Old Grist | Outpost | Expert | Warrior + Mage | 18 | Silver | - |
| **Master Dorran Hale** (champion) | Outpost ledge | Expert | Engineer + Warrior | 40 | Silver | **Engineer** |

## Coins, stakes and favours (D-033)

- A new journey starts with **20 coins**. The pause menu shows the place, coins and wins (D-037: no always-on corner panels).
- **Coins:** both put up the opponent's stake; the winner takes it (+stake or -stake). Ties change nothing.
- **Friendly table** (Gran): costs nothing; a win pays 3 coins. Always available, so a player can never be stuck.
- **Favour:** if you can't cover a stake, the opponent plays you for a favour. Win: half the stake (rounded up).
  Lose: no coins; a short scene of the chore you owe them.
- Each finished match settles; a rematch at the same table stakes again (a favour if the purse ran dry).
- Coins never go below 0.

## Errands (D-033)

Fourteen errands: Hearthmoor 4, the North Road 2, Brindlecross 5, the Outpost 3. Talk to someone marked
HAS WORK, accept (the choice shows the reward), and:

- **Delivery:** carry the item to the named person; they pay on the spot.
- **Fetch:** a sparkling item appears where they said; pick it up (Interact), take it back, they pay.
- One errand at a time per giver; some unlock after another (Wren's lantern after her hammer).
- Pause → ERRANDS AND SATCHEL lists errands under way, charms owned and your wheel.

## Stalls, charms and wheels (D-033)

- **Ada's Stall** (Brindlecross) and **Anvara's Forge** (Outpost). Talk → BROWSE → the list; BUY with Enter / A.
- **Charms** (one per match, used up when that match ends; chosen when you sit down):
  Crown Tonic 8 (Crown starts at 12), Bag of Mortar 10 (Bulwark 2), Spark Flint 10 (both figurines start with 2
  energy), Square Medal 14 (left figurine starts Silver), Diamond Medal 14 (right figurine starts Silver; forge only).
- **Wheels** (kept forever; you always bring your best): Bronze 35, Silver 90 (forge), Gold 200 (forge).
- Balance: charms are head starts, never extra spins or hidden odds; opponents up the mountain bring better wheels.

## Flow

1. **Title** (first launch only): BEGIN YOUR JOURNEY / CONTINUE JOURNEY, PRACTICE TABLE, QUIT.
2. **Explore**: walk; the nearest usable thing shows a prompt (`[E] Talk to Wren`, `[E] Pick up Wren's hammer`).
3. **Talk**: an errand hand-in comes first; stalls offer BROWSE; challengers end on PLAY FOR n / PLAY (FREE) /
   PLAY FOR A FAVOUR, ANY WORK? (if they have an errand), NOT NOW. Villagers with work end on I'LL DO IT (reward) / NOT NOW.
4. **Charm**: if you own charms, a list offers them (or JUST PLAY) before you sit down.
5. **Champions**: sit in the empty chair (the hall, or the ledge table) → PLAY / STAND UP.
6. **Table**: straight to figurine selection. The result screen shows the stake, coins won or lost, and any charm used.
7. **Return**: back where you stood; the opponent reacts, then coins, favour and prize pages.
8. **Menu** (Esc / Start): Resume, Errands and Satchel, Your Deck, View, Sound, How to play, Practice table, Save and quit.

## Controls

| Action | Keyboard / mouse | Gamepad | Touch |
|---|---|---|---|
| Walk | WASD / arrow keys | Left stick / D-pad | on-screen stick |
| Sprint (hold) | Shift | LT or L3 | RUN |
| Jump | Space | A (when nobody is close enough to talk to) | A |
| Look (first person) | Mouse | Right stick | - |
| Switch overhead / first person | V | Y | VIEW |
| Your deck | I | X | - |
| Talk / use / pick up / advance | E (Enter/Space on focused buttons) | A | A / tap |
| Menu | Esc | Start | MENU |
| Close a list or conversation | Esc | B | tap CLOSE / NOT NOW |

## Deliberate limits

- The journey saves itself (save version 2: coins, satchel, errands). A match in progress is not saved.
- Two towns and one highland outpost; the next champion (Priest or Assassin) waits for a later area.
- No combat, quests with branching, schedules or day/night. Stalls sell only what is listed above.
