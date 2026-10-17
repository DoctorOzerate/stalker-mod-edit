using CommunityToolkit.Mvvm.ComponentModel;
using XRayForge.Core.Characters;
using XRayForge.Core.Models;
using XRayForge.Core.Ltx;
using XRayForge.Localization;

namespace XRayForge.ViewModels;

/// <summary>Группа списка персонажей: подпись community и карточки (`groups[g]` в JS).</summary>
public sealed class CharacterGroupViewModel
{
    public CharacterGroupViewModel(string header, IReadOnlyList<CharacterViewModel> items)
    {
        Header = header;
        Items = items;
    }

    /// <summary>`👤 G · N` — заголовок группы в левой панели.</summary>
    public string Header { get; }

    public IReadOnlyList<CharacterViewModel> Items { get; }
}

/// <summary>Строка diff с `original`: подпись поля и значения до/после.</summary>
public sealed class CharacterDiffRow
{
    public CharacterDiffRow(CharacterDiff diff)
    {
        LabelKey = diff.LabelKey;
        BeforeText = diff.Before.Length > 0 ? diff.Before : "—";
        AfterText = diff.After.Length > 0 ? diff.After : "—";
    }

    /// <summary>Ключ локализации подписи поля (`charDiff*`).</summary>
    public string LabelKey { get; }

    public string BeforeText { get; }

    public string AfterText { get; }
}

/// <summary>Кнопка связанного диалога в карточке «Диалоги персонажа».</summary>
public sealed class CharacterRelatedViewModel
{
    public CharacterRelatedViewModel(DialogueGraph dialogue) => Dialogue = dialogue;

    public DialogueGraph Dialogue { get; }

    /// <summary>`d.name || d.id` — подпись кнопки.</summary>
    public string Title => !string.IsNullOrWhiteSpace(Dialogue.Name) ? Dialogue.Name : Dialogue.Id;
}

/// <summary>Чип community в карточке `game_relations`: иконка, имя и число персонажей.</summary>
public sealed class CommunityChipViewModel
{
    public CommunityChipViewModel(string community, string icon, int count)
    {
        Text = icon + " " + community + (count > 0 ? " " + count : "");
        Tooltip = count > 0 ? community + " · " + count + " " + LocalizationSource.Get("charPersons") : community;
    }

    public string Text { get; }

    public string Tooltip { get; }
}

/// <summary>Метка шкалы rating/reputation: `value name` и подсказка с названием шкалы.</summary>
public sealed class ScaleMarkViewModel
{
    public ScaleMarkViewModel(ScaleMark mark, string titleKey)
    {
        Text = mark.Value + " " + mark.Name;
        Tooltip = LocalizationSource.Get(titleKey) + " " + mark.Name;
    }

    public string Text { get; }

    public string Tooltip { get; }
}

/// <summary>Заголовок столбца матрицы отношений: первые 8 символов (`String(c).slice(0, 8)`).</summary>
public sealed class RelationColumnViewModel
{
    public RelationColumnViewModel(string community)
    {
        Community = community;
        Short = community.Length > 8 ? community[..8] : community;
    }

    public string Community { get; }

    public string Short { get; }
}

/// <summary>Строка матрицы отношений: первые 12 символов и редактируемые ячейки.</summary>
public sealed class RelationRowViewModel
{
    public RelationRowViewModel(string community, int index, IReadOnlyList<RelationCellViewModel> cells)
    {
        Community = community;
        Index = index;
        Short = community.Length > 12 ? community[..12] : community;
        Cells = cells;
    }

    public string Community { get; }

    /// <summary>Индекс строки в `gr.communities` (в JS `setRelation(ri, ci, …)`).</summary>
    public int Index { get; }

    public string Short { get; }

    public IReadOnlyList<RelationCellViewModel> Cells { get; }
}

/// <summary>Ячейка матрицы отношений; правка сразу пишется в `project.gameRelations`.</summary>
public sealed partial class RelationCellViewModel : ObservableObject
{
    private readonly CharactersViewModel _owner;
    private readonly bool _loading;

    [ObservableProperty] private string _value;

    public RelationCellViewModel(CharactersViewModel owner, int rowIndex, int columnIndex, string value)
    {
        _owner = owner;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        _loading = true;
        _value = value;
        _loading = false;
    }

    public int RowIndex { get; }

    public int ColumnIndex { get; }

    partial void OnValueChanged(string value)
    {
        if (_loading) return;
        _owner.SetRelation(RowIndex, ColumnIndex, value);
    }
}
