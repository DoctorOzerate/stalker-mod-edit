using System.Globalization;
using XRayForge.Core.Graph;
using XRayForge.Core.Models;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>
/// Кнопка диалога в списке «Загруженные диалоги» (`project.dialogues.map` в `DialoguesTab.js`):
/// подпись (`name || id`), счётчики узлов и связей; клик грузит диалог в холст
/// (`loadDialogueIntoGraph`).
/// </summary>
public sealed class DialogueListItemViewModel
{
    public DialogueListItemViewModel(DialogueGraph dialogue)
    {
        Dialogue = dialogue;

        var name = DialogueGroups.Name(dialogue);
        Name = name.Length > 0 ? name : LocalizationSource.Get("graphDialogueFallback");

        var nodes = dialogue.Nodes?.Count ?? 0;
        var edges = dialogue.Edges?.Count ?? 0;
        Meta = nodes.ToString(CultureInfo.CurrentCulture) + " " + LocalizationSource.Get("graphStatNodes") +
               " · " + edges.ToString(CultureInfo.CurrentCulture) + " " + LocalizationSource.Get("graphStatEdges");
    }

    /// <summary>Диалог проекта — параметр команды загрузки в холст.</summary>
    public DialogueGraph Dialogue { get; }

    /// <summary>Подпись кнопки (`d.name || d.id`).</summary>
    public string Name { get; }

    /// <summary>Вторая строка кнопки: «3 нод · 2 связей».</summary>
    public string Meta { get; }
}

/// <summary>
/// Группа списка загруженных диалогов (`dialogueGroup` в `graph.js`): заголовок вида «Бар · 2»
/// и кнопки диалогов. Группы отсортированы так же, как `Object.keys(groups).sort()`.
/// </summary>
public sealed class DialogueBucketViewModel
{
    public DialogueBucketViewModel(DialogueBucket bucket)
    {
        Group = bucket.Group;
        Header = bucket.Group + " · " + bucket.Dialogues.Count.ToString(CultureInfo.CurrentCulture);
        Items = bucket.Dialogues.Select(dialogue => new DialogueListItemViewModel(dialogue)).ToList();
    }

    /// <summary>Подпись группы (локация, персонаж или тип диалога).</summary>
    public string Group { get; }

    /// <summary>Заголовок группы с числом диалогов (`g + ' · ' + groups[g].length`).</summary>
    public string Header { get; }

    /// <summary>Диалоги группы в порядке появления в проекте.</summary>
    public List<DialogueListItemViewModel> Items { get; }
}
