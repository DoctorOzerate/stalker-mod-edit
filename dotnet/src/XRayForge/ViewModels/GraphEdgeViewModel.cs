using CommunityToolkit.Mvvm.ComponentModel;
using XRayForge.Core.Graph;
using XRayForge.Core.Models;

namespace XRayForge.ViewModels;

/// <summary>
/// Связь графа на холсте: кубическая кривая между портами карточек (`edges.map` в
/// `DialoguesTab.js`). Путь считается через <see cref="GraphEditing.EdgePath"/>; связь
/// подсвечивается, когда касается выделенного узла, остальные приглушаются до 0.2.
/// </summary>
public sealed partial class GraphEdgeViewModel : ObservableObject
{
    public GraphEdgeViewModel(DialogueEdge edge) => Edge = edge;

    /// <summary>Модель связи проекта (`{ id, source, target }`).</summary>
    public DialogueEdge Edge { get; }

    /// <summary>Идентификатор связи (`e.id`).</summary>
    public string Id => Edge.Id;

    /// <summary>Узел-источник; `null`, если id не найден (как в JS — связь не рисуется).</summary>
    public GraphNodeViewModel? Source { get; private set; }

    /// <summary>Узел-цель; `null`, если id не найден.</summary>
    public GraphNodeViewModel? Target { get; private set; }

    /// <summary>Разметка пути (`M x y C …`) для `Path.Data`.</summary>
    [ObservableProperty]
    private string _path = "";

    /// <summary>Связь касается выделенного узла — подсветка зелёным (в JS `#22c55e`).</summary>
    [ObservableProperty]
    private bool _isHighlighted;

    /// <summary>Непрозрачность: при выделении несвязанные связи гаснут до 0.2.</summary>
    [ObservableProperty]
    private double _opacity = 1;

    /// <summary>Есть что рисовать: оба конца связи разрешены в узлы графа.</summary>
    public bool HasPath => Path.Length > 0;

    /// <summary>Пересобрать путь по текущим позициям узлов (после перетаскивания или раскладки).</summary>
    public void RefreshPath(IReadOnlyDictionary<string, GraphNodeViewModel> index)
    {
        Source = index.TryGetValue(Edge.Source, out var source) ? source : null;
        Target = index.TryGetValue(Edge.Target, out var target) ? target : null;
        Path = Source == null || Target == null ? "" : GraphEditing.EdgePath(Source.Node, Target.Node);
        OnPropertyChanged(nameof(HasPath));
    }
}
