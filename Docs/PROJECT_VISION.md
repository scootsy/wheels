# Project Vision

## Product concept

Create an original game built around a collectible, mechanical tabletop contest that feels like a real pastime inside a larger fictional world. The appeal comes from short matches, visible tactical timing, physical-looking pieces, unlockable strategic variety, and opponents who appear to inhabit the same world as the player.

The reference point is the pleasure of in-world games such as Wheels, Gwent, dice poker, and other side activities that can become compelling enough to support a game of their own. This project does not reuse their protected names, art, characters, settings, dialogue, audio, or UI.

## Creative-director model

The human acts as creative director and playtester. Agents handle engineering, editor operations, tests, builds, technical diagnosis, and routine implementation choices. The project must remain understandable and steerable by someone who is not expected to inspect C# or Unity serialization.

Every handoff should answer:

- What can I play now?
- How do I launch and control it?
- What changed that I can see?
- What needs my judgment rather than an engineering decision?

## Experience pillars

### A physical game with readable machinery

The board should eventually resemble a compact mechanical diorama. Reels, barriers, meters, pieces, and attacks should make the current state readable through placement and movement rather than relying on menus alone.

### Decisions before spectacle

Locking, rerolling, unit timing, defense, and growth must remain interesting with primitive art and instant placeholder transitions. Art cannot be used to hide a weak match.

### Short sessions with strategic variation

A match should be easy to begin, understandable within minutes, and variable enough to invite rematches. New pieces and opponents should alter decisions rather than merely raise numbers.

### Original identity

The final game uses original terminology, visual language, fiction, pieces, effects, sound, and progression. Reference screenshots are for private spatial study only.

### Controlled scope

The credible long-term vertical slice is one tavern-like location, one tabletop game, and roughly five distinct opponents. It is not an open-world RPG. Even that small world is deferred until the core match passes human playtesting.

## Visual direction after approval

The favored direction is:

- fixed-camera stylized low-poly 3D;
- a warm, tactile tabletop or diorama presentation;
- collectible miniature-like pieces with strong static silhouettes;
- crisp 2D overlays for exact numbers and accessibility;
- restrained animation that communicates rules before adding spectacle.

This direction is not authorization to create production assets before the human playtest gate.

## First playable

The first playable is deliberately ugly. It contains one complete human-versus-AI match with two original placeholder units, five reels, full deterministic resolution, keyboard/mouse and controller support, readable state, results, replay capture, and tests.

Success is not measured by visual similarity to a reference game. It is measured by whether the creative director can play repeated matches and make informed judgments about:

- whether locking and rerolling are satisfying;
- whether unit timing is legible;
- whether offense, defense, and growth create meaningful tension;
- whether the match length feels right;
- whether the underlying idea deserves progression, world context, and art investment.

## Explicit non-goals before playtest approval

- building a full RPG or exploration game;
- recreating *Sea of Stars* or Wheels;
- producing final characters, environments, or effects;
- implementing a collection economy before the match is fun;
- designing around online competition;
- accumulating systems merely because an agent can generate them.

