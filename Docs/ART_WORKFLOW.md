# Art Workflow: Getting Models Into the Game

How imported 3D models (characters and buildings) get from a model generator or Blender into the game (D-026). The goal: you drop files in a folder, click two menu items, drag models onto names, and check the result in the real build.

## The short version

1. **Make or pick the model.** Export **FBX**, one model per file. Target sizes are below.
2. **Drop it in** `Assets/Game/Characters/` (people) or `Assets/Game/Buildings/` (buildings). Give the file a readable name, e.g. `Wren_Tinker.fbx` or `Inn_Steampunk.fbx`.
3. In Unity: **Tabletop → Art → 1. Prepare Imported Models.** This unpacks the textures hidden inside the FBX, makes a proper material, and hooks them up. (Without it the model shows up plain grey.)
4. **Tabletop → Art → 2. Render Model Previews** writes a picture of every model to `Logs/ModelPreviews/` so you can see them all at once.
5. Open **`Assets/Game/Art/WorldArt.asset`**. It lists every person (`Wren`, `Mira Tallow`, `Player`, ...) and every building (`Hearthmoor/Cottage`, `Brindlecross/Inn`, ...). **Drag your model onto the slot.** Empty slots keep the placeholder, so art can arrive one piece at a time.
6. Press Play in `World.unity` to look. Then ask Claude to "rebuild and self-check": the real Windows build is the only thing that counts.

## Slot settings

| Setting | What it does |
|---|---|
| **Model** | The FBX. |
| **Pose** | Rigged characters only: an animation to take the standing pose from. Without one they stand in their rest pose, which is often a T-pose. |
| **Pose Time** | Which moment of that animation (seconds). |
| **Loop** | Play the animation continuously instead of freezing one frame. Use this for an **idle** animation. |
| **Turn** | Rotate the model if it faces the wrong way. The front (door, face) should point the same way as the placeholder's front. Try 90 / 180 / 270. |
| **Size** | People: height in metres (blank = 1.75). Buildings: width in metres (blank = fill the placeholder's footprint). |

The game scales every model automatically, stands it on the ground, and centres it, so exported size and pivot don't matter.

## Shelving models (switch back to placeholders without deleting anything)

- **Tabletop → Art → Shelve Imported People (use placeholders)** (or **Shelve Imported Buildings**) takes the model off its slot and puts it on a shelf (`Assets/Game/Editor/Art/ShelvedArt.asset`). The placeholder comes back.
- Nothing in the game points at a shelved model any more, so **it is not in any build**. Each build's result line reports `peopleModels=` / `buildingModels=` so this can be checked.
- The model files stay in `Assets/Game/Characters` / `Buildings` and in Git.
- **Tabletop → Art → Restore Shelved Models** puts every shelved model back exactly as it was.
- As of D-030, the two imported people (Wren, Mira Tallow) are shelved; the imported buildings are still in use.

## What to ask the model generator for

| | Characters | Buildings |
|---|---|---|
| Triangles | **10,000–30,000** (today's are 180,000–300,000) | 10,000–40,000 |
| Texture | 1024 px is plenty at this camera distance | 2048 px |
| Pose / animation | Rigged, with an **Idle** animation (and later **Walk**) | Static |
| Style | Matching style across the set; clothed village folk fit the game's tone | Front door on one clear side |
| Base / pedestal | **None**: figurine bases look odd in the world | Flat bottom |

The biggest single improvement is **an idle animation per character**. The game will loop it with **Loop** ticked. The current rigged models only contain an attack combo, so they hold its first frame as a pose.

## Asset Store packs and the world look (D-033)

1. In Unity: **Window → Package Manager → My Assets**, download and import the pack.
2. If the import says it will overwrite **ProjectSettings**, that is a complete-project pack: let it import, then
   ask Claude to restore the project's settings from Git (it keeps a backup of the pack's versions).
3. Run **Tabletop → Art → Build World Look and Sounds**. It lists what the world uses (`WorldLook.asset`), the
   sounds (`SoundBank.asset`) and the table figurines (`Figurines.asset`), caps texture sizes, and links them into
   both scenes.
4. Pack folders are **not committed** (public repository; Asset Store EULA). Builds contain what they use. A fresh
   checkout without the packs still runs, with placeholders and without pack sounds.
5. New CC0 downloads go in `Assets/Game/ThirdParty/` with their licence and a line in `CREDITS.md`.

## Rules the project keeps

- **Original characters only.** No recognizable characters from existing games, anime, or films (project mission in `AGENTS.md`).
- **Nothing sexual, and nothing that reads as underage in a revealing outfit.** Models that fail this go in the `Excluded Models` folder next to the project, outside `Assets`. That folder is out of Git, and Unity never imports anything in it, so the Prepare step can't unpack it either.
- **Know where it came from.** Keep the generator's terms or licence for anything that ships.
- **Large files go through Git LFS** automatically (`.fbx`, and the unpacked textures under `Characters/Textures` and `Buildings/Textures`).
- **Materials must be URP.** The Prepare step makes URP/Lit materials, and tests fail if a model would render magenta in the build (D-024).

## What Claude checks every time

- The `WorldArtTests` (EditMode) and `ImportedModels_...` (PlayMode) tests:
  - the art list only names real people and buildings;
  - every material is URP with its colour texture connected;
  - models are the right height and stand on the ground;
  - posed characters use the runtime pose path.
- `bash Tools/selfcheck_build.sh` runs the real build, which:
  - screenshots Hearthmoor, the bridge, Wren's camp, Brindlecross, the Inn, the hall and the champion's table;
  - counts the model buildings and people;
  - prints `PLAYER PROBLEMS`.

  This is how the editor-only pose bug was caught: the editor looked fine and the build did not.

## Going further later (not started, needs approval)

- **Blender pass:** reduce triangle counts, remove bases, and fix orientation and pivots before export. Blender MCP is reserved for approved art work (AGENTS.md).
- **Animation sets:** idle and walk for the player and villagers, driven by movement.
- **Modular kit:** walls, roofs and props that build many houses from a few pieces, instead of one unique model per building.
