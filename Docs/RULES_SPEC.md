# Tabletop Match Rules Specification

**Document role:** Source of truth for the deterministic match simulation  
**Status:** Research-complete draft for implementation and black-box verification  
**Version:** 0.1.0  
**Research cutoff:** 2026-09-22  

**Implementation companions:** [`MATCH_UX_SPEC.md`](MATCH_UX_SPEC.md) defines screens, controls, presentation, accessibility, configuration examples, and first-playable acceptance. [`VISUAL_REFERENCE.md`](VISUAL_REFERENCE.md) documents board anatomy, representative table states, and isolated figurine references. Neither overrides mechanical rules in this document.

## 1. Purpose

This document does two separate jobs:

1. It records the rules of the *Sea of Stars* minigame *Wheels* as accurately as public evidence allows.
2. It defines a clean, implementable baseline for the project's first playable match.

The reference game is not fully documented by Sabotage Studio, and its source code is not public. Therefore:

- **CONFIRMED** means multiple sources or direct in-game documentation agree.
- **HIGH CONFIDENCE** means one detailed source agrees with observed gameplay and no source conflicts.
- **IMPLEMENTATION DECISION** means public evidence does not settle the behavior, so this project defines it explicitly.
- **OPEN** means the behavior should be tested against the reference game before claiming exact parity.

The project must never silently replace an OPEN rule with an assumption. Any assumption used in code must be recorded here and in `DECISIONS.md`.

## 2. IP and Naming Boundary

> **D-033 (2026-09-26):** the creative director states that a licensing agreement is now in place permitting the
> source's names and assets. Player-facing text therefore uses the source's terms (Wheels, Wheel, Crown, Bulwark,
> figurine, Square / Diamond energy, Warrior, Mage, Archer, Engineer, Assassin, Priest, Warlock). Code ids stay
> neutral (`striker`, `caster`, `ranger`, `mason`, `shade`, `mender`, `hexer`; Channel A/B; Barrier), so saves,
> replays and tests are unchanged. The world's towns and people remain original. The paragraph below is the
> pre-licence rule, kept for history.

The mechanics may be used as a design reference. Player-facing names, art, characters, setting, dialogue, UI, audio, icon designs, and presentation must remain original unless a separate written license explicitly permits their use.

Use neutral internal terminology in production code:

| Reference term | Neutral internal term |
|---|---|
| Wheels | Match game |
| Crown | Core |
| Bulwark | Barrier |
| Hero / Figurine | Unit / Piece |
| Square energy | Channel A |
| Diamond energy | Channel B |
| Wheel | Reel |
| Champion | Opponent |

The reference names remain in this document because they are necessary to audit the research.

## 3. Design Pillars Derived from the Reference Game

The match is built around five interacting decisions:

1. **Push-your-luck rerolling:** Commit useful reel results or gamble for a stronger combination.
2. **Competing demands:** The same five reels must fund offense, defense, and in-match growth.
3. **Asymmetric units:** Each unit changes the value of symbols and the opponent's likely response.
4. **Visible timing:** Action rods expose how close every unit is to acting.
5. **Short-term versus long-term value:** Immediate energy can win now; XP can create a stronger unit or direct-damage bomb later.

Any redesign should preserve these tensions even if individual values change.

## 4. Reference Match Overview

### 4.1 Objective

- Two sides compete.
- Each Crown begins at **10 HP**.
- Reduce the opposing Crown to **0 HP**.
- Victory is checked only after the entire round resolves.
- If both Crowns are at 0 HP at the final check, the match is a **tie**.
- A Crown at 0 HP does not stop remaining actions in that round.
- The Priest can heal a Crown before the final check, including one that was reduced to 0 earlier in the round.
- Crown HP is normally capped at **10**, but Priest healing can raise it to a hard cap of **12**.

**Confidence:** CONFIRMED. [S1][S2]

### 4.2 Setup

- Each side selects **two** figurines.
- The left figurine is assigned to Square/Channel A energy.
- The right figurine is assigned to Diamond/Channel B energy.
- The player can see the opponent's selected figurines before confirming their own pair.
- Both figurines begin each match at **Bronze** rank with **0 XP** and an empty action meter.
- The Crown begins at 10 HP and the Bulwark at 0.

**Confidence:** CONFIRMED, except the explicit 0-energy initialization is HIGH CONFIDENCE. [S1]

### 4.3 Round Loop

Each round has two conceptual phases:

1. **Spin phase**
2. **Resolution phase**

The player receives up to **three spins** of five reels.

- Spin 1 always spins all five reels.
- After spin 1, any reel can be locked.
- Spin 2 rerolls only unlocked reels.
- After spin 2, any reel may be locked or unlocked.
- Spin 3 rerolls only unlocked reels and then finalizes the result.
- Locking all five reels after spin 1 or spin 2 finalizes the result early.
- The original single-player game imposes no decision timer.

