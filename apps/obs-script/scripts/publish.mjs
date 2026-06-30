// Publishes recorder.lua as the website's downloadable copy so the two never
// drift. `build` writes the copy; `test` (--check) fails if it is stale.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const source = resolve(here, "..", "recorder.lua");
const dest = resolve(here, "..", "..", "website", "public", "recorder.lua");

const src = readFileSync(source);

if (process.argv.includes("--check")) {
  let inSync = false;
  try {
    inSync = readFileSync(dest).equals(src);
  } catch {
    inSync = false;
  }
  if (!inSync) {
    console.error(
      `recorder.lua download is out of sync (${dest}).\n` +
        "Run `vp run qlipq-obs-script#build` to refresh it.",
    );
    process.exit(1);
  }
  console.log("recorder.lua download is in sync.");
} else {
  writeFileSync(dest, src);
  console.log(`Published recorder.lua -> ${dest}`);
}
