using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Qlipq.App.Services;
using Qlipq.App.ViewModels;
using Qlipq.Host;

namespace Qlipq.App;

public partial class App : Application
{
    /// <summary>App-wide DI container (services + the shell view-model).</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = ConfigureServices();

        // One-time migration of config/edits from the old Roaming AppData location.
        Services.GetRequiredService<AppPaths>().MigrateLegacyData();

        _window = Services.GetRequiredService<MainWindow>();
        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Host services (the ported Tauri commands).
        services.AddSingleton<AppPaths>();
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<ConfigStore>();
        services.AddSingleton<AppDataStore>();
        services.AddSingleton<MediaProbe>();
        services.AddSingleton<ExportRunner>();
        services.AddSingleton<CaptureDetect>();
        services.AddSingleton<FolderWatcher>();

        // App-layer services.
        services.AddSingleton<LibVlcService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<PlaybackStore>();

        // View-models.
        services.AddSingleton<ShellViewModel>();
        services.AddTransient<EditorViewModel>();

        // Windows.
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
