namespace Qlipq.Core;

/// <summary>A trim window. <see cref="EndSec"/> is exclusive (the cut ends at this timestamp).</summary>
public sealed record TrimSpec
{
    public double StartSec { get; set; }
    public double EndSec { get; set; }
}

/// <summary>A pixel-space crop rectangle relative to the source frame.</summary>
public sealed record CropSpec
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>Selection and level for one source audio track.</summary>
public sealed record AudioTrackSpec
{
    /// <summary>Audio-relative index matching <see cref="AudioStreamInfo.Index"/>.</summary>
    public int Index { get; set; }

    public bool Enabled { get; set; }

    /// <summary>Linear gain multiplier: 1 = unchanged, 0 = muted, 2 = +6dB.</summary>
    public double Volume { get; set; } = 1;
}

/// <summary>A complete description of the edits to apply to one clip.</summary>
public sealed record EditSpec
{
    public TrimSpec? Trim { get; set; }
    public CropSpec? Crop { get; set; }
    public List<AudioTrackSpec> AudioTracks { get; set; } = [];
}

public static class EditSpecs
{
    /// <summary>An edit spec that applies no changes, selecting every source audio track at unity gain.</summary>
    public static EditSpec DefaultEditSpec(MediaInfo? media = null)
    {
        return new EditSpec
        {
            AudioTracks = (media?.AudioStreams ?? [])
                .Select(stream => new AudioTrackSpec { Index = stream.Index, Enabled = true, Volume = 1 })
                .ToList(),
        };
    }

    /// <summary>The output duration in seconds after trimming, or the full duration when untrimmed.</summary>
    public static double EffectiveDuration(EditSpec spec, MediaInfo media)
    {
        if (spec.Trim is null) return media.DurationSec;
        return Math.Max(0, spec.Trim.EndSec - spec.Trim.StartSec);
    }

    /// <summary>Returns an error message if the spec is invalid for the given media, otherwise null.</summary>
    public static string? ValidateEditSpec(EditSpec spec, MediaInfo media)
    {
        if (spec.Trim is { } trim)
        {
            if (trim.StartSec < 0) return "Trim start cannot be negative.";
            if (trim.EndSec <= trim.StartSec) return "Trim end must be after the start.";
            if (trim.EndSec > media.DurationSec + 0.5) return "Trim end is beyond the clip duration.";
        }
        if (spec.Crop is { } crop)
        {
            if (crop.Width <= 0 || crop.Height <= 0) return "Crop width and height must be positive.";
            if (crop.X < 0 || crop.Y < 0) return "Crop position cannot be negative.";
            if (crop.X + crop.Width > media.Width || crop.Y + crop.Height > media.Height)
            {
                return "Crop rectangle extends outside the frame.";
            }
        }
        foreach (var track in spec.AudioTracks)
        {
            if (track.Volume < 0) return "Audio volume cannot be negative.";
        }
        // Disabling every audio track is allowed (produces a silent video); callers may warn.
        return null;
    }
}
