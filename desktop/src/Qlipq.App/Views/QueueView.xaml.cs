using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Qlipq.App.ViewModels;

namespace Qlipq.App.Views;

public sealed partial class QueueView : UserControl
{
    public QueueView()
    {
        InitializeComponent();
    }

    private ShellViewModel? Vm => DataContext as ShellViewModel;

    private static QueueItemViewModel? ItemOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as QueueItemViewModel;

    private void OnRename(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) Vm?.RenameCommand.Execute(item);
    }

    private void OnDismiss(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) Vm?.DismissCommand.Execute(item);
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item) Vm?.DeleteCommand.Execute(item);
    }

    private void OnTagAll(object sender, RoutedEventArgs e) => Vm?.SetTagFilterCommand.Execute(null);

    private void OnTagFilter(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Content is string tag) Vm?.SetTagFilterCommand.Execute(tag);
    }
}
