using XRayForge.Core.History;

namespace XRayForge.Tests;

public class HistoryTests
{
    [Fact]
    public void Push_truncates_future_and_caps_limit()
    {
        var h = new History<string>(3);
        var s = HistoryState<string>.Empty;
        s = h.Push(s, "a");
        s = h.Push(s, "b");
        s = h.Push(s, "c");
        s = h.Push(s, "d"); // превышает лимит 3
        Assert.Equal(new[] { "b", "c", "d" }, s.Hist);
        Assert.Equal(2, s.Pos);
    }

    [Fact]
    public void Move_changes_cursor_and_returns_unchanged_on_bounds()
    {
        var h = new History<string>(10);
        var s = HistoryState<string>.Empty;
        s = h.Push(s, "a");
        s = h.Push(s, "b");
        var applied = new List<string>();
        s = h.Move(s, -1, applied.Add);
        Assert.Equal(0, s.Pos);
        Assert.Equal(new[] { "a" }, applied);

        var before = s;
        s = h.Move(s, -1, _ => throw new InvalidOperationException("не должен вызываться"));
        Assert.Equal(before, s); // за границей — то же состояние, apply не вызван

        s = h.Move(s, 1, applied.Add);
        Assert.Equal(1, s.Pos);
        Assert.Equal(new[] { "a", "b" }, applied);
    }

    [Fact]
    public void Push_after_undo_discards_redo_branch()
    {
        var h = new History<string>(10);
        var s = HistoryState<string>.Empty;
        s = h.Push(s, "a");
        s = h.Push(s, "b");
        s = h.Move(s, -1, null); // pos = 0
        s = h.Push(s, "c");      // отбрасывает "b"
        Assert.Equal(new[] { "a", "c" }, s.Hist);
        Assert.Equal(1, s.Pos);
    }
}