**Confidence:** CONFIRMED. [S1][S2][S3]

## 5. Reel Faces and Symbol Evaluation

### 5.1 Face Types

A reel face can contain:

- one, two, or three Square/Channel A symbols;
- one, two, or three Diamond/Channel B symbols;
- one, two, or three Hammer symbols;
- a Square or Diamond face with a starry/blue XP background;
- or a blank.

An XP face contributes its printed symbol quantity to energy and exactly **1 XP** to the matching unit. A double-symbol XP face still grants only 1 XP.

Notation used below:

- `S`, `SS`, `SSS`: 1, 2, or 3 Square symbols
- `D`, `DD`, `DDD`: 1, 2, or 3 Diamond symbols
- `H`, `HH`, `HHH`: 1, 2, or 3 Hammers
- `+`: the face also grants 1 XP
- `-`: blank

### 5.2 Resource Formula

Square energy, Diamond energy, and Hammer building use the same formula independently:

```text
resourceGained(symbolCount) = max(0, symbolCount - 2)
```

| Matching symbols | Resource gained |
|---:|---:|
| 0 | 0 |
| 1 | 0 |
| 2 | 0 |
| 3 | 1 |
| 4 | 2 |
| 5 | 3 |
| 6 | 4 |
| 7 | 5 |

All printed symbols are totaled before subtracting two. For example, `SS + S` is three Squares and grants 1 Channel A energy.

XP ignores this threshold. A single matching XP face grants 1 XP even if fewer than three matching symbols are present.

**Confidence:** CONFIRMED. [S1][S2][S3][S4]

### 5.3 Exact Player Reel Distributions

Each physical reel has eight faces. Face order should be preserved for visual fidelity even though a uniform digital randomizer needs only the face multiset.

#### Fixed reels 1–4

| Reel | Faces in physical order |
|---|---|
| 1 | `S, D, S, S+, D, H, DD+, H` |
| 2 | `S+, D, SS, D+, S, H, DD, HH` |
| 3 | `S+, D, D+, S, D, HH, SS, HH` |
| 4 | `S, D, S+, D, HH, S, D+, HH` |

#### Progressive fifth reel

| Tier | Faces in physical order |
|---|---|
| Copper | `S, D, H, -, -, S, D, -` |
| Bronze | `S, D, H, -, -, S, D, HH` |
| Silver | `S, D, H, -, D, SS+, D, HH` |
| Gold | `S, DD+, H, S, D, SS+, D, HH` |
| Diamond | `S, DD+, HH, SS, DD, SS+, D, HH` |
| Platinum | `S, DD+, HHH, SS+, DD+, SS+, D, HH` |

The first four reels never change. Collection progression replaces only reel 5.

**Confidence:** CONFIRMED. The same exact distributions appear in two independently maintained rule references. [S1][S2]

### 5.4 Randomness

For player reels, each of the eight physical faces is treated as equally likely unless black-box testing proves otherwise.

The reference game's NPC outcomes do **not** appear to be drawn from the same fixed eight-face reels. Detailed community documentation states that Champion panels are selected from a less constrained random distribution. Exact opponent probabilities are not publicly documented.

For this project:

- Use a deterministic seeded PRNG.
- Each reel draw must be reproducible from the match seed and draw index.
- Human and AI sides use the same legal reel definitions in the first prototype.
- Do not reproduce undocumented NPC roll advantages.

**Confidence:** Player-face data is CONFIRMED. Equal face probability is HIGH CONFIDENCE. Opponent distribution is confirmed as non-identical, but its probabilities are OPEN. [S1]

## 6. Unit Energy and Activation

Each unit has an energy cost determined by its type and current rank.

- Energy earned for a channel advances only the unit assigned to that channel.
- Energy persists between rounds until the unit acts or an Assassin removes it.
- When stored energy reaches the unit's current cost, that unit is ready to act.
- When it acts, its action meter resets.
- Excess energy is discarded. It never carries into the next activation.
- A unit can normally act at most once from reel energy in a round.
- Priest-granted energy can enable an additional activation later in the same round.

Recommended internal representation:

```text
energyStored: integer in [0, currentEnergyCost]
isReady: energyStored >= currentEnergyCost
```

On activation:

```text
energyStored = 0
perform action using current rank
grant 2 action XP
resolve immediate rank-up or bomb eligibility
```

**Confidence:** CONFIRMED except the recommended representation. [S1][S3]

## 7. XP, Rank, and Bombs

### 7.1 XP Sources

A unit gains XP from:

