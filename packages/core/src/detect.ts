/** Raw OBS config files, read by the host and parsed here (no I/O in this module). */
export interface ObsConfigFiles {
  /** Contents of `obs-studio/user.ini`, or null if absent. */
  userIni: string | null;
  /** Map of profile directory name → contents of its `basic.ini`. */
  profiles: Record<string, string>;
}

/**
 * Read a single `key=value` from an INI section. Section and key match
 * case-insensitively; tolerant of a UTF-8 BOM and CRLF line endings, both of
 * which OBS writes.
 */
function getIniValue(text: string, section: string, key: string): string | undefined {
  let current: string | undefined;
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.replace(/^﻿/, "").trim();
    if (!line || line.startsWith(";") || line.startsWith("#")) continue;
    const header = /^\[(.+)\]$/.exec(line);
    if (header) {
      current = header[1].toLowerCase();
      continue;
    }
    if (current !== section.toLowerCase()) continue;
    const eq = line.indexOf("=");
    if (eq < 0) continue;
    if (line.slice(0, eq).trim().toLowerCase() === key.toLowerCase()) {
      return line.slice(eq + 1).trim();
    }
  }
  return undefined;
}

/**
 * Resolve the folder OBS records into, from its config files.
 *
 * Picks the active profile (`user.ini` `[Basic] ProfileDir`, falling back to
 * `Profile`, then the sole/first profile present), then reads that profile's
 * `basic.ini`: `[Output] Mode = Advanced` uses `[AdvOut] RecFilePath`, otherwise
 * `[SimpleOutput] FilePath`. Returns `undefined` when nothing usable is found.
 */
export function detectObsRecordingFolder(files: ObsConfigFiles): string | undefined {
  const profileNames = Object.keys(files.profiles);
  if (profileNames.length === 0) return undefined;

  const active =
    (files.userIni && getIniValue(files.userIni, "Basic", "ProfileDir")) ||
    (files.userIni && getIniValue(files.userIni, "Basic", "Profile"));

  const basicIni =
    (active && files.profiles[active]) ??
    (active && matchProfileCaseInsensitive(files.profiles, active)) ??
    files.profiles[profileNames[0]];

  if (!basicIni) return undefined;

  const mode = getIniValue(basicIni, "Output", "Mode");
  const folder =
    mode?.toLowerCase() === "advanced"
      ? getIniValue(basicIni, "AdvOut", "RecFilePath")
      : getIniValue(basicIni, "SimpleOutput", "FilePath");

  const trimmed = folder?.trim();
  return trimmed ? trimmed : undefined;
}

function matchProfileCaseInsensitive(
  profiles: Record<string, string>,
  name: string,
): string | undefined {
  const lower = name.toLowerCase();
  for (const [key, value] of Object.entries(profiles)) {
    if (key.toLowerCase() === lower) return value;
  }
  return undefined;
}
