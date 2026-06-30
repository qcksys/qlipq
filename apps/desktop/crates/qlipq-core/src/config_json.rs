//! Load / pretty-save of `config.json`. Parsing is plain serde: missing fields keep their defaults
//! (`#[serde(default)]` on the config types) and the extra `$schema` key is ignored. The JSON Schema
//! is **derived from the `AppConfig` type by `schemars`**, so it stays in sync with the struct
//! automatically — there is no hand-written schema to keep aligned.

use serde_json::Value;

use crate::config::AppConfig;

/// Filename of the JSON Schema the app writes next to `config.json`. `config.json`'s `$schema`
/// points at it with a relative path, so editors validate against the local copy (no network).
pub const SCHEMA_FILE: &str = "config.schema.json";

/// Parse `config.json` onto defaults. Missing fields (and the extra `$schema` key) are tolerated by
/// the config types' `#[serde(default)]`; a document malformed enough that serde rejects it falls back
/// to the full default config.
pub fn parse(text: &str) -> AppConfig {
    serde_json::from_str(text).unwrap_or_default()
}

/// Serialize to pretty JSON with a leading `$schema` reference (keys are sorted, so `$` sorts first).
pub fn serialize(cfg: &AppConfig) -> String {
    let mut value = serde_json::to_value(cfg).expect("AppConfig serializes");
    if let Value::Object(ref mut map) = value {
        map.insert("$schema".to_string(), Value::String(format!("./{SCHEMA_FILE}")));
    }
    serde_json::to_string_pretty(&value).expect("Value serializes")
}

/// The JSON Schema for [`AppConfig`], derived from the type by `schemars` (camelCase keys matching the
/// serialized `config.json`). The single source of truth; the app writes it to disk next to the config.
pub fn config_schema() -> Value {
    serde_json::to_value(schemars::schema_for!(AppConfig)).expect("schema serializes to Value")
}

/// [`config_schema`] serialized as the pretty JSON the app writes to disk.
pub fn schema_json() -> String {
    serde_json::to_string_pretty(&config_schema()).expect("schema serializes")
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::config::AppConfig;

    #[test]
    fn round_trips_defaults() {
        let cfg = AppConfig::default();
        assert_eq!(parse(&serialize(&cfg)), cfg);
    }

    #[test]
    fn missing_fields_default_and_schema_key_ignored() {
        let cfg = parse(r#"{ "$schema": "./config.schema.json", "outputFolder": "E:/Out" }"#);
        assert_eq!(cfg, AppConfig { output_folder: "E:/Out".into(), ..Default::default() });
    }

    #[test]
    fn partial_nested_object_fills_defaults() {
        let cfg = parse(r#"{ "output": { "crf": 30 } }"#);
        assert_eq!(cfg.output.crf, 30);
        // Unspecified nested fields fall back to the struct default.
        assert_eq!(cfg.output.encoder_preset, AppConfig::default().output.encoder_preset);
    }

    #[test]
    fn keybinds_default_to_premiere() {
        let cfg = parse("{}");
        assert_eq!(cfg.keybinds.play_pause, "Space");
        assert_eq!(cfg.keybinds.set_in, "I");
        assert_eq!(cfg.keybinds.export, "Ctrl+M");
    }

    #[test]
    fn malformed_falls_back_to_default() {
        assert_eq!(parse("not json at all"), AppConfig::default());
    }

    #[test]
    fn derived_schema_has_camelcase_keys_enums_and_crf_range() {
        let schema = serde_json::to_string(&config_schema()).unwrap();
        for key in ["watchedFolders", "outputFolder", "qualityMode", "videoCodec", "keybinds", "playPause"] {
            assert!(schema.contains(key), "schema missing key {key}");
        }
        assert!(schema.contains("preset") && schema.contains("vbr"), "enum values missing");
        assert!(schema.contains("\"maximum\":51"), "crf range not carried into schema");
    }
}

