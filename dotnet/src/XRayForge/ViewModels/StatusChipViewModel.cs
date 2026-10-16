using XRayForge.Core.Ui;

namespace XRayForge.ViewModels;

/// <summary>Плитка статусной строки/обзора: глиф вкладки, ключ локализации подписи и значение.</summary>
public sealed class StatusChipViewModel
{
    public StatusChipViewModel(SummaryChip chip)
        : this(chip.Glyph, chip.LabelKey, chip.Count)
    {
    }

    public StatusChipViewModel(string glyph, string labelKey, int count)
    {
        Glyph = glyph;
        LabelKey = labelKey;
        Count = count;
    }

    public string Glyph { get; }
    public string LabelKey { get; }
    public int Count { get; }
    public string CountText => Count.ToString();
}

/// <summary>Строка раздела (персонаж, LTX-секция, скрипт, язык, квест, диалог).</summary>
public sealed record SectionRow(string Glyph, string Label, string Detail);
