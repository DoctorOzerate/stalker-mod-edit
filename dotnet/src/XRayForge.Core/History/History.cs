namespace XRayForge.Core.History;

/// <summary>Состояние истории отмены/повтора: снапшоты и позиция курсора.</summary>
public readonly record struct HistoryState<T>(T[] Hist, int Pos)
{
    public static HistoryState<T> Empty => new(Array.Empty<T>(), -1);
}

/// <summary>
/// Общая история отмены/повтора (undo/redo) для любого домена.
/// Хранит снапшоты с курсором и обрезает их лимитом. Чистые функции: состояние → новое состояние.
/// </summary>
public sealed class History<T>
{
    private readonly int _limit;

    public History(int limit = 50) => _limit = Math.Max(1, limit);

    /// <summary>Добавить снапшот; всё после текущей позиции отбрасывается (новый виток истории).</summary>
    public HistoryState<T> Push(HistoryState<T> state, T snapshot)
    {
        var kept = state.Hist.Take(Math.Max(0, state.Pos + 1)).ToList();
        kept.Add(snapshot);
        if (kept.Count > _limit) kept.RemoveRange(0, kept.Count - _limit);
        return new HistoryState<T>(kept.ToArray(), kept.Count - 1);
    }

    /// <summary>
    /// Сдвинуть курсор на delta, вызвать apply(snapshot) для побочного эффекта и вернуть новое состояние.
    /// Если сдвиг выходит за границы — вернуть исходное состояние без apply.
    /// </summary>
    public HistoryState<T> Move(HistoryState<T> state, int delta, Action<T>? apply)
    {
        var pos = state.Pos + delta;
        if (pos < 0 || pos >= state.Hist.Length) return state;
        apply?.Invoke(state.Hist[pos]);
        return new HistoryState<T>(state.Hist, pos);
    }
}
