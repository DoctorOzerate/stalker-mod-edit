using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.ViewModels;

/// <summary>
/// Кнопка палитры узлов (`filteredTypes.map` в `DialoguesTab.js`): цветной квадрат с иконкой
/// типа, подпись и краткое описание. Клик добавляет узел этого типа (`addNode`).
/// </summary>
public sealed class PaletteItemViewModel
{
    public PaletteItemViewModel(PaletteItem item)
    {
        Type = item.Type;
        Info = item.Info;
    }

    /// <summary>Тип узла (`START`, `PHRASE`, `RANDOM`, `END`) — параметр команды добавления.</summary>
    public string Type { get; }

    /// <summary>Отображение типа (`NODE_TYPES[k]`): иконка, подпись, цвет и описание.</summary>
    public NodeTypeInfo Info { get; }

    /// <summary>Подпись типа в кнопке.</summary>
    public string Label => Info.Label;

    /// <summary>Иконка типа.</summary>
    public string Icon => Info.Icon;

    /// <summary>Описание типа под подписью.</summary>
    public string Desc => Info.Desc;

    /// <summary>Цвет квадрата с иконкой (`style: { background: v.color }`).</summary>
    public string ColorHex => Info.Color;
}