- **XP reel faces:** 1 XP per matching starry/blue face, applied before Hammer and energy resolution.
- **Acting:** 2 XP immediately after the unit completes an action.

### 7.2 Rank Progression

- Every unit begins at Bronze.
- At **6 XP**, Bronze becomes Silver.
- At **6 XP**, Silver becomes Gold.
- At **6 XP**, a Gold unit launches a bomb rather than gaining another rank.
- After a rank-up or bomb, that unit's XP resets to 0.
- Excess XP is discarded. Example: a unit at 5/6 XP that receives 3 XP becomes the next rank at 0/6, not 2/6.
- Rank is match-local and resets to Bronze at the start of every new match.

### 7.3 Bomb

- A bomb deals **2 damage** directly to the opposing Crown.
- It ignores Bulwark.
- A bomb can be triggered by pre-action panel XP or post-action XP.
- Bomb timing follows the resolution sequence in Section 10.

### 7.4 Rank Timing

- Panel XP resolves before energy, so a unit may rank up and then use its new cost and statistics in the same round.
- Action XP resolves after the action, so that action uses the unit's pre-rank statistics.

**Confidence:** CONFIRMED. This corrects two erroneous values found in an unofficial reimplementation: the threshold is 6, not 10, and overflow is discarded, not retained. [S1][S2][S3][S4]

## 8. Bulwark and Attack Targeting

### 8.1 Building

- Bulwark begins at 0.
- Hammer symbols use `max(0, hammerCount - 2)`.
- Bulwark health and height are always the same value.
- Maximum Bulwark is **5**.
- The Engineer adds 2 Bulwark when it acts, capped at 5.
- Public reference rules do not describe automatic Bulwark decay. The baseline implementation must not decay it between rounds.

### 8.2 Attack Height

For each non-bypassing attack:

```text
if attackHeight > targetBulwark:
    damage target Crown by crownDamage
else:
    damage target Bulwark by bulwarkDamage
```

Equal height is blocked. An attack at height 3 hits the Crown only when Bulwark is 0, 1, or 2.

### 8.3 No Overflow

An attack targets either Crown or Bulwark, never both. If an attack deals 5 Bulwark damage to a Bulwark of 1, the remaining 4 damage is discarded.

### 8.4 Bypass Effects

These effects damage Crown regardless of Bulwark:

- Assassin direct damage;
- Mage's height-6 projectile;
- Bombs.

The Warlock's height-5 projectile does **not** bypass a max-height Bulwark because attack height must be strictly greater than Bulwark height.

**Confidence:** CONFIRMED for base-game units; Warlock interaction is a direct consequence of the confirmed comparison rule. [S1][S2]

## 9. Reference Unit Definitions

### 9.1 Warrior

Single attack at height 1. Fast, high direct damage, and fully stopped by any Bulwark.

| Rank | Energy | Crown damage | Bulwark damage | Height |
|---|---:|---:|---:|---:|
| Bronze | 3 | 3 | 3 | 1 |
| Silver | 3 | 5 | 5 | 1 |
| Gold | 3 | 7 | 5 | 1 |

### 9.2 Mage

Two sequential projectiles per action:

1. height 1;
2. height 6.

Each projectile uses the listed Crown/Bulwark values and evaluates its target separately against the current Bulwark. The height-6 projectile always reaches Crown because Bulwark caps at 5.

| Rank | Energy | Crown damage per projectile | Bulwark damage per projectile | Heights |
|---|---:|---:|---:|---|
| Bronze | 5 | 2 | 2 | 1, 6 |
| Silver | 4 | 3 | 3 | 1, 6 |
| Gold | 4 | 3 | 5 | 1, 6 |

### 9.3 Archer

One attack at height 3. Strong against Crown, weak against Bulwark.

| Rank | Energy | Crown damage | Bulwark damage | Height |
|---|---:|---:|---:|---:|
| Bronze | 4 | 3 | 1 | 3 |
| Silver | 3 | 4 | 2 | 3 |
| Gold | 3 | 6 | 3 | 3 |

### 9.4 Engineer

One ground-level attack, then add 2 to the friendly Bulwark, capped at 5. Strong against Bulwark, weak against Crown.

| Rank | Energy | Crown damage | Bulwark damage | Height | Friendly Bulwark |
|---|---:|---:|---:|---:|---:|
| Bronze | 4 | 1 | 3 | 1 | +2 |
| Silver | 4 | 2 | 5 | 1 | +2 |
| Gold | 3 | 4 | 5 | 1 | +2 |

### 9.5 Assassin

On action:

1. Select the opposing unit closest to acting, meaning the one with the least energy still required.
2. Remove/delay the listed amount of stored energy, capped so stored energy cannot fall below 0.
3. Deal the listed direct damage to Crown, ignoring Bulwark.

