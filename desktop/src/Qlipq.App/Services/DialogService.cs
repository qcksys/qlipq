using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Qlipq.Core;
using Qlipq.Host;
using Qlipq.App.ViewModels;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Qlipq.App.Services;

/// <summary>
/// Native folder picking, file-manager reveal, and "open externally", plus ContentDialog hosting.
/// Mirrors the Tauri dialog/opener plugin calls used by the frontend.
/// </summary>
public sealed class DialogService
{
    private Window? _window;

    public void Attach(Window window) => _window = window;

    private nint Hwnd => WindowNative.GetWindowHandle(_window
        ?? throw new InvalidOperationException("DialogService not attached to a window"));

    public Microsoft.UI.Xaml.XamlRoot? XamlRoot => _window?.Content?.XamlRoot;

    /// <summary>Open a native folder picker; returns the chosen path (forward slashes) or null.</summary>
    public async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, Hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder is null ? null : PathUtil.ToPosix(folder.Path);
    }

    /// <summary>Reveal a file in File Explorer (selecting it).</summary>
    public void RevealInExplorer(string path)
    {
        try { Process.Start("explorer.exe", $"/select,\"{path.Replace('/', '\\')}\""); }
        catch (Exception e) { Debug.WriteLine($"reveal failed: {e.Message}"); }
    }

    public void OpenInDefaultApp(string path) => ShellOpen(path);

    public void OpenExternal(string url) => ShellOpen(url);

    private static void ShellOpen(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) { Debug.WriteLine($"open failed: {e.Message}"); }
    }

    /// <summary>Show a ContentDialog rooted in the main window.</summary>
    public Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        dialog.XamlRoot = XamlRoot;
        return dialog.ShowAsync().AsTask();
    }

    /// <summary>A simple yes/no confirmation; true when the primary button is pressed.</summary>
    public async Task<bool> ConfirmAsync(string title, string content, string primaryText, string closeText = "Cancel")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Close,
        };
        return await ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    /// <summary>Three-button dialog returning Primary / Secondary / None (close).</summary>
    public Task<ContentDialogResult> ThreeWayAsync(
        string title, string content, string primaryText, string secondaryText, string closeText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primaryText,
            SecondaryButtonText = secondaryText,
            CloseButtonText = closeText,
            DefaultButton = ContentDialogButton.Primary,
        };
        return ShowAsync(dialog);
    }

    /// <summary>
    /// A dialog with one button per option (plus a "Keep"/close button). Returns the chosen
    /// option, or null when dismissed. Used for the after-export prompt (Keep/Rename/Move/Delete).
    /// </summary>
    public async Task<string?> ChooseActionAsync(string title, string content, string closeText, params string[] options)
    {
        string? chosen = null;
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap });

        var dialog = new ContentDialog { Title = title, CloseButtonText = closeText };
        foreach (var option in options)
        {
            var button = new Button { Content = option, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => { chosen = option; dialog.Hide(); };
            panel.Children.Add(button);
        }
        dialog.Content = panel;
        await ShowAsync(dialog);
        return chosen;
    }

    /// <summary>
    /// Rename prompt with a "Use template" helper. Returns the new file name (with extension)
    /// or null if cancelled. Mirrors RenameModal.tsx.
    /// </summary>
    public async Task<string?> PromptRenameAsync(string fileName, string? recordedAtIso, string? source, string namingTemplate)
    {
        var (name, ext) = Rename.SplitFileName(fileName);
        var box = new TextBox { Text = name, AcceptsReturn = false };
        var panel = new StackPanel { Spacing = 8 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(box);
        if (ext.Length > 0)
        {
            row.Children.Add(new TextBlock
            {
                Text = $".{ext}",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
        }
        panel.Children.Add(new TextBlock { Text = fileName, TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        panel.Children.Add(row);

        var dialog = new ContentDialog
        {
            Title = "Rename recording",
            Content = panel,
            PrimaryButtonText = "Rename",
            SecondaryButtonText = "Use template",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        dialog.SecondaryButtonClick += (_, e) =>
        {
            e.Cancel = true; // keep the dialog open; just fill the suggestion
            var recordedAt = IsoTime.ToLocal(recordedAtIso);
            var suggested = Rename.BuildRenamedFileName(namingTemplate,
                new RenameVars { Name = name, Ext = ext, RecordedAt = recordedAt, Source = source });
            box.Text = Rename.SplitFileName(suggested).Name;
        };

        if (await ShowAsync(dialog) != ContentDialogResult.Primary) return null;
        var trimmed = box.Text.Trim();
        if (trimmed.Length == 0) return null;
        return ext.Length > 0 ? $"{trimmed}.{ext}" : trimmed;
    }
}
