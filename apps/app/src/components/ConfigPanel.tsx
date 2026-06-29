import type { AppConfig } from "@qcksys/qlipq-core";
import type { CapturePresets } from "../lib/api.ts";
import * as api from "../lib/api.ts";

interface ConfigPanelProps {
  config: AppConfig;
  dirty: boolean;
  presets: CapturePresets;
  onChange: (patch: Partial<AppConfig>) => void;
  onSave: () => void;
  onReprocess: (folder: string) => void;
}

export function ConfigPanel({
  config,
  dirty,
  presets,
  onChange,
  onSave,
  onReprocess,
}: ConfigPanelProps) {
  const addFolder = async () => {
    const folder = await api.pickFolder();
    if (folder) addWatchedFolder(folder);
  };

  const addWatchedFolder = (folder: string) => {
    if (!config.watchedFolders.includes(folder)) {
      onChange({ watchedFolders: [...config.watchedFolders, folder] });
    }
  };

  const removeFolder = (folder: string) => {
    onChange({ watchedFolders: config.watchedFolders.filter((f) => f !== folder) });
  };

  const presetOptions: Array<{ label: string; folder: string }> = [
    ...(presets.obs ? [{ label: "OBS", folder: presets.obs }] : []),
    ...(presets.nvidiaShare ? [{ label: "NVIDIA Share", folder: presets.nvidiaShare }] : []),
  ].filter((p) => !config.watchedFolders.includes(p.folder));

  const pickOutput = async () => {
    const folder = await api.pickFolder();
    if (folder) onChange({ outputFolder: folder });
  };

  return (
    <div className="config">
      <section>
        <h3>Watched folders</h3>
        <p className="muted small">New recordings in these folders are added to the queue.</p>
        <ul className="folder-list">
          {config.watchedFolders.map((folder) => (
            <li key={folder}>
              <span title={folder}>{folder}</span>
              <span className="folder-actions">
                <button type="button" className="link" onClick={() => onReprocess(folder)}>
                  Reprocess
                </button>
                <button type="button" className="link" onClick={() => removeFolder(folder)}>
                  Remove
                </button>
              </span>
            </li>
          ))}
          {config.watchedFolders.length === 0 && <li className="muted">None yet.</li>}
        </ul>
        <div className="row wrap">
          <button type="button" onClick={addFolder}>
            Add folder…
          </button>
          {presetOptions.map((preset) => (
            <button
              key={preset.folder}
              type="button"
              title={preset.folder}
              onClick={() => addWatchedFolder(preset.folder)}
            >
              + {preset.label} ({preset.folder})
            </button>
          ))}
        </div>
      </section>

      <section>
        <h3>Output folder</h3>
        <div className="row">
          <input
            type="text"
            value={config.outputFolder}
            placeholder="Where exported clips are saved"
            onChange={(e) => onChange({ outputFolder: e.target.value })}
          />
          <button type="button" onClick={pickOutput}>
            Browse…
          </button>
        </div>
      </section>

      <section>
        <h3>Naming template</h3>
        <input
          type="text"
          value={config.namingTemplate}
          onChange={(e) => onChange({ namingTemplate: e.target.value })}
        />
        <p className="muted small">
          Tokens: <code>{"{date}"}</code> <code>{"{time}"}</code> <code>{"{datetime}"}</code>{" "}
          <code>{"{source}"}</code> <code>{"{name}"}</code> <code>{"{index}"}</code>
        </p>
      </section>

      <section>
        <h3>ffmpeg</h3>
        <label className="field">
          ffmpeg path
          <input
            type="text"
            value={config.ffmpegPath}
            onChange={(e) => onChange({ ffmpegPath: e.target.value })}
          />
        </label>
        <label className="field">
          ffprobe path
          <input
            type="text"
            value={config.ffprobePath}
            onChange={(e) => onChange({ ffprobePath: e.target.value })}
          />
        </label>
        <label className="inline">
          <input
            type="checkbox"
            checked={config.deleteSourceAfterExport}
            onChange={(e) => onChange({ deleteSourceAfterExport: e.target.checked })}
          />
          Delete source file after a successful export
        </label>
      </section>

      <button type="button" className="primary" disabled={!dirty} onClick={onSave}>
        {dirty ? "Save settings" : "Saved"}
      </button>
    </div>
  );
}