| Rank | Energy | Delay | Direct Crown damage |
|---|---:|---:|---:|
| Bronze | 3 | 1 | 1 |
| Silver | 3 | 1 | 2 |
| Gold | 3 | 2 | 2 |

The public rules do not define how equal-distance target ties are broken. See Section 15.

### 9.6 Priest

The Priest does not attack. On action:

1. Heal the friendly Crown.
2. Grant energy to the other friendly unit, either immediately or in the deferred Priest phase defined in Section 10.

| Rank | Energy | Crown healing | Energy granted to partner |
|---|---:|---:|---:|
| Bronze | 4 | 1 | 2 |
| Silver | 3 | 2 | 2 |
| Gold | 3 | 2 | 3 |

Healing is capped at 12 Crown HP. Energy overflow is discarded.

### 9.7 Warlock, DLC Reference

The 2025 *Throes of the Watchmaker* DLC adds a seventh figurine. On action:

1. Deal 2 damage to the friendly Crown, but never reduce it below 1 HP from this self-damage.
2. Launch three attacks sequentially at heights 5, 3, and 1.
3. Each projectile uses the rank's listed Crown/Bulwark damage and reevaluates the current Bulwark.

| Rank | Energy | Crown damage per projectile | Bulwark damage per projectile | Heights | Self-damage |
|---|---:|---:|---:|---|---:|
| Bronze | 4 | 1 | 1 | 5, 3, 1 | 2, floor 1 HP |
| Silver | 4 | 2 | 2 | 5, 3, 1 | 2, floor 1 HP |
| Gold | 4 | 3 | 3 | 5, 3, 1 | 2, floor 1 HP |

The DLC did not otherwise rebalance the core Wheels rules according to contemporary coverage. [S2][S8][S9]

## 10. Exact Resolution Sequence

This sequence is essential. Do not replace it with a generic initiative system.

For each numbered stage, resolve the human/player side first, then the opponent side. Within one side and one stage, resolve the Channel A/left unit before the Channel B/right unit.

1. **Panel XP**
   - Grant 1 XP for each matching XP face.
   - Immediately resolve rank-ups.
   - A Gold unit reaching 6 XP queues/launches its bomb in the appropriate bomb step.
2. **Hammer results**
   - Calculate Hammer resource and add Bulwark, capped at 5.
3. **Energy results**
   - Calculate Square and Diamond energy independently.
   - Add energy to the matching units, capped at their activation costs.
4. **Assassin actions**
   - Ready Assassins act first.
   - Their delay can prevent an opposing unit from acting later in this round.
   - Grant 2 action XP immediately and check rank/bomb.
5. **Priest healing and conditional energy setup**
   - Ready Priests heal.
   - If the partner was not ready from reel energy, grant Priest energy now.
   - If the partner was already ready from reel energy, defer the Priest energy until step 9 so it is not wasted before the partner resets.
   - Grant 2 action XP immediately and check rank/bomb.
6. **Engineer actions**
   - Ready Engineers attack and build friendly Bulwark.
   - Grant 2 action XP immediately and check rank/bomb.
7. **Early bombs**
   - Resolve bombs produced during the preceding priority actions and applicable panel-XP events.
8. **Remaining normal actions**
   - Ready Warrior, Mage, Archer, and Warlock units act.
   - Grant 2 action XP immediately after each action and check rank/bomb.
9. **Deferred Priest energy**
   - Apply Priest energy that was deferred because the partner had already been ready from reel energy.
10. **Priest-enabled partner actions**
    - A partner made ready by Priest energy acts now, after units made ready directly from reel energy.
    - Grant 2 action XP immediately and check rank/bomb.
11. **Late bombs**
    - Resolve bombs produced by Priest-enabled actions and any remaining post-action XP.
12. **Final Crown check**
    - If only one Crown is at 0, the other side wins.
    - If both are at 0, tie.
    - Otherwise begin the next round.

This sequence reconciles the comprehensive English rule reference with a separately maintained Korean rules table. The Korean table splits rank/bomb checks into additional visual steps; the resulting state transitions are equivalent. [S1][S2]

## 11. Action and Damage Semantics

### 11.1 Multi-hit Actions

- Projectiles resolve in listed order.
- The Bulwark is updated after every projectile.
- Each following projectile checks the new Bulwark height.
- Crown damage does not change Bulwark.
- Bulwark damage does not spill into Crown.

### 11.2 State at Zero HP

- Crown HP is clamped to a minimum of 0 for display and state comparisons.
- Units continue acting after their Crown reaches 0 because defeat is checked only at round end.
- Priest healing can rescue a side before the final check.
- Warlock self-damage cannot lower its own Crown below 1, but enemy damage can lower it to 0.

