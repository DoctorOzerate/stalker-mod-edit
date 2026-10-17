using CommunityToolkit.Mvvm.ComponentModel;
using XRayForge.Core.Suggestions;

namespace XRayForge.ViewModels;

/// <summary>
/// Многострочное поле одного ключа метаданных реплики (`has_info`, `precondition`, `action`, …)
/// в блоке «Действия и условия»: строки хранятся списком (`split(/\r?\n)` в JS), а поле правит
/// их через <see cref="DialogueGraphViewModel.SetMetadataText"/> (`patchNode`).
/// </summary>
public sealed partial class MetadataFieldViewModel : ObservableObject
{
    private readonly DialogueGraphViewModel _owner;
    private bool _loading;

    public MetadataFieldViewModel(DialogueGraphViewModel owner, string key)
    {
        _owner = owner;
        Key = key;
        Label = key;
    }

    /// <summary>Ключ метаданных (`META_KEYS`); подпись поля — сам ключ, как в оригинале.</summary>
    public string Key { get; }

    /// <summary>Подпись поля (совпадает с ключом).</summary>
    public string Label { get; }

    /// <summary>Подсказки по ключу: info-порции для `*_info`, скрипты для `precondition`/`action`.</summary>
    public IReadOnlyList<SuggestionEntry> Suggestions => _owner.SuggestionsForMetadata(Key);

    /// <summary>Содержимое поля: `(metadata[key] || []).join('\n')`.</summary>
    [ObservableProperty]
    private string _text = "";

    /// <summary>Уведомить UI после пересборки библиотеки при смене проекта.</summary>
    public void RefreshSuggestions() => OnPropertyChanged(nameof(Suggestions));

    partial void OnTextChanged(string value)
    {
        if (_loading) return;
        _owner.SetMetadataText(Key, value);
    }

    /// <summary>Показать значение из модели, не помечая это правкой (`patchNode` не вызывается).</summary>
    public void Load(string value)
    {
        _loading = true;
        Text = value;
        _loading = false;
    }
}
