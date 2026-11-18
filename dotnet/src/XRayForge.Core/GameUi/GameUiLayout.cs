namespace XRayForge.Core.GameUi;

/// <summary>Элемент с вычисленной геометрией: координаты в родителе и абсолютные на холсте окна.</summary>
public sealed record GameUiPlacement(
    string Path,
    GameUiElement Element,
    GameUiRect Local,
    GameUiRect Absolute,
    int Depth)
{
    /// <summary>У элемента есть размер (иначе он не рисуется в предпросмотре).</summary>
    public bool HasSize => Absolute.HasSize;

    /// <summary>Класс элемента.</summary>
    public GameUiElementKind Kind => Element.Kind;

    /// <summary>Подпись для дерева и подсказки.</summary>
    public string DisplayName => Element.DisplayName;
}

/// <summary>
/// Геометрия игрового UI: координаты элемента задаются относительно родителя, поэтому
/// для предпросмотра считаются абсолютные прямоугольники. Слой без WPF — используется
/// и предпросмотром, и хит-тестом при клике по холсту.
/// </summary>
public static class GameUiLayout
{
    /// <summary>Все элементы с абсолютной геометрией (обход в порядке файла).</summary>
    public static IReadOnlyList<GameUiPlacement> Flatten(GameUiDocument document)
    {
        var result = new List<GameUiPlacement>();
        foreach (var root in document.Roots) Add(result, root, null);
        return result;
    }

    private static void Add(List<GameUiPlacement> result, GameUiElement element, GameUiPlacement? parent)
    {
        var local = element.Rect;
        var originX = parent?.Absolute.X ?? 0;
        var originY = parent?.Absolute.Y ?? 0;
        var placement = new GameUiPlacement(
            element.Path,
            element,
            local,
            local.Offset(originX, originY),
            element.Depth);
        result.Add(placement);
        foreach (var child in element.Children) Add(result, child, placement);
    }

    /// <summary>
    /// Элемент под точкой холста: берётся последний подходящий (верхний в порядке отрисовки).
    /// Элементы без размера пропускаются, если `requireSize`.
    /// </summary>
    public static GameUiPlacement? HitTest(
        IReadOnlyList<GameUiPlacement> placements, double x, double y, bool requireSize = true)
    {
        GameUiPlacement? found = null;
        foreach (var placement in placements)
        {
            if (requireSize && !placement.Absolute.HasSize) continue;
            if (placement.Absolute.Contains(x, y)) found = placement;
        }
        return found;
    }

    /// <summary>Найти размещение по адресу элемента (`0.2.1`).</summary>
    public static GameUiPlacement? Find(IReadOnlyList<GameUiPlacement> placements, string? path) =>
        string.IsNullOrEmpty(path) ? null : placements.FirstOrDefault(p => p.Path == path);

    /// <summary>Прямые дети размещения (для дерева с фильтром по видимости).</summary>
    public static IReadOnlyList<GameUiPlacement> ChildrenOf(
        IReadOnlyList<GameUiPlacement> placements, GameUiPlacement parent)
    {
        var prefix = parent.Path + ".";
        return placements
            .Where(p => p.Depth == parent.Depth + 1 && p.Path.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>Вписать прямоугольник в холст: размер не больше холста, координаты не отрицательны.</summary>
    public static GameUiRect Clamp(GameUiRect rect, double canvasWidth, double canvasHeight)
    {
        var width = Math.Max(0, Math.Min(rect.Width, canvasWidth));
        var height = Math.Max(0, Math.Min(rect.Height, canvasHeight));
        var x = Math.Clamp(rect.X, 0, Math.Max(0, canvasWidth - width));
        var y = Math.Clamp(rect.Y, 0, Math.Max(0, canvasHeight - height));
        return new GameUiRect(x, y, width, height);
    }
}