### 11.3 Rank Changes and Current Energy

Public sources do not specify exactly how stored energy behaves when a rank-up lowers a unit's activation cost.

**IMPLEMENTATION DECISION for prototype:** Preserve absolute stored energy, clamp it to the new cost, and treat the unit as ready if the stored amount now meets that cost. Do not convert energy proportionally.

This must remain behind a test so it can be changed after black-box verification.

## 12. Reference Modes and Progression

### 12.1 Casual

- Opponent pair is somewhat randomized.
- Lower difficulty.
- Victory awards **10 Gold**.
- No unique collection unlock.

### 12.2 Champion

- Each standard Champion uses a fixed pair.
- First victory against each unique Champion contributes one progression win.
- Rewards are based on total unique Champion wins, not which location was beaten.
- Repeated victories award only Gold.
- The Watchmaker unlocks only after all nine standard Champions are defeated.
- The Watchmaker's pair is randomized between attempts.

### 12.3 Base-Game Reward Sequence

| Unique Champion win | Reward |
|---:|---|
| Starting kit | Copper reel, Warrior, Mage |
| 1 | Archer figurine |
| 2 | Bronze reel |
| 3 | Silver reel |
| 4 | Engineer figurine |
| 5 | Gold reel |
| 6 | Priest figurine |
| 7 | Diamond reel |
| 8 | Assassin figurine |
| 9 | Platinum reel |
| Final Watchmaker win | Flimsy Hammer and achievement |

Some commercial guides incorrectly swap the first two rewards or attach rewards to particular locations. Multiple play records and the comprehensive rules reference support the sequence above. [S1][S6]

### 12.4 Base-Game Champion Pairs

These pairs are useful as encounter-design references but are not required for the first playable prototype.

| Location | Champion pair |
|---|---|
| Port Town of Brisk | Archer + Priest |
| Stonemasons Outpost | Engineer + Warrior |
| Town of Lucent | Mage + Assassin |
| The Vespertine | Engineer + Archer |
| Mooncradle | Warrior + Priest |
| Docarri Village | Assassin + Mage |
| Mirth | Assassin + Priest |
| Cloud Kingdom | Engineer + Mage |
| Repine | Warrior + Archer |
| Watchmaker | Random pair |

**Confidence:** HIGH CONFIDENCE from a complete community achievement guide, cross-checked against independent discussions. [S7]

### 12.5 DLC Progression

- *Throes of the Watchmaker* adds the Warlock figurine and four Wheels opponents in Horloge.
- The Warlock is found through a DLC side objective and is required to challenge the final three Horloge players.
- The DLC does not add a new player reel tier or rewrite the base match rules.

This DLC content is a reference appendix, not required prototype scope. [S8][S9]

## 13. Prototype Ruleset for This Project

The first implementation should reproduce the bounded match loop before adding towns, collection progression, save data, or final art.

### 13.1 Required Engine Capability

Implement the full generic engine for:

- two units per side;
- five eight-face reels;
- three spins with per-reel locking and unlocking;
- Channel A, Channel B, Hammer, XP, and blank faces;
- persistent energy within a match;
- Bronze, Silver, Gold, and bombs;
- Barrier height/health;
- single-hit, multi-hit, direct-damage, delay, healing, energy-grant, and self-damage actions;
- the exact priority sequence in Section 10;
- deterministic seeds;
- human or AI input controllers over the same rules API.

### 13.2 First Playable Content

Expose only two original working units in the first player-facing build:

- a fast height-1 striker using the Warrior numbers;
- a slower two-projectile caster using the Mage numbers.

Use the exact Copper reel configuration for the default match. Add a developer-only selector for every fifth-reel tier so playtesting can distinguish rule quality from early-tier blank frequency.

Each side brings its own fifth reel (D-033): the tiers may differ, and each side spins only its own set. Face
distributions must still be valid.

### 13.2a Charm head starts (D-033, IMPLEMENTATION DECISION)

A side may carry **boons** in its configuration (from a charm bought in the world). They change only the starting
state, before the first round, through the simulation, and are part of the configuration and so of every replay:

| Boon | Effect | Limit |
|---|---|---|
| Rank A / Rank B | that figurine starts at the given rank (0 XP) | charms give at most one Silver |
| Barrier | starting Bulwark | 0-5 (charms give 2) |
| Crown bonus | added to the starting Crown | Crown ≤ 12 (charms give +2) |
| Energy A / Energy B | starting energy | capped at cost - 1, so nobody starts ready (charms give 2 each) |

`SideConfig.Encode` appends boons only when present, so replays from before D-033 decode unchanged.

### 13.3 Human Playtest Gate

After the first complete playable match exists:

