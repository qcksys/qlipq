using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Qlipq.App.ViewModels;

namespace Qlipq.App.Views;

public sealed partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;
    private ConfigViewModel? Config => Shell?.Config;

    private void OnReprocess(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string folder) Config?.ReprocessCommand.Execute(folder);
    }

    private void OnRemoveFolder(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is string folder) Config?.RemoveFolderCommand.Execute(folder);
    }

    private void OnAddPreset(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PresetOption preset) Config?.AddWatchedFolder(preset.Folder);
    }

    private void OnOpenConfig(object sender, RoutedEventArgs e) => Shell?.OpenConfigFileCommand.Execute(null);
}
