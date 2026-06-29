using Qlipq.Core;

namespace Qlipq.App.ViewModels;

/// <summary>
/// Per-file edit state persisted to <c>edits.json</c> (keyed by path), matching the web app's
/// <c>StoredEdit</c> shape so existing files load unchanged.
/// </summary>
public sealed class StoredEdit
{
    public EditSpec? Edit { get; set; }
    public OutputOverride? OutputOverride { get; set; }
    public List<string>? Tags { get; set; }
}
