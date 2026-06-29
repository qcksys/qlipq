import type { AudioTrackSpec } from "@qcksys/qlipq-core";

export interface BuildProxyOptions {
  inputPath: string;
  outputPath: string;
  /**
   * Transcode to H.264 (for codecs the webview can't play, e.g. HEVC). When false,
   * the video stream is copied — a near-instant container remux.
   */
  transcode: boolean;
  /** Max height for the transcoded proxy; keeps aspect (even width). */
  maxHeight?: number;
  /**
   * When set, bake the selected audio tracks into a single mixed stream (so the player
   * matches the export). Omit for single-track clips (the element handles volume/mute).
   */
  audioTracks?: AudioTrackSpec[];
}

function formatVolume(volume: number): string {
  return String(Number(volume.toFixed(4)));
}

const TRANSCODE_VIDEO = [
  "-c:v",
  "libx264",
  "-preset",
  "veryfast",
  "-crf",
  "23",
  "-g",
  "30",
  "-keyint_min",
  "30",
  "-pix_fmt",
  "yuv420p",
];

/**
 * Build ffmpeg args for a webview-playable PREVIEW proxy. WebView2's `<video>` can't
 * play MKV and needs a system extension for HEVC, so the editor previews a proxy while
 * exports still use the original. A short GOP keeps `<video>` seeking frame-accurate.
 *
 * With `audioTracks`, the enabled tracks are mixed (with per-track volume) into one
 * stream so the preview audio matches the export.
 */
export function buildProxyArgs(opts: BuildProxyOptions): string[] {
  const { inputPath, outputPath, transcode, maxHeight = 720, audioTracks } = opts;

  // No baked selection: fast remux, or a default transcode of the default streams.
  if (!audioTracks) {
    if (!transcode) {
      return ["-y", "-i", inputPath, "-c", "copy", "-movflags", "+faststart", outputPath];
    }
    return [
      "-y",
      "-i",
      inputPath,
      "-vf",
      `scale=-2:${maxHeight}`,
      ...TRANSCODE_VIDEO,
      "-c:a",
      "aac",
      "-b:a",
      "128k",
      "-movflags",
      "+faststart",
      outputPath,
    ];
  }

  const enabled = audioTracks.filter((track) => track.enabled);
  const filters: string[] = [];
  if (transcode) filters.push(`[0:v:0]scale=-2:${maxHeight}[vout]`);

  let audioOut: string | null = null;
  if (enabled.length === 1) {
    const t = enabled[0];
    filters.push(`[0:a:${t.index}]volume=${formatVolume(t.volume)}[aout]`);
    audioOut = "[aout]";
  } else if (enabled.length > 1) {
    const labels = enabled.map((t, i) => {
      filters.push(`[0:a:${t.index}]volume=${formatVolume(t.volume)}[pa${i}]`);
      return `[pa${i}]`;
    });
    filters.push(`${labels.join("")}amix=inputs=${labels.length}:normalize=0[aout]`);
    audioOut = "[aout]";
  }

  const args = ["-y", "-i", inputPath];
  if (filters.length > 0) args.push("-filter_complex", filters.join(";"));
  args.push(
    ...(transcode ? ["-map", "[vout]", ...TRANSCODE_VIDEO] : ["-map", "0:v:0", "-c:v", "copy"]),
  );
  args.push(...(audioOut ? ["-map", audioOut, "-c:a", "aac", "-b:a", "192k"] : ["-an"]));
  args.push("-movflags", "+faststart", outputPath);
  return args;
}
