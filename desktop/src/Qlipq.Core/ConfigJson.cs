using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qlipq.Core;

/// <summary>
/// Lenient load / pretty save of <c>config.json</c>, mirroring the original Rust host
/// (<c>get_config</c>/<c>set_config</c>): missing fields keep defaults, a present-but-bad
/// field reverts to its default (the serde_with <c>DefaultOnError</c> behaviour), enum/range
/// values are validated and repaired (garde + <c>normalize_config</c>), and a <c>$schema</c>
/// reference is stamped on write. Pure (string in/out); file I/O lives in the host.
/// </summary>
public static class ConfigJson
{
    public const string SchemaUrl = "https://qlipq.com/schema/config.json";

    /// <summary>Parse config JSON onto defaults, tolerating missing/invalid fields.</summary>
    public static AppConfig Parse(string text)
    {
        var cfg = new AppConfig();
        JsonObject? obj;
        try { obj = JsonNode.Parse(text) as JsonObject; }
        catch (JsonException) { return cfg; }
        if (obj is null) return cfg;

        if (ReadStringList(obj, "watchedFolders") is { } wf) cfg.WatchedFolders = wf;
        if (ReadString(obj, "outputFolder") is { } of) cfg.OutputFolder = of;
        if (ReadStringList(obj, "videoExtensions") is { } ve) cfg.VideoExtensions = ve;
        if (ReadString(obj, "namingTemplate") is { } nt) cfg.NamingTemplate = nt;
        if (ReadString(obj, "ffmpegPath") is { } fp) cfg.FfmpegPath = fp;
        if (ReadString(obj, "ffprobePath") is { } pp) cfg.FfprobePath = pp;

        if (obj["afterExport"] is JsonObject ae)
        {
            var a = cfg.AfterExport;
            a.Action = ParseAfterExportAction(ReadString(ae, "action"), a.Action);
            if (ReadString(ae, "moveFolder") is { } mf) a.MoveFolder = mf;
            if (ReadString(ae, "renamePrefix") is { } rp) a.RenamePrefix = rp;
            if (ReadString(ae, "renameSuffix") is { } rs) a.RenameSuffix = rs;
        }

        if (obj["output"] is JsonObject o)
        {
            var s = cfg.Output;
            s.QualityMode = ParseQualityMode(ReadString(o, "qualityMode"), s.QualityMode);
            s.QualityPreset = ParseQualityPreset(ReadString(o, "qualityPreset"), s.QualityPreset);
            // garde range(0,51): a negative reverts to default (u32 parse fails); >51 clamps.
            if (ReadInt(o, "crf") is { } crf) s.Crf = crf < 0 ? 20 : Math.Min(crf, 51);
            if (ReadInt(o, "videoBitrateKbps") is { } vb) s.VideoBitrateKbps = vb;
            if (ReadString(o, "encoderPreset") is { } ep) s.EncoderPreset = ep;
            s.VideoCodec = ParseVideoCodec(ReadString(o, "videoCodec"), s.VideoCodec);
            s.Container = ParseContainer(ReadString(o, "container"), s.Container);
            if (ReadInt(o, "fps") is { } fps) s.Fps = fps;
            if (ReadInt(o, "maxHeight") is { } mh) s.MaxHeight = mh;
            if (ReadInt(o, "audioBitrateKbps") is { } ab) s.AudioBitrateKbps = ab;
        }

        return cfg;
    }

    /// <summary>Serialize to pretty JSON with a leading <c>$schema</c> reference.</summary>
    public static string Serialize(AppConfig config)
    {
        var node = JsonSerializer.SerializeToNode(config, QlipqJson.Options)!.AsObject();
        var ordered = new JsonObject { ["$schema"] = SchemaUrl };
        foreach (var key in node.Select(kv => kv.Key).ToList())
        {
            var value = node[key];
            node.Remove(key);
            ordered[key] = value;
        }
        return ordered.ToJsonString(QlipqJson.IndentedOptions);
    }

    private static string? ReadString(JsonObject obj, string key) =>
        obj[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int? ReadInt(JsonObject obj, string key)
    {
        if (obj[key] is not JsonValue v) return null;
        if (v.TryGetValue<int>(out var i)) return i;
        if (v.TryGetValue<double>(out var d)) return (int)d;
        return null;
    }

    private static List<string>? ReadStringList(JsonObject obj, string key)
    {
        if (obj[key] is not JsonArray arr) return null;
        var list = new List<string>();
        foreach (var n in arr)
        {
            if (n is JsonValue v && v.TryGetValue<string>(out var s)) list.Add(s);
        }
        return list;
    }

    private static AfterExportAction ParseAfterExportAction(string? s, AfterExportAction def) => s switch
    {
        "nothing" => AfterExportAction.Nothing,
        "delete" => AfterExportAction.Delete,
        "move" => AfterExportAction.Move,
        "rename" => AfterExportAction.Rename,
        "prompt" => AfterExportAction.Prompt,
        _ => def,
    };

    private static QualityMode ParseQualityMode(string? s, QualityMode def) => s switch
    {
        "preset" => QualityMode.Preset,
        "crf" => QualityMode.Crf,
        "bitrate" => QualityMode.Bitrate,
        "vbr" => QualityMode.Vbr,
        _ => def,
    };

    private static QualityPreset ParseQualityPreset(string? s, QualityPreset def) => s switch
    {
        "original" => QualityPreset.Original,
        "high" => QualityPreset.High,
        "balanced" => QualityPreset.Balanced,
        "small" => QualityPreset.Small,
        _ => def,
    };

    private static VideoCodecChoice ParseVideoCodec(string? s, VideoCodecChoice def) => s switch
    {
        "libx264" => VideoCodecChoice.Libx264,
        "libx265" => VideoCodecChoice.Libx265,
        _ => def,
    };

    private static ContainerFormat ParseContainer(string? s, ContainerFormat def) => s switch
    {
        "mp4" => ContainerFormat.Mp4,
        "mkv" => ContainerFormat.Mkv,
        _ => def,
    };
}
