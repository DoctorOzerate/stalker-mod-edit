using System.Windows;
using XRayForge.Core.Storage;
using XRayForge.Services;

namespace XRayForge;

/// <summary>
/// Точка входа (этап 5): собирает сервисы (`AppDataService`, `ThemeService`,
/// `WpfDialogService`), применяет настройки профиля до появления окна
/// и открывает <see cref="MainWindow"/>.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var store = new AppDataService();
        var theme = new ThemeService(Resources);
        theme.ApplySettings(store.LoadProgramSettings());

        MainWindow = new MainWindow(new WpfDialogService(), theme);
        MainWindow.Show();
    }
}