> STOP. Do not add collection progression, rewards, towns, dialogue, save/load, additional opponents, production art, or final effects until the creative director has played the match and explicitly approves continuation.

The agent may fix crashes, rule errors, unreadable UI, or input failures without crossing this gate.

## 14. Prototype Opponent AI

Do not use machine learning. The AI is a legal player operating through the same spin and lock commands as the human.

### 14.1 Decision Interface

After spin 1 and spin 2, the AI may:

- lock any unlocked reels;
- unlock previously locked reels;
- finalize early if all reels are locked;
- or consume the next spin.

### 14.2 Baseline Evaluation

Score a potential final result using visible state:

```text
utility =
    lethalValue
  + usefulEnergyA
  + usefulEnergyB
  + xpValueA
  + xpValueB
  + usefulBarrier
  + activationSynergy
  - wastedEnergy
  - wastedBarrier
```

Requirements:

- Energy beyond the amount needed for one activation is scored as waste unless a special effect can use it.
- Barrier value responds to enemy attack heights and direct-damage units.
- XP value increases near a rank-up or Gold bomb.
- A guaranteed lethal line dominates nonlethal development.
- Difficulty profiles modify weights, not RNG legality.

For reproducibility, any search or rollout simulation must use a derived deterministic seed and a fixed evaluation budget.

### 14.3 Difficulty Profiles

| Profile | Behavior |
|---|---|
| Learner | Pursues obvious three-symbol matches; low lookahead; tolerates waste |
| Standard | Balances activation, XP, and Barrier; evaluates expected value of rerolls |
| Expert | Searches lock masks, recognizes lethal lines, counters visible unit timing |

Do not create difficulty by secretly improving face probabilities.

## 15. Open Questions and Required Black-Box Tests

These are the only material rules not fully recoverable from public documentation.

| ID | Question | Prototype behavior until verified |
|---|---|---|
| O-01 | When an Assassin has two equally close targets, which one is delayed? | Target Channel A/left first; log the tie |
| O-02 | When rank-up lowers energy cost below stored energy, does the unit become immediately ready? | Preserve absolute stored energy and clamp to new cost |
| O-03 | Are player reel faces perfectly uniform or animation-weighted? | Uniform 1/8 per face |
| O-04 | What are exact NPC face probabilities by opponent and difficulty? | Do not emulate; use the same legal reels as the human |
| O-05 | Does Warlock resolve in the normal-hero phase, and what is its exact interaction with every priority unit? | Normal-hero phase |
| O-06 | Are there any rare animation-dependent timing differences in bomb order? | Follow Section 10 |
| O-07 | If multiple rank/bomb thresholds could theoretically be crossed by one grant, can more than one trigger? | One trigger; excess XP is discarded |

Each test should capture before/after state, video or screenshots, game version, selected pieces, reel results, and observed ordering.

### 15.1 Implementation decisions active in code (M0)

These are project decisions, not recovered reference rules. Each is covered by an EditMode test and recorded in `DECISIONS.md`.

| ID | Behavior implemented | Where tested |
|---|---|---|
| O-01 | Assassin (Shade) targets the opposing unit with the least `cost - storedEnergy`; ties target Channel A and the `EnergyDelayed` event notes the tie. | `O01_ShadeTie_TargetsChannelA_AndLogsTheTie` |
| O-02 | Rank-up keeps absolute stored energy clamped to the new cost; a unit at or above the new cost is ready. | `RankUpLoweringCost_PreservesAbsoluteEnergyClampedToNewCost` |
| O-03 | Uniform 1/8 per face. | `EachReel_ProducesOnlyFacesInItsDefinition_WithRoughlyUniformFrequency` |
| O-04 | AI uses the same legal reels as the human; no NPC roll advantage. | AI tests |
| O-05 | Warlock (Hexer) acts in stage 8. | `O05_Hexer_ResolvesInTheNormalActionStage` |
| O-06 | Bomb order follows Section 10 only. | bomb tests |
| O-07 | One XP grant crosses at most one threshold; excess is discarded. | `XpOverflow_IsDiscarded` |
| IMPL-01 | Reel draws are a pure function of (seed, side, round, spin number, reel index). One side's rerolls never change the other side's faces (D-013). | `EachSidesDraws_AreIndependentOfTheOtherSidesChoices` |
| IMPL-02 | Panel XP for a unit is one grant equal to its matching XP-face count, so overflow is discarded (D-014). | `XpOverflow_IsDiscarded` |
| IMPL-03 | The third spin finalizes inside the simulation. `FinalizeSpin` is accepted only when all five reels are locked; the match controller issues it when the player confirms with all five locked (D-016; the confirm step is interface-only, D-028). | `LockingAllFiveReels_CanFinalizeEarly` |
| IMPL-04 | A unit whose stored energy already meets its cost at stage 3 (for example carried from an earlier round or clamped by a rank-up) counts as ready from reel energy. | `RankUpLoweringCost...`, `PanelXp_RanksBeforeEnergyAndActions` |
| IMPL-05 | Priest (Mender) energy is deferred to stage 9 only when the partner is ready from reel energy and has not yet acted this round; otherwise it is granted at stage 5, and a partner made ready by it acts at stage 10. | Mender tests |
| IMPL-06 | Bombs from panel XP and stages 4-6 resolve at stage 7; bombs from stages 8 and 10 resolve at stage 11. Within a bomb step: player side first, Channel A before B, then queue order. | `Gold_Produces2DamageBomb...`, `GoldActionXpBomb_ResolvesInLateBombStep` |
| IMPL-07 | Warlock self-damage never lowers its own Crown below 1; at 1 HP or less it has no effect. | `Hexer_SelfDamageFloorsAt1...` |
| IMPL-08 | Safety cap: a match that reaches round 200 without a winner ends in a tie. Normal play never approaches this. | invariant tests |

