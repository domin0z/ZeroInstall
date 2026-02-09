using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using ZeroInstall.App.Infrastructure;
using ZeroInstall.App.Services;
using ZeroInstall.App.ViewModels;

namespace ZeroInstall.App;

/// <summary>
/// Application startup — builds DI host and shows the main window.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = AppHost.BuildHost();
        _host.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var viewModel = _host.Services.GetRequiredService<MainWindowViewModel>();
        mainWindow.DataContext = viewModel;
        mainWindow.Show();

        // Navigate to welcome screen
        var nav = _host.Services.GetRequiredService<INavigationService>();
        nav.NavigateTo<WelcomeViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
