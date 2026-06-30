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

Requires OBS 28 or newer. Works on Windows, macOS, and Linux.

## 2. Configure it

The script reads the game from your scene's **Game Capture** (or **Window Capture**) source, then files clips into subfolders of your recording folder.

| Setting                              | Default         | What it does                                                                    |
| ------------------------------------ | --------------- | ------------------------------------------------------------------------------- |
| Fallback folder name                 | `Any Recording` | Where clips go when no game is detected (desktop screenshots, Display Capture). |
| Organization mode                    | Folder per game | Optionally add a `yy-mm-dd` date subfolder.                                     |
| Prefix filenames with the game title | Off             | Names files `Game - <original>` so the game travels in the filename too.        |
| Organize replay-buffer saves         | On              | Replays go under a `replay` subfolder.                                          |
| Organize screenshots                 | On              | Screenshots go under a `screenshot` subfolder.                                  |

> The capture source must be **visible** in your current scene for the script to read the hooked game. Keep your Game/Window Capture source enabled.

## 3. Let QlipQ read the organized clips

QlipQ scans each watched folder **including subfolders**, so clips the script tidies away still land in your queue. It also recovers the **game name** and surfaces it as the `{source}` token in your [naming template](/guide/getting-started) — two ways, either of which this script can drive:

- **From the folder.** When a clip sits in a per-game subfolder under a watched folder — exactly what this script creates — QlipQ uses that folder name as the clip's source, the same convention it reads from NVIDIA Share. Just add your OBS recording folder under **Settings → Watched folders**.
- **From the filename.** Turn on **Prefix filenames with the game title** and the script names files `Apex - 2026-06-30 12-00-00.mkv`. QlipQ treats the leading label as the source, so the game name rides along even if the file is moved out of its folder.

A filename label takes precedence over the folder when both are present, so you can use either or both safely.

> This is the same `{source}` value described in [OBS replay buffer setup](/guide/obs-replay-buffer) — the script is just a hands-off way to populate it without editing OBS's filename format.

## What it covers (and what it doesn't)

The script ports RecORDER's core organizing behavior. It files the **final** recording on stop, every saved replay, and every screenshot. It does **not** split-handle OBS automatic file-splitting mid-recording (only the last segment is sorted), and it tracks the capture source in your **current scene** (sources nested inside sub-scenes are not traversed).

**Next:** [point QlipQ at your recording folder](/guide/getting-started).
