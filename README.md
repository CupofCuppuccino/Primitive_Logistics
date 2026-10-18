# Primitive Logistics

**Log: An easy solution for transport or recording.**

A short physics puzzle game made for **Ludum Dare 60 — Old Tech**.
Deliver a fragile robot across a ruined world using three logs, a wooden pallet,
and a delivery crew with questionable judgment. Collect a fourth log, fill pits,
climb steep slopes, push a boulder into a hole, and tip the robot under a low roof.
Nine checkpoints let you recover your equipment and retry each obstacle.

## Open and play

1. Install **Unity 2022.3.62f3, international edition**.
2. Clone this repository or download and extract its source ZIP.
3. In Unity Hub, add the repository folder as a project. Let Unity import the assets.
4. Open `Assets/OldTech/Scenes/MainMenu.unity` and press Play.

The project uses the built-in rendering pipeline and Unity's built-in modules;
no third-party Unity packages are required. Git LFS is not required.

## Controls

| Input | Action |
| --- | --- |
| Left mouse button | Grab and drag logs using a physical spring |
| Hold Space | Push the pallet; release to slow down |
| R | Restore the latest checkpoint, robot and allocated logs |
| Esc | Pause, resume or return to the menu |

In **How to Play**, enter `LudumDare60` to enable developer mode for the current
session. This unlocks checkpoint jumps with 0–9 and physics settings with F1.
Save data uses local Unity PlayerPrefs; in WebGL it belongs to the browser/site.

## Build

- **Windows x64:** select `Old Tech > Build final Windows submission`.
  Output: `Builds/ReleaseWindows/PrimitiveLogistics.exe`.
- **WebGL / itch.io:** install **WebGL Build Support for Unity 2022.3.62f3**, then
  select `Old Tech > Build itch.io WebGL submission`. The editor switches to
  WebGL and exports to `Builds/WebGL/`, including `index.html`.
- Build Settings can also build the two enabled scenes in their existing order:
  `MainMenu`, then `RollingDelivery`.

Optional packaging scripts use Python 3 and its standard library:
`python Tools/package_windows_release.py` or `python Tools/package_webgl_release.py`.
Their output is written to `Builds/Submission/`. The WebGL ZIP has `index.html`
at its root and can be marked as playable in the browser on itch.io.

## Source layout

- `Assets/OldTech/Runtime/`: gameplay, physics, UI, audio and presentation code.
- `Assets/OldTech/Scenes/`: the finished menu and delivery scenes.
- `Assets/OldTech/Art/`, `Audio/`, `Physics/`: assets used by the game and checkpoint prefab.
- `Assets/OldTech/Resources/`: UI and sound bindings.
- `Assets/OldTech/Editor/`: build tools, scene-authoring helpers and validation checks.
- `Assets/WebGLTemplates/`: responsive browser player template.
- `Packages/`, `ProjectSettings/`: reproducible Unity project configuration.
- `Documentation/`: English game description, controls and credits.

The finished scenes are ready to run. Historical scene-building/import helpers
remain as development source; they are not required to open or build the game.
Some optional import helpers expect local drawing/reference files, which are
intentionally excluded. Edit the included runtime PNGs directly when changing art.

This source submission excludes reference images, drawing templates, unused
asset versions, layered art files, promotional images, old backups, generated
Unity caches, test captures/logs, and compiled game builds. All included Unity
assets retain their original `.meta` files and GUIDs.

## Credits

- **Engine:** made with Unity.
- **Sound effects:** generated with [jsfxr](https://sfxr.me/), with additional free
  sound assets from [Pixabay](https://pixabay.com/).
- **Artwork:** mainly self-drawn using visual references, with some AI-generated UI elements.
- **Modeling:** self-assembled from simple shapes, with some procedurally generated meshes.
- **Development assistance:** AI-assisted coding.

See [the submission description](Documentation/Submission.md) and
[credits](Documentation/Credits.txt) for the accompanying submission text.
