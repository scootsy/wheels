# World Spec (W1 + W3 + W4)

Source of truth for the explorable world. W1 (D-025) is the valley slice; W3 (D-033) adds coins, stakes, errands,
stalls, charms and wheels, pack art and sound, the source's names, and the Stonemasons' Outpost. W4 (D-038) adds the
rest of the journey: Lanternmere, Duskhollow and Ironbell, whose champions hold the Priest, the Assassin and the
Warlock, the Grand Tournament at Crownhold, and free wandering off the paths. Match rules stay in `RULES_SPEC.md`; the
table itself stays in `MATCH_UX_SPEC.md`.

## Player fantasy

A 2.5D journey north and up: leave Hearthmoor, cross the Willow Stream, play for coins in Brindlecross, take on its
Champion, then climb the Quarry Path to the Stonemasons' Outpost, where the stakes and the wheels are bigger.
Follow the stream to the lake at Lanternmere, go down into the pinewood hollow at Duskhollow, and up onto the moor at
Ironbell. Villagers pay for small errands, stalls sell charms and wheels, and every town's Champion holds a new
figurine. Hold all five champion titles and the Grand Tournament at Crownhold opens: three rounds in a row for the
title of Grand Champion of the Realm.

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
| **The Stream Path** (W4) | Forks west off the North Road just north of the bridge and follows the Willow Stream upstream (sign at the fork). |
| **Lanternmere** (W4, level 1.2) | Lake village on the north shore of Mirrorwater, the lake the stream comes from: chapel, lanternhouse, net loft, cottage, boathouse, lanterns along a beach, boats, lilies and reeds; a pier with Mother Seraphine's table at its end. Finn Harrow, Nell Cotter, Brother Aldous (challengers); Old Maudie (stall), Pim. |
| **The Hollow Path** (W4) | Climbs out of Brindlecross's north-west corner into the pinewood. |
| **Duskhollow** (W4, level 0.2) | A hollow sunk into the old pinewood, with darker, closer fog: herb hut, forager's hut, charcoal kiln, lodge, mushrooms, dead trees; a ring of standing stones (open to the south) where Silas Thorne, the Nightjar, plays; the great tree behind it. Moth, Grey Agathe, Corwen Ash (challengers); Hob. |
| **The Bell Road** (W4) | Forks east off the North Road and climbs onto the eastern moor (sign at the fork). |
| **Ironbell** (W4, level 12) | Bell town on the moor: bell tower, clockworks, bellfoundry (stall), inn, cottages, a gate; the great bell in its timber frame with Magister Orlan Vey's table beneath it; standing stones and a flock of sheep on the moor to the south. Tilda Brass, Captain Rusk, Widow Callow (challengers); Oskar Bell (stall), Wenna. |
| **The Moor Track** (W4) | North from Ironbell across the moor to the Stonemasons' Outpost. |
| **The Tourney Road** (W4) | Leaves Brindlecross's north-east corner, passes east of the Champion's Hall, and climbs to Crownhold, with pennants. |
| **Crownhold** (W4, level 6.5) | Walled tournament grounds on a hill (low front wall, tall side and back walls, towers, a gate): the keep, two towers, the arena ring with stands and banners, the trophy on its pedestal, and the tournament table. Herald Aubrey; Dame Ottilie Frane, Lord Casimir Vane, Aldric Mourne (the tournament); Pip Lark, Maren Hale, Old Colm. |

## Wandering (D-038)

- The paths, villages, bridges, the pier and the hall's interior are level ground, as before. Off them, the player can
  now walk anywhere on the land within about 40 m of a path or village, over the hills and through the woods.
