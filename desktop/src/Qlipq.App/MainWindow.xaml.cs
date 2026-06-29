using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Qlipq.App.Services;
using Qlipq.App.ViewModels;

namespace Qlipq.App;

public sealed partial class MainWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public MainWindow(ShellViewModel viewModel, DialogService dialogs)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Root.DataContext = viewModel;

        dialogs.Attach(this);

        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
        }

        _ = viewModel.InitializeAsync();
    }
}
