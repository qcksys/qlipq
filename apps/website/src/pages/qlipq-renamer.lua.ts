import script from "@qcksys/qlipq-obs-script/qlipq-renamer.lua?raw";
import type { APIRoute } from "astro";

// Serves the OBS companion script at /qlipq-renamer.lua, sourced directly from
// the @qcksys/qlipq-obs-script package (single source of truth — no copy in public/).
export const GET: APIRoute = () =>
  new Response(script, {
    headers: { "content-type": "text/plain; charset=utf-8" },
  });
