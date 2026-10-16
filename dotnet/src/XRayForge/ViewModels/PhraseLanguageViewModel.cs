using XRayForge.Core.Models;

namespace XRayForge.ViewModels;

/// <summary>
/// Локаль таблицы переводов в панели свойств реплики (ряд кнопок `langs.map` в `DialoguesTab.js`):
/// код локали, её имя и подпись для выпадающего списка. Активная локаль хранится в
/// <see cref="DialogueGraphViewModel.ActivePhraseLocale"/>.
/// </summary>
public sealed class PhraseLanguageViewModel
{
    public PhraseLanguageViewModel(Language language)
    {
        Locale = language.Locale;
        Name = language.Name;
    }

    /// <summary>Код локали (`rus`, `eng`, …) — значение для выбора активной таблицы.</summary>
    public string Locale { get; }

    /// <summary>Человекочитаемое имя таблицы (`Русский`).</summary>
    public string Name { get; }

    /// <summary>Подпись кнопки локали (`lang.locale || '?'`).</summary>
    public string Display => Locale.Length > 0 ? Locale : "?";

    /// <summary>Подсказка: код локали и имя таблицы (в JS — только код).</summary>
    public string Tooltip => Name.Length > 0 ? Display + " · " + Name : Display;
}
