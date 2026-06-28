import { spawnSync } from "node:child_process";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterAll, beforeAll, expect, test } from "vite-plus/test";
import { buildExportArgs, buildProbeArgs, parseFfprobe } from "../src/index.ts";

// These tests run the real ffmpeg/ffprobe binaries against the args our builders
// produce. They are skipped automatically when ffmpeg is not installed (e.g. a
// CI runner without it), so the suite stays green everywhere.
function hasBin(bin: string): boolean {
  const result = spawnSync(bin, ["-version"], { stdio: "ignore" });
  return !result.error && result.status === 0;
}

const ffmpegAvailable = hasBin("ffmpeg") && hasBin("ffprobe");

let dir = "";
let input = "";

beforeAll(() => {
  if (!ffmpegAvailable) return;
  dir = mkdtempSync(join(tmpdir(), "qlipq-it-"));
  input = join(dir, "fixture.mkv");
  // 2s 1280x720 test pattern with two distinct audio tracks (440Hz + 1000Hz).
  const gen = spawnSync(
    "ffmpeg",
    [
      "-y",
      "-f",
      "lavfi",
      "-i",
      "testsrc=size=1280x720:rate=30:duration=2",
      "-f",
      "lavfi",
      "-i",
      "sine=frequency=440:duration=2",
      "-f",
      "lavfi",
      "-i",
      "sine=frequency=1000:duration=2",
      "-map",
      "0:v",
      "-map",
      "1:a",
      "-map",
      "2:a",
      "-c:v",
      "libx264",
      "-pix_fmt",
      "yuv420p",
      "-shortest",
      input,
    ],
    { encoding: "utf8" },
  );
  if (gen.status !== 0) throw new Error(`fixture generation failed:\n${gen.stderr}`);
}, 60_000);

afterAll(() => {
  if (dir) rmSync(dir, { recursive: true, force: true });
});

test.skipIf(!ffmpegAvailable)(
  "buildProbeArgs + parseFfprobe describe a real clip",
  () => {
    const probe = spawnSync("ffprobe", buildProbeArgs(input), { encoding: "utf8" });
    expect(probe.status).toBe(0);
    const info = parseFfprobe(probe.stdout);
    expect(info.width).toBe(1280);
    expect(info.height).toBe(720);
    expect(info.audioStreams.length).toBe(2);
    expect(info.durationSec).toBeGreaterThan(1.5);
  },
  60_000,
);

test.skipIf(!ffmpegAvailable)(
  "buildExportArgs trims, crops, and selects audio as specified",
  () => {
    const output = join(dir, "out.mp4");
    const args = buildExportArgs({
      inputPath: input,
      outputPath: output,
      spec: {
        trim: { startSec: 0.5, endSec: 1.5 },
        crop: { x: 100, y: 50, width: 640, height: 360 },
        audioTracks: [
          { index: 0, enabled: true, volume: 0.5 },
          { index: 1, enabled: false, volume: 1 },
        ],
      },
    });

    const run = spawnSync("ffmpeg", args, { encoding: "utf8" });
    expect(run.status, run.stderr).toBe(0);
    expect(existsSync(output)).toBe(true);

    const probe = spawnSync("ffprobe", buildProbeArgs(output), { encoding: "utf8" });
    const info = parseFfprobe(probe.stdout);
    expect(info.width).toBe(640); // cropped width
    expect(info.height).toBe(360); // cropped height
    expect(info.audioStreams.length).toBe(1); // one track disabled
    expect(info.durationSec).toBeGreaterThan(0.8); // ~1.0s after trim
    expect(info.durationSec).toBeLessThan(1.3);
  },
  60_000,
);