## 16. Deterministic Simulation Contract

The match simulation must be presentation-independent.

### 16.1 State

Minimum authoritative state:

```text
MatchState
  rulesVersion
  seed
  rngState or deterministic drawIndex
  roundNumber
  phase
  activeSideForResolution
  winner: none | sideA | sideB | tie
  sides[2]
    crownHp
    barrier
    reelTier
    reels[5]
      definitionId
      currentFaceIndex
      locked
    units[2]
      definitionId
      channel
      rank
      xp
      energyStored
      readySource: none | reels | priest
      actedFromReelsThisRound
      actedFromPriestThisRound
  eventLog[]
```

### 16.2 Commands

```text
StartMatch(seed, sideConfigs)
Spin(sideId)
SetReelLock(sideId, reelIndex, locked)
FinalizeSpin(sideId)
ResolveRound()
```

### 16.3 Events

At minimum emit:

```text
ReelsSpun
ReelLockChanged
SpinFinalized
PanelXpGranted
UnitRankedUp
BarrierBuilt
EnergyGranted
EnergyDelayed
UnitActivated
ProjectileResolved
CrownDamaged
BarrierDamaged
CrownHealed
BombLaunched
RoundEnded
MatchEnded
```

Animations consume events. Animations must never mutate authoritative rules state.

## 17. Required Automated Tests

### 17.1 Reels and Locking

- Same seed and commands produce identical faces and event logs.
- First spin affects all five reels.
- Locked reels do not change on the next spin.
- Unlocking a reel allows it to change again.
- A round accepts no more than three spins.
- Locking all five reels can finalize early.
- Each reel can produce only faces in its definition.

### 17.2 Symbol Evaluation

- Counts 0–2 grant 0 resource.
- Count 3 grants 1; 4 grants 2; 5 grants 3.
- Multi-symbol faces count every printed symbol.
- An XP face grants exactly 1 XP regardless of printed symbol count.
- XP applies even when energy threshold is not met.

### 17.3 Energy and Actions

- Energy persists between rounds.
- Reaching cost makes a unit ready.
- Excess energy is discarded.
- Acting resets the meter.
- Current rank determines cost and action statistics.
- A unit delayed below cost does not act in its later phase.

### 17.4 XP and Bombs

- Bronze ranks to Silver at 6 XP.
- Silver ranks to Gold at 6 XP.
- Gold produces a 2-damage bomb at 6 XP.
- XP overflow is discarded.
- Panel XP ranks before energy/actions.
- Action XP ranks after the action.
- Bombs ignore Barrier.

### 17.5 Barrier and Attacks

- Barrier caps at 5.
- Height greater than Barrier hits Crown.
- Height equal to Barrier hits Barrier.
- Barrier damage does not overflow to Crown.
- A height-1 attack is blocked by any nonzero Barrier.
- Archer height 3 clears Barrier 0–2 and is blocked by 3–5.
- Mage's height-1 and height-6 projectiles resolve sequentially.
- Engineer attacks and then adds 2 friendly Barrier.
- Warlock self-damage floors at 1 and its three attacks update Barrier sequentially.

### 17.6 Priority and End State

- Assassin resolves before Priest, Engineer, and ordinary attackers.
- Priest healing resolves before the final Crown check.
- Engineer resolves before ordinary attackers.
- Left unit resolves before right unit within one priority group.
- Player/human side resolves before opponent side within one step.
- A side at 0 Crown HP continues resolving actions.
- One side at 0 after all steps loses.
- Both sides at 0 after all steps tie.
- A Priest can rescue a Crown reduced to 0 before the final check.
- Priest-deferred energy can create a second partner activation in the same round.