- Real barriers only: water (the stream and the lake; the bridge and the pier cross them), ground steeper than 34°
  (the player can't climb it or walk off it), the edge of the map, and solid things: buildings, walls, fences, tree
  trunks, big rocks, stalls, people.
- Off the paths the player's feet follow the visible land. The overhead camera rises over a hill that would hide the
  player, and trees or buildings between the camera and the player keep only their shadow until the player moves on.
- Some errands hide their item off the paths (Pim's boat across the lake, Hob's axe head up the slope, Captain Rusk's
  rope by the standing stones, Tilda's spring beside the Bell Road).

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
| Finn Harrow | Lanternmere | Standard | Engineer + Mage | 18 | Silver | - |
| Nell Cotter | Lanternmere | Expert | Archer + Warrior | 20 | Silver | - |
| Brother Aldous | Lanternmere | Standard | Warrior + Engineer | 22 | Gold | - |
| **Mother Seraphine Vell** (champion) | Lanternmere pier | Expert | Priest + Archer | 60 | Gold | **Priest** |
| Moth | Duskhollow | Standard | Mage + Archer | 25 | Gold | - |
| Grey Agathe | Duskhollow | Expert | Engineer + Archer | 28 | Gold | - |
| Corwen Ash | Duskhollow | Expert | Warrior + Engineer | 32 | Gold | - |
| **Silas Thorne, the Nightjar** (champion) | Duskhollow stones | Expert | Assassin + Mage | 80 | Gold | **Assassin** |
| Tilda Brass | Ironbell | Expert | Engineer + Mage | 30 | Gold | - |
| Captain Rusk | Ironbell | Expert | Archer + Priest | 35 | Diamond | - |
| Widow Callow | Ironbell | Expert | Assassin + Warrior | 40 | Diamond | - |
| **Magister Orlan Vey** (champion) | under Ironbell's great bell | Expert | Warlock + Engineer | 100 | Diamond | **Warlock** |

The towns can be visited in any order. Every figurine except the starting pair is held by exactly one town champion.

## The Grand Tournament at Crownhold (D-038)

- **Entry:** Herald Aubrey enters any player who holds all five town champion titles (Corvin, Dorran, Seraphine, the
  Nightjar, Orlan). Anyone can walk into Crownhold before that; the Herald says which champions are still to beat.
- **Three rounds in a row** at the tournament table in the arena. The opponent for your round sits at the table; the
  others wait at the edge of the ring. No stakes: each win pays a purse.

| Round | Opponent | Figurines | Wheel | Purse |
|---|---|---|---|---|
| 1 | Dame Ottilie Frane, the Iron Rose | Assassin + Priest | Diamond | 60 |
| 2 | Lord Casimir Vane | Warlock + Archer | Diamond | 90 |
| Final | Aldric Mourne, Grand Champion of the Realm | a different pair each entry (never the same twice in a row) | Platinum | 250 |

- **Lose a round:** out of the tournament; talk to the Herald to enter again from round one (free, any time).
  **Tie:** the round is played again. A rematch at the same table from the result screen is an exhibition game.
- **Win the final:** Grand Champion of the Realm, Aldric's **Platinum Wheel** (never sold), and a short epilogue. The
  journey carries on: every table stays open, and the title can be defended at Crownhold again.
- The run (round and number of entries) is saved (save version 3).

## Coins, stakes and favours (D-033)

- A new journey starts with **20 coins**. The pause menu shows the place, coins and wins (D-037: no always-on corner panels).
- **Coins:** both put up the opponent's stake; the winner takes it (+stake or -stake). Ties change nothing.
- **Friendly table** (Gran): costs nothing; a win pays 3 coins. Always available, so a player can never be stuck.
- **Favour:** if you can't cover a stake, the opponent plays you for a favour. Win: half the stake (rounded up).
  Lose: no coins; a short scene of the chore you owe them.
- Each finished match settles; a rematch at the same table stakes again (a favour if the purse ran dry).
- Coins never go below 0.

## Errands (D-033)

Twenty-three errands: Hearthmoor 4, the North Road 2, Brindlecross 5, the Outpost 3, Lanternmere 3, Duskhollow 3,
Ironbell 3 (D-038). Talk to someone marked
HAS WORK, accept (the choice shows the reward), and:

- **Delivery:** carry the item to the named person; they pay on the spot.
- **Fetch:** a sparkling item appears where they said; pick it up (Interact), take it back, they pay.
- One errand at a time per giver; some unlock after another (Wren's lantern after her hammer).
- Pause → ERRANDS AND SATCHEL lists errands under way, charms owned and your wheel.

## Stalls, charms and wheels (D-033)

- **Ada's Stall** (Brindlecross), **Anvara's Forge** (Outpost), **Maudie's Lanterns** (Lanternmere: charms and both
  medals) and **The Bellfoundry** (Ironbell: charms, medals, Gold and Diamond wheels). Talk → BROWSE → the list; BUY
  with Enter / A.
- **Charms** (one per match, used up when that match ends; chosen when you sit down):
  Crown Tonic 8 (Crown starts at 12), Bag of Mortar 10 (Bulwark 2), Spark Flint 10 (both figurines start with 2
  energy), Square Medal 14 (left figurine starts Silver), Diamond Medal 14 (right figurine starts Silver; forge only).
- **Wheels** (kept forever; you always bring your best): Bronze 35, Silver 90 (forge), Gold 200 (forge, bellfoundry),
  Diamond 400 (bellfoundry), Platinum (the Grand Tournament's prize, never sold).
- Balance: charms are head starts, never extra spins or hidden odds; opponents up the mountain bring better wheels.

## Flow

1. **Title** (first launch only): BEGIN YOUR JOURNEY / CONTINUE JOURNEY, PRACTICE TABLE, QUIT.
2. **Explore**: walk; the nearest usable thing shows a prompt (`[E] Talk to Wren`, `[E] Pick up Wren's hammer`).
3. **Talk**: an errand hand-in comes first; stalls offer BROWSE; challengers end on PLAY FOR n / PLAY (FREE) /
   PLAY FOR A FAVOUR, ANY WORK? (if they have an errand), NOT NOW. Villagers with work end on I'LL DO IT (reward) / NOT NOW.
4. **Charm**: if you own charms, a list offers them (or JUST PLAY) before you sit down.
5. **Champions**: sit in the empty chair (the hall, the ledge table, the pier, the stones, under the great bell) →
   PLAY / STAND UP. At Crownhold: talk to the Herald → ENTER THE TOURNAMENT, then sit at the tournament table →
   PLAY ROUND n / PLAY THE FINAL.
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

- The journey saves itself (save version 3: coins, satchel, errands, the tournament run). A match in progress is not saved.
- Five towns, one tournament; all seven figurines can be won. Music reuses the four CC0 tracks (Duskhollow and
  Crownhold get their own mix of them); Lanternmere adds the pack's lakeshore ambience.
- No combat, quests with branching, schedules or day/night. Stalls sell only what is listed above.
