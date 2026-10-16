using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XRayForge.Core.Storage;
using XRayForge.Core.Ui;

namespace XRayForge.ViewModels;

/// <summary>Акцентный цвет темы в списке настроек (`accentGreen`, …).</summary>
public sealed record AccentOption(string Id, string LabelKey);

/// <summary>Плотность интерфейса (`compact`/`normal`/`roomy`).</summary>
public sealed record DensityOption(string Id, string LabelKey);

/// <summary>Язык интерфейса (`ru`/`en`).</summary>
public sealed record LocaleOption(string Id, string Label);

/// <summary>
/// Модель окна «Настройки программы» (`programSettings`): тема, акцент, плотность, язык,
/// автосейв, шрифт, узлы, синхронизация и таблица горячих клавиш. Правки применяются сразу
/// (<see cref="ShellViewModel.ApplyProgramSettings"/>), «Сохранить» пишет `settings.json`,
/// «Отмена» возвращает значения профиля.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    public SettingsViewModel(ShellViewModel shell)
    {
        _shell = shell;
        Settings = shell.Settings;

        Themes = new ObservableCollection<ThemeDefinition>(ThemeCatalog.Themes);
        Densities = new ObservableCollection<DensityOption>(ProgramSettingsCatalog.Densities
            .Select(id => new DensityOption(id, id)));
        Locales = new ObservableCollection<LocaleOption>(new[]
        {
            new LocaleOption("ru", "Русский"),
            new LocaleOption("en", "English")
        });

        _selectedTheme = Themes.FirstOrDefault(theme => theme.Id == ThemeCatalog.NormalizeThemeId(Settings.Theme)) ?? Themes[0];
        BuildAccents();
        _selectedDensity = Densities.FirstOrDefault(item => item.Id == Settings.Density) ?? Densities[1];
        _selectedLocale = Locales.FirstOrDefault(item => item.Id == Settings.Locale) ?? Locales[0];
        _autosaveInterval = Settings.AutosaveInterval ?? 0;
        _editorFontSize = Settings.EditorFontSize;
        _showNodeText = Settings.ShowNodeText;
        _syncUrl = Settings.SyncUrl;
        Hotkeys = new ObservableCollection<HotkeyBinding>(shell.Hotkeys);
    }

    /// <summary>Настройки программы (правятся на месте).</summary>
    public ProgramSettings Settings { get; }

    /// <summary>Темы `ThemeCatalog.Themes` (Steel, Olive, Hazard).</summary>
    public ObservableCollection<ThemeDefinition> Themes { get; }

    /// <summary>Плотности `ProgramSettingsCatalog.Densities`.</summary>
    public ObservableCollection<DensityOption> Densities { get; }

    /// <summary>Языки интерфейса.</summary>
    public ObservableCollection<LocaleOption> Locales { get; }

    /// <summary>Сочетания клавиш (`HotkeyCatalog.Bindings`).</summary>
    public ObservableCollection<HotkeyBinding> Hotkeys { get; private set; }

    /// <summary>Конфликты сочетаний: id действия → чужая комбинация.</summary>
    public IReadOnlyDictionary<string, string> HotkeyConflicts => _shell.HotkeyDuplicates;

    /// <summary>Есть конфликтующие сочетания.</summary>
    public bool HasConflicts => HotkeyConflicts.Count > 0;

    // --- команды оболочки (экспорт/импорт профиля, синхронизация, плагины) ---

    public ICommand ExportSettingsCommand => _shell.ExportSettingsCommand;
    public ICommand ImportSettingsCommand => _shell.ImportSettingsCommand;
    public ICommand ResetDataCommand => _shell.ResetDataCommand;
    public ICommand SyncNowCommand => _shell.SyncNowCommand;
    public ICommand SyncLoadCommand => _shell.SyncLoadCommand;
    public ICommand RunPluginsCommand => _shell.RunPluginsCommand;

    /// <summary>Применить текущие значения к оболочке (кнопка «Применить»).</summary>
    public void Apply() => _shell.ApplyProgramSettings();

    /// <summary>Записать настройки в профиль (кнопка «Сохранить»).</summary>
    public void Save() => _shell.SaveProgramSettings();

    /// <summary>Вернуть значения профиля (кнопка «Отмена»).</summary>
    public void Cancel() => _shell.ReloadProgramSettings();

    // --- наблюдаемые свойства ---

    /// <summary>Акценты выбранной темы.</summary>
    [ObservableProperty]
    private ObservableCollection<AccentOption> _accents = new();

    /// <summary>Выбранная тема.</summary>
    [ObservableProperty]
    private ThemeDefinition _selectedTheme;

    /// <summary>Выбранный акцентный цвет.</summary>
    [ObservableProperty]
    private AccentOption? _selectedAccent;

    /// <summary>Выбранная плотность.</summary>
    [ObservableProperty]
    private DensityOption _selectedDensity;

    /// <summary>Выбранный язык интерфейса.</summary>
    [ObservableProperty]
    private LocaleOption _selectedLocale;

    /// <summary>Интервал автосохранения в секундах (0 — выключено).</summary>
    [ObservableProperty]
    private double _autosaveInterval;

    /// <summary>Размер шрифта редактора.</summary>
    [ObservableProperty]
    private double _editorFontSize;

    /// <summary>Показывать текст реплик в узлах графа.</summary>
    [ObservableProperty]
    private bool _showNodeText;

    /// <summary>URL сервера синхронизации.</summary>
    [ObservableProperty]
    private string _syncUrl;

    partial void OnSelectedThemeChanged(ThemeDefinition value)
    {
        if (value == null) return;
        Settings.Theme = value.Id;
        BuildAccents();
        Apply();
    }

    partial void OnSelectedAccentChanged(AccentOption? value)
    {
        if (value == null) return;
        Settings.Accent = value.Id;
        Apply();
    }

    partial void OnSelectedDensityChanged(DensityOption value)
    {
        if (value == null) return;
        Settings.Density = value.Id;
        Apply();
    }

    partial void OnSelectedLocaleChanged(LocaleOption value)
    {
        if (value == null) return;
        Settings.Locale = value.Id;
        Apply();
    }

    partial void OnAutosaveIntervalChanged(double value)
    {
        Settings.AutosaveInterval = value;
        Apply();
    }

    partial void OnEditorFontSizeChanged(double value)
    {
        Settings.EditorFontSize = double.IsFinite(value) ? value : 13;
        Apply();
    }

    partial void OnShowNodeTextChanged(bool value)
    {
        Settings.ShowNodeText = value;
        Apply();
    }

    partial void OnSyncUrlChanged(string value) => Settings.SyncUrl = value ?? "";

    /// <summary>Сбросить горячие клавиши к значениям по умолчанию (`resetHotkey`).</summary>
    [RelayCommand]
    private void ResetHotkeys()
    {
        Settings.Hotkeys = ProgramSettingsCatalog.DefaultHotkeys();
        Hotkeys = new ObservableCollection<HotkeyBinding>(_shell.Hotkeys);
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(HotkeyConflicts));
        Apply();
    }

    /// <summary>Акценты выбранной темы: id плюс ключ локализации (`accentBlue`, …).</summary>
    private void BuildAccents()
    {
        var themeId = SelectedTheme?.Id ?? "";
        var ids = ThemeCatalog.Accents.TryGetValue(themeId, out var list) ? list : Array.Empty<string>();

        Accents = new ObservableCollection<AccentOption>(ids.Select(id => new AccentOption(id, AccentKey(id))));
        var current = ThemeCatalog.NormalizeAccentId(themeId, Settings.Accent);
        SelectedAccent = Accents.FirstOrDefault(item => item.Id == current) ?? Accents.FirstOrDefault();
    }

    /// <summary>`green` → `accentGreen`.</summary>
    private static string AccentKey(string id) =>
        id.Length == 0 ? id : "accent" + char.ToUpperInvariant(id[0]) + id[1..];
}
