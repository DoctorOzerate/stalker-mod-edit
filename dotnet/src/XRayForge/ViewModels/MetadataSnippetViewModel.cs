using XRayForge.Core.Graph;

namespace XRayForge.ViewModels;

/// <summary>
/// Кнопка-сниппет блока «Действия и условия» (`META_SNIPPETS.map` в `DialoguesTab.js`):
/// подпись на кнопке и подсказка «ключ → значение». Клик дописывает значение в метаданные
/// реплики (`AddMetadataSnippetCommand`).
/// </summary>
public sealed class MetadataSnippetViewModel
{
    public MetadataSnippetViewModel(MetaSnippet snippet) => Snippet = snippet;

    /// <summary>Сниппет из Core — параметр команды добавления.</summary>
    public MetaSnippet Snippet { get; }

    /// <summary>Подпись кнопки (`+info`, `-info`, `выдать info`, …).</summary>
    public string Label => Snippet.Label;

    /// <summary>Подсказка кнопки: `key → value`.</summary>
    public string Tooltip => Snippet.Key + " → " + Snippet.Value;
}
