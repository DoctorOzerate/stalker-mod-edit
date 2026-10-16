using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using XRayForge.Core.Ui;
using XRayForge.Services;
using XRayForge.ViewModels;

namespace XRayForge;

/// <summary>
/// Окно оболочки (этап 5): панель инструментов, левая навигация, рабочая область
/// (обзор/раздел), приветственный экран и статусная строка. Хром свой
/// (`WindowChrome`), горячие клавиши идут через <see cref="HotkeyCatalog"/>
/// в <see cref="ShellViewModel.RunHotkey"/>.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ShellViewModel _shell;

    public MainWindow(IDialogService dialogs, ThemeService theme)
    {
        InitializeComponent();

        _shell = new ShellViewModel(dialogs, theme) { ActiveWindow = this };
        DataContext = _shell;

        Closed += (_, _) => _shell.Dispose();
    }

    // --- хром окна ---

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnStateChanged(object sender, EventArgs e) =>
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "▢";

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // --- горячие клавиши ---

    /// <summary>
    /// `Ctrl+1…9` переключают вкладки, остальные сочетания берутся из настроек программы
    /// (`HotkeyCatalog.MatchAction`). Ввод текста не перехватывается.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase) return;

        var name = KeyName(e.Key == Key.System ? e.SystemKey : e.Key);
        if (name == null) return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;

        var tab = HotkeyCatalog.TabForNumber(ctrl, shift, alt, name);
        if (tab != null)
        {
            _shell.ActivateTabById(tab.Id);
            e.Handled = true;
            return;
        }

        var action = HotkeyCatalog.MatchAction(_shell.Settings.Hotkeys, ctrl, shift, alt, name);
        if (action == null) return;

        _shell.RunHotkey(action);
        e.Handled = true;
    }

    /// <summary>Клавиша → токен комбинации (`s`, `1`, `delete`, `escape`, …).</summary>
    private static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString().ToLowerInvariant(),
        >= Key.D0 and <= Key.D9 => key.ToString().Substring(1),
        >= Key.NumPad0 and <= Key.NumPad9 => key.ToString().Substring("NumPad".Length),
        Key.Delete => "delete",
        Key.Escape => "escape",
        Key.Back => "backspace",
        Key.Enter => "enter",
        _ => null
    };
}