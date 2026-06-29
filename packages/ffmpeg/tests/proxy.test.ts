import { expect, test } from "vite-plus/test";
import { buildProxyArgs } from "../src/proxy.ts";

test("remux (no transcode) stream-copies to a faststart mp4", () => {
  const out = buildProxyArgs({ inputPath: "in.mkv", outputPath: "out.mp4", transcode: false });
  expect(out).toEqual(["-y", "-i", "in.mkv", "-c", "copy", "-movflags", "+faststart", "out.mp4"]);
});

test("transcode builds a short-GOP H.264 proxy", () => {
  const out = buildProxyArgs({ inputPath: "in.mp4", outputPath: "out.mp4", transcode: true });
  const joined = out.join(" ");
  expect(joined).toContain("-c:v libx264");
  expect(joined).toContain("scale=-2:720");
  expect(joined).toContain("-g 30");
  expect(out.at(-1)).toBe("out.mp4");
});

test("transcode honors maxHeight", () => {
  const out = buildProxyArgs({
    inputPath: "in.mp4",
    outputPath: "out.mp4",
    transcode: true,
    maxHeight: 480,
  });
  expect(out.join(" ")).toContain("scale=-2:480");
});

test("baked audio mixes enabled tracks at their volumes (remuxed video)", () => {
  const out = buildProxyArgs({
    inputPath: "in.mkv",
    outputPath: "out.mp4",
    transcode: false,
    audioTracks: [
      { index: 0, enabled: true, volume: 1 },
      { index: 1, enabled: false, volume: 1 },
      { index: 2, enabled: true, volume: 0.5 },
    ],
  });
  const joined = out.join(" ");
  expect(joined).toContain("[0:a:0]volume=1[pa0]");
  expect(joined).toContain("[0:a:2]volume=0.5[pa1]");
  expect(joined).toContain("amix=inputs=2:normalize=0[aout]");
  expect(joined).toContain("-map 0:v:0 -c:v copy");
  expect(joined).toContain("-map [aout] -c:a aac");
});

test("baked audio with one enabled track uses a single volume filter (no amix)", () => {
  const out = buildProxyArgs({
    inputPath: "in.mkv",
    outputPath: "out.mp4",
    transcode: false,
    audioTracks: [
      { index: 0, enabled: false, volume: 1 },
      { index: 1, enabled: true, volume: 1 },
    ],
  });
  const joined = out.join(" ");
  expect(joined).toContain("[0:a:1]volume=1[aout]");
  expect(joined).not.toContain("amix");
});

test("baked audio with nothing enabled drops audio (-an)", () => {
  const out = buildProxyArgs({
    inputPath: "in.mkv",
    outputPath: "out.mp4",
    transcode: false,
    audioTracks: [{ index: 0, enabled: false, volume: 1 }],
  });
  expect(out).toContain("-an");
});
