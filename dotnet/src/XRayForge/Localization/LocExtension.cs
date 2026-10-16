using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Markup;
using XRayForge.Core.Localization;

namespace XRayForge.Localization;

/// <summary>
/// Строки интерфейса для XAML: источник с индексером, который отдаёт `LocalizationService.Tr`
/// по ключу. Смена локали уведомляет привязки (`Item[]`).
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    /// <summary>Единственный экземпляр-источник (`{x:Static loc:LocalizationSource.Instance}`).</summary>
    public static LocalizationSource Instance { get; } = new();

    private string _locale = "ru";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Текущая локаль интерфейса (`ru`/`en`), как `programSettings.locale`.</summary>
    public string Locale
    {
        get => _locale;
        set
        {
            var next = value == "en" ? "en" : "ru";
            if (next == _locale) return;
            _locale = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Locale)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    /// <summary>Строка по ключу словаря Core (`{Binding [tabDialogues], Source=…}`).</summary>
    public string this[string key] => LocalizationService.Tr(_locale, key);

    /// <summary>Строка по ключу вне XAML (статусы, диалоги, подписи кнопок).</summary>
    public static string Get(string key) => LocalizationService.Tr(Instance._locale, key);
}

/// <summary>Разметка `{loc:Loc tabDialogues}` — привязка к <see cref="LocalizationSource"/>.</summary>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    /// <summary>Ключ словаря локализации (без `appTitle` — там `Ru`/`En` совпадают).</summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Индексер <see cref="LocalizationSource"/> отдаёт строку по ключу и пересчитывается
    /// при смене локали (`Item[]`). Привязка применяется через `Binding.ProvideValue`,
    /// чтобы XAML не пытался привести `Binding` к типу свойства.
    /// </summary>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding("[" + Key + "]")
        {
            Source = LocalizationSource.Instance,
            Mode = BindingMode.OneWay
        };

        return binding.ProvideValue(serviceProvider);
    }
}
