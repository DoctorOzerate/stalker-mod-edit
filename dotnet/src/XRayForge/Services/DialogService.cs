using System.IO;
using System.Windows;
using Microsoft.Win32;
using XRayForge.Localization;
using XRayForge.Views;

namespace XRayForge.Services;

/// <summary>Диалоги оболочки за интерфейсом: ViewModels не зависят от окон WPF.</summary>
public interface IDialogService
{
    /// <summary>Подтверждение действия (`removeProject` в JS спрашивает подтверждение).</summary>
    bool Confirm(string message, string? title = null);

    /// <summary>Ввод одной строки (имя нового проекта, URL синхронизации).</summary>
    string? Prompt(string title, string label, string initial);

    /// <summary>Выбор каталога (`showDirectoryPicker` gamedata).</summary>
    string? PickFolder(string? initialDirectory);

    /// <summary>Открытие файла с произвольным фильтром (импорт `game_dialogs*.xml`).</summary>
    string? PickOpenFile(string title, string filter, string? initialDirectory);

    /// <summary>Открытие файла `.json` (импорт проекта/настроек).</summary>
    string? PickOpenJson(string title, string? initialDirectory);

    /// <summary>Сохранение файла `.json` (экспорт настроек).</summary>
    string? PickSaveJson(string title, string suggestedName, string? initialDirectory);

    /// <summary>Открытие файла с произвольным фильтром (экспорт XML редактора).</summary>
    string? PickSaveFile(string title, string suggestedName, string filter, string? initialDirectory);

    /// <summary>Информационное сообщение (статистика сканирования, пропущенные файлы).</summary>
    void ShowMessage(string title, string message);
}

/// <summary>Реализация диалогов на `MessageBox`/`InputWindow` и стандартных диалогах оболочки.</summary>
public sealed class WpfDialogService : IDialogService
{
    public bool Confirm(string message, string? title = null) =>
        MessageBox.Show(Owner(), message, title ?? LocalizationSource.Get("appTitle"),
            MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;

    public string? Prompt(string title, string label, string initial)
    {
        var window = new InputWindow(title, label, initial);
        var owner = Owner();
        if (owner != null && !ReferenceEquals(owner, window)) window.Owner = owner;
        return window.ShowDialog() == true ? window.Value : null;
    }

    public string? PickFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog { Title = LocalizationSource.Get("chooseFolder") };
        SetInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(Owner()) == true ? dialog.FolderName : null;
    }

    public string? PickOpenFile(string title, string filter, string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };
        SetInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(Owner()) == true ? dialog.FileName : null;
    }

    public string? PickOpenJson(string title, string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "*.json|*.json|*.*|*.*",
            CheckFileExists = true
        };
        SetInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(Owner()) == true ? dialog.FileName : null;
    }

    public string? PickSaveJson(string title, string suggestedName, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = suggestedName,
            DefaultExt = ".json",
            Filter = "X-Ray Forge (*.json)|*.json|*.*|*.*"
        };
        SetInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(Owner()) == true ? dialog.FileName : null;
    }

    public string? PickSaveFile(string title, string suggestedName, string filter, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = suggestedName,
            Filter = filter
        };
        SetInitialDirectory(dialog, initialDirectory);
        return dialog.ShowDialog(Owner()) == true ? dialog.FileName : null;
    }

    public void ShowMessage(string title, string message) =>
        MessageBox.Show(Owner(), message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    private static void SetInitialDirectory(FileDialog dialog, string? initialDirectory)
    {
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
    }

    private static void SetInitialDirectory(OpenFolderDialog dialog, string? initialDirectory)
    {
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;
    }

    /// <summary>Активное окно приложения — владелец модальных диалогов.</summary>
    private static Window? Owner()
    {
        var app = Application.Current;
        if (app == null) return null;
        foreach (Window window in app.Windows)
            if (window.IsActive) return window;
        return app.MainWindow;
    }
}