## 18. Balancing Telemetry for Human Playtests

Log these values without affecting gameplay:

- match seed;
- match length in rounds and real time;
- spins used per round;
- lock/unlock decisions;
- final face results;
- useful and wasted energy;
- XP gained and wasted;
- Barrier built and overcapped;
- attacks by unit and target type;
- damage to Crown and Barrier;
- turns between activations;
- unit rank timing;
- bomb timing;
- final HP;
- resign/restart events.

The prototype must also allow copying a compact replay seed plus command log so a surprising match can be reproduced exactly.

## 19. Known Design Weaknesses in the Reference Game

These are not implementation bugs. They are documented playtest risks for the original project:

- Mage guarantees Crown damage on every action and remains useful against every Barrier state.
- Assassin's low cost, direct damage, and priority delay can suppress both counterplay and unit growth.
- Priest can heal above starting HP and enable extra actions, creating strong late-match snowballing.
- Assassin + Priest is widely reported as the dominant pairing.
- Early fifth reels contain blanks while NPC outcomes are generated differently, creating a perception of unfairness.
- The optimal move can become “feed one unit every round,” reducing the intended two-unit tradeoff.
- Barrier is binary against large attacks because damage does not overflow: one point can absorb an entire high-damage ground attack.
- Reward progression sometimes provides strategically weaker pieces after stronger options, so unlock order does not create a smooth power curve.

The first prototype should reproduce enough of the reference behavior to understand why it is compelling. It should not assume every imbalance must be preserved in the final original game.

## 20. Source Ledger

- **[S1] Sea of Stars Wiki, “Wheels.”** Most complete English rules reference: turn order, reel faces, stats, modes, rewards, and opponent-roll caveat. Community-maintained, not official source code. <https://seaofstars.fandom.com/wiki/Wheels>
- **[S2] Namu Wiki, “Sea of Stars,” Wheels section.** Independent Korean rules table: exact face layouts, 6-XP threshold, resolution order, base-unit stats, and DLC Warlock values. <https://www.namu.moe/w/Sea%20of%20Stars(%EA%B2%8C%EC%9E%84)>
- **[S3] Gamer Guides, “How to Unlock the Wheels Minigame.”** Cross-check for locking, symbol quantities, Bulwark cap, XP, rank, and bombs. <https://www.gamerguides.com/sea-of-stars/guide/minigames/wheels/how-to-unlock-the-wheels-minigame-in-sea-of-stars>
- **[S4] Siliconera, “How to Play Wheels in Sea of Stars.”** Cross-check for symbol variants, 10 Crown HP, action rods, and 6-XP rank threshold. <https://www.siliconera.com/how-to-play-wheels-in-sea-of-stars/>
- **[S5] ArrPeeGeeZ, “Sea of Stars Walkthrough: Wheels Guide.”** Cross-check for the core loop, unit roles, action XP, and locations. <https://www.arrpeegeez.com/2023/09/sea-of-stars-walkthrough-wheels-guide.html>
- **[S6] Steam Community, “Treasure/Collectible Checklist.”** Play-record evidence that the first Champion reward is Archer and the second is Bronze. <https://steamcommunity.com/sharedfiles/filedetails/?id=3027532149>
- **[S7] TrueAchievements community guide/comments, “Clockwork Champion.”** Complete fixed Champion pair list. <https://www.trueachievements.com/a403237/clockwork-champion-achievement>
- **[S8] Sabotage Studio, “Throes of the Watchmaker DLC Out Now.”** Official confirmation that Wheels returned in the May 20, 2025 DLC. <https://sabotagestudio.com/press-release/malevolent-spectacle-awaits-sea-of-stars-free-eight-hour-throes-of-the-watchmaker-dlc-out-now-alongside-limited-time-base-game-discount/>
- **[S9] Play Critically, “Throes of the Watchmaker Review.”** Contemporary description of the new Warlock's multi-hit/self-damage behavior and lack of broader Wheels rebalance. <https://playcritically.com/2025/06/01/sea-of-stars-throes-of-the-watchmaker-review/>
- **[S10] Sea of Stars Help Center, Update 3.0.60146.** Current final-update notes; no Wheels rule or balance changes are listed. <https://playdigious.helpshift.com/hc/en/37-sea-of-stars/faq/778-update-3-0-60146---june-8-2026/>

## 21. Change Control

When changing a rule:

1. Change the data or simulation rule.
2. Update or add deterministic tests.
3. Update this document in the same commit.
4. Record the design reason in `DECISIONS.md`.
5. If the change intentionally departs from the reference game, label it as a project rule rather than rewriting reference history.

Do not allow presentation code, animation timing, or AI shortcuts to become hidden rules.
