using Qlipq.Core;

namespace Qlipq.App.ViewModels;

/// <summary>
/// Maps domain enums to/from the lowercase string keys used as ComboBoxItem <c>Tag</c>s
/// (which match the on-disk JSON values), so XAML can two-way bind via SelectedValue.
/// </summary>
public static class EnumKeys
{
    public static string QualityMode(QualityMode v) => v switch
    {
        Core.QualityMode.Preset => "preset",
        Core.QualityMode.Crf => "crf",
        Core.QualityMode.Bitrate => "bitrate",
        Core.QualityMode.Vbr => "vbr",
        _ => "preset",
    };

    public static QualityMode QualityMode(string key) => key switch
    {
        "crf" => Core.QualityMode.Crf,
        "bitrate" => Core.QualityMode.Bitrate,
        "vbr" => Core.QualityMode.Vbr,
        _ => Core.QualityMode.Preset,
    };

    public static string QualityPreset(QualityPreset v) => v switch
    {
        Core.QualityPreset.Original => "original",
        Core.QualityPreset.High => "high",
        Core.QualityPreset.Balanced => "balanced",
        Core.QualityPreset.Small => "small",
        _ => "original",
    };

    public static QualityPreset QualityPreset(string key) => key switch
    {
        "high" => Core.QualityPreset.High,
        "balanced" => Core.QualityPreset.Balanced,
        "small" => Core.QualityPreset.Small,
        _ => Core.QualityPreset.Original,
    };

    public static string VideoCodec(VideoCodecChoice v) =>
        v == VideoCodecChoice.Libx265 ? "libx265" : "libx264";

    public static VideoCodecChoice VideoCodec(string key) =>
        key == "libx265" ? VideoCodecChoice.Libx265 : VideoCodecChoice.Libx264;

    public static string Container(ContainerFormat v) => v == ContainerFormat.Mkv ? "mkv" : "mp4";

    public static ContainerFormat Container(string key) => key == "mkv" ? ContainerFormat.Mkv : ContainerFormat.Mp4;

    public static string AfterExport(AfterExportAction v) => v switch
    {
        AfterExportAction.Delete => "delete",
        AfterExportAction.Move => "move",
        AfterExportAction.Rename => "rename",
        AfterExportAction.Prompt => "prompt",
        _ => "nothing",
    };

    public static AfterExportAction AfterExport(string key) => key switch
    {
        "delete" => AfterExportAction.Delete,
        "move" => AfterExportAction.Move,
        "rename" => AfterExportAction.Rename,
        "prompt" => AfterExportAction.Prompt,
        _ => AfterExportAction.Nothing,
    };
}
