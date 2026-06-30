---
title: Auto-organize OBS recordings by game
description: A companion OBS Lua script that sorts recordings, replays, and screenshots into per-game folders QlipQ reads automatically.
order: 3
---

OBS writes every recording into one flat folder. **RecORDER (Lua)** is an optional companion script that sorts each finished recording, saved replay, and screenshot into a folder named after the game you were capturing — the same per-game layout NVIDIA ShadowPlay uses, and one QlipQ already understands. It runs on the LuaJIT runtime bundled with OBS, so there is no Python to install.

## 1. Install the script

1. Download [`recorder.lua`](/recorder.lua).
2. In OBS, open **Tools → Scripts**.
3. On the **Lua Scripts** tab, click **+** and select `recorder.lua`.

Requires OBS 28 or newer. Works on Windows, macOS, and Linux. Unlike the original RecORDER script, this uses the Lua runtime OBS already ships with — nothing else to install.

## 2. Configure it

The script reads the game from your scene's **Game Capture** (or **Window Capture**) source, then files clips into subfolders of your existing recording folder. Open it under **Tools → Scripts → recorder.lua** to set:

| Setting                              | Default         | What it does                                                                    |
| ------------------------------------ | --------------- | ------------------------------------------------------------------------------- |
| Fallback folder name                 | `Any Recording` | Where clips go when no game is detected (desktop screenshots, Display Capture). |
| Organization mode                    | Folder per game | Optionally add a `yy-mm-dd` date subfolder under each game.                     |
| Prefix filenames with the game title | Off             | Names files `Game - <original>` so the game also travels in the filename.       |
| Organize replay-buffer saves         | On              | Saved replays go under a `replay` subfolder; turn off to leave them put.        |
| Replay subfolder name                | `replay`        | Name of that subfolder.                                                         |
| Organize screenshots                 | On              | Screenshots go under a `screenshot` subfolder.                                  |
| Screenshot subfolder name            | `screenshot`    | Name of that subfolder.                                                         |

Recordings are always organized; replays and screenshots are individually toggleable.

> The capture source must be **visible** in your current scene for the script to read the hooked game. Keep your Game/Window Capture source enabled.

## 3. What the folders look like

Say OBS records into `D:\Clips`. Before the script, everything piles up in one place:

```text
D:\Clips\
├─ 2026-06-30 21-14-02.mkv
├─ 2026-06-30 21-55-40.mkv
├─ Replay 2026-06-30 22-03-11.mkv
└─ 2026-06-30 22-10-00.png
```

With the script running, each file lands under the game that was hooked when it was captured:

```text
D:\Clips\
├─ Apex Legends\
│  ├─ 2026-06-30 21-14-02.mkv
│  └─ replay\
│     └─ Replay 2026-06-30 22-03-11.mkv
├─ Counter Strike 2\
│  └─ 2026-06-30 21-55-40.mkv
└─ Any Recording\
   └─ screenshot\
      └─ 2026-06-30 22-10-00.png
```

(The screenshot was taken on the desktop with no game hooked, so it went to the **fallback** folder.)

Switch **Organization mode** to **Folder per game, then date** to add a daily subfolder:

```text
D:\Clips\Apex Legends\26-06-30\2026-06-30 21-14-02.mkv
```

## 4. Let QlipQ read the organized clips

QlipQ scans each watched folder **including subfolders**, so the tidied clips still land in your queue. It also recovers the **game name** and surfaces it as the `{source}` token in your [naming template](/guide/getting-started). The script can drive that two ways:

- **From the folder.** When a clip sits in a per-game subfolder under a watched folder — exactly what this script creates — QlipQ uses that folder name as the source, the same convention it reads from NVIDIA Share. Just add your OBS recording folder under **Settings → Watched folders** (QlipQ can auto-detect it). In the example above, the queue shows the two `.mkv` clips with `{source}` = _Apex Legends_ and _Counter Strike 2_.
- **From the filename.** Turn on **Prefix filenames with the game title** and the script names files `Apex Legends - 2026-06-30 21-14-02.mkv`. QlipQ reads the leading label as the source, so the game name rides along even if the clip is later moved out of its folder.

A filename label takes precedence over the folder when both are present, so it is safe to use either or both.

> This is the same `{source}` covered in [OBS replay buffer setup](/guide/obs-replay-buffer) — the script is just a hands-off way to fill it without editing OBS's filename format yourself.

## 5. Recommended setup

For a clean capture-to-clip pipeline:

1. Set OBS to record in **mkv** and, ideally, run the **replay buffer** — see [OBS replay buffer setup](/guide/obs-replay-buffer).
2. Load `recorder.lua` and leave the defaults (per-game folders; replays and screenshots organized).
3. In QlipQ, add your OBS recording folder under **Settings → Watched folders**.
4. That's it: press your save-replay hotkey mid-game, the script files the clip under the game, and QlipQ queues it with the game already set as `{source}`.

Prefer the game name baked into the filenames (e.g. you sync clips elsewhere)? Also enable **Prefix filenames with the game title**.

## Troubleshooting

- **Clips land in "Any Recording."** No game was hooked at capture time. Make sure your **Game Capture** (or **Window Capture**) source is in the current scene, visible, and actually capturing. **Display Capture** can't report a game, so its clips always use the fallback folder.
- **The game folder name looks stripped** (e.g. _Tom Clancys Rainbow Six Siege_). Titles are reduced to letters, numbers, and spaces, so punctuation like `:`, `®`, or `'` is removed — matching how the original script names folders.
- **A split recording wasn't fully sorted.** With OBS automatic file splitting, only the **final** segment is moved; earlier segments stay in the root folder.
- **Nothing moved at all.** The script acts when a file is finalized — **Recording Stopped**, **Replay Buffer Saved**, or **Screenshot Taken** — not while recording is still running.

## What it covers (and what it doesn't)

The script files the **final** recording on stop, every saved replay, and every screenshot. It does **not** split-handle OBS automatic file-splitting mid-recording (only the last segment is sorted), and it tracks the capture source in your **current scene** (sources nested inside sub-scenes are not traversed).

**Next:** [point QlipQ at your recording folder](/guide/getting-started).
