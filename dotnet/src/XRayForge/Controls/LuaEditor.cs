using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using XRayForge.Core.Lua;

namespace XRayForge.Controls;

/// <summary>
/// Редактор Lua с подсветкой прямо во время ввода. Заменяет пару «TextBox + режим
/// просмотра»: токены перекрашиваются после каждой правки, каретка/выделение/прокрутка
/// сохраняются, а Ctrl+Z/Ctrl+Y работают по собственной истории текста (документ
/// пересобирается, поэтому встроенный undo RichTextBox непригоден).
/// </summary>
public sealed class LuaEditor : RichTextBox
{
    public static readonly DependencyProperty EditorTextProperty = DependencyProperty.Register(
        nameof(EditorText), typeof(string), typeof(LuaEditor),
        new FrameworkPropertyMetadata(string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditorTextChanged));

    private static readonly Brush CommentBrush = Frozen(0x4A, 0xDE, 0x80);
    private static readonly Brush StringBrush = Frozen(0xFB, 0xBF, 0x24);
    private static readonly Brush NumberBrush = Frozen(0x60, 0xA5, 0xFA);
    private static readonly Brush KeywordBrush = Frozen(0xC0, 0x84, 0xFC);

    private readonly List<LineInfo> _lines = new();
    private readonly Stack<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();
    private bool _suppress;
    private bool _highlightQueued;
    private bool _restoringHistory;
    private string _appliedText = string.Empty;
    private DateTime _lastTypingPush = DateTime.MinValue;
    private int _lastKnownCaret;
    private int _lastKnownSelectionEnd;
    private double _lineHeight = 16;

    public LuaEditor()
    {
        AcceptsReturn = true;
        AcceptsTab = true;
        IsUndoEnabled = false;
        IsDocumentEnabled = false;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalContentAlignment = VerticalAlignment.Top;
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        SpellCheck.SetIsEnabled(this, false);
        Document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            ColumnWidth = double.PositiveInfinity,
            TextAlignment = TextAlignment.Left
        };

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, OnUndo, CanUndoCommand));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, OnRedo, CanRedoCommand));
        SizeChanged += (_, _) => UpdatePageWidth(_appliedText);
        SelectionChanged += (_, _) => RememberSelection();
    }

    /// <summary>Текст документа; двусторонняя привязка к `ScriptFile.Text`.</summary>
    public string EditorText
    {
        get => (string)GetValue(EditorTextProperty);
        set => SetValue(EditorTextProperty, value ?? string.Empty);
    }

    /// <summary>Смещение каретки/начала выделения (аналог `TextBox.CaretIndex`).</summary>
    public int CaretOffset => Math.Clamp(OffsetOf(Selection.Start), 0, _appliedText.Length);

    /// <summary>Длина выделения в символах.</summary>
    public int SelectionLength
    {
        get
        {
            var start = Math.Clamp(OffsetOf(Selection.Start), 0, _appliedText.Length);
            var end = Math.Clamp(OffsetOf(Selection.End), 0, _appliedText.Length);
            return Math.Max(0, end - start);
        }
    }

    public bool CanUndoText => _undo.Count > 0;
    public bool CanRedoText => _redo.Count > 0;

    /// <summary>Поставить каретку и выделение по символьным смещениям.</summary>
    public void SelectRange(int start, int length)
    {
        var textLength = _appliedText.Length;
        var from = Math.Clamp(start, 0, textLength);
        var to = Math.Clamp(from + Math.Max(0, length), from, textLength);
        var begin = PositionAt(from);
        var end = PositionAt(to);
        Selection.Select(begin, end);
        _lastKnownCaret = from;
        _lastKnownSelectionEnd = to;
    }

    /// <summary>Прокрутить к строке (0-based) с запасом по центру.</summary>
    public void ScrollToEditorLine(int line)
    {
        var max = Math.Max(0, _lines.Count - 1);
        var target = Math.Max(0, Math.Min(max, line));
        var viewport = Math.Max(1, ViewportHeight);
        ScrollToVerticalOffset(Math.Max(0, target * _lineHeight - viewport / 3));
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        if (_suppress || _restoringHistory) return;

        var text = ReadDocumentText();
        if (text == _appliedText) return;

        var previous = _appliedText;
        _appliedText = text;
        PushUndo(previous, coalesce: true);

        // Двусторонняя привязка обновляет проект сразу, как TextBox с PropertyChanged.
        _suppress = true;
        try
        {
            SetCurrentValue(EditorTextProperty, text);
            GetBindingExpression(EditorTextProperty)?.UpdateSource();
        }
        finally
        {
            _suppress = false;
        }

        QueueHighlight();
    }

    private static void OnEditorTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LuaEditor editor) return;
        var text = e.NewValue as string ?? string.Empty;
        if (editor._suppress) return;
        if (text == editor._appliedText) return;

        editor.PushUndo(editor._appliedText, coalesce: false);
        editor._lastTypingPush = DateTime.MinValue;
        editor._appliedText = text;
        editor.RebuildDocument(text, editor._lastKnownCaret, editor._lastKnownSelectionEnd);
    }

    private void QueueHighlight()
    {
        if (_highlightQueued) return;
        _highlightQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _highlightQueued = false;
            RebuildDocument(_appliedText, _lastKnownCaret, _lastKnownSelectionEnd);
        }));
    }

    private void RebuildDocument(string text, int selectionStart, int selectionEnd)
    {
        var caret = Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Clamp(selectionEnd, caret, text.Length);

        _suppress = true;
        try
        {
            _lines.Clear();
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0),
                LineHeight = _lineHeight,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight
            };

            var tokenLines = LuaTokenizer.TokenizeLines(text);
            var sourceLines = text.Split('\n');
            for (var i = 0; i < sourceLines.Length; i++)
            {
                var info = new LineInfo();
                var tokens = i < tokenLines.Count ? tokenLines[i] : new List<LuaToken>();
                if (tokens.Count == 0)
                {
                    var empty = new Run(string.Empty);
                    paragraph.Inlines.Add(empty);
                    info.Runs.Add(empty);
                }
                else
                {
                    foreach (var token in tokens)
                    {
                        if (token.Text.Length == 0) continue;
                        var run = new Run(token.Text);
                        StyleRun(run, token.Cls);
                        paragraph.Inlines.Add(run);
                        info.Runs.Add(run);
                    }
                }

                _lines.Add(info);
                if (i < sourceLines.Length - 1) paragraph.Inlines.Add(new LineBreak());
            }

            Document.Blocks.Clear();
            Document.Blocks.Add(paragraph);
            UpdatePageWidth(text);
        }
        finally
        {
            _suppress = false;
        }

        var startPointer = PositionAt(caret);
        var endPointer = PositionAt(end);
        Selection.Select(startPointer, endPointer);
        _lastKnownCaret = caret;
        _lastKnownSelectionEnd = end;
    }

    private void StyleRun(Run run, string cls)
    {
        switch (cls)
        {
            case LuaTokenClasses.Comment:
                run.Foreground = CommentBrush;
                run.FontStyle = FontStyles.Italic;
                break;
            case LuaTokenClasses.StringLiteral:
                run.Foreground = StringBrush;
                break;
            case LuaTokenClasses.Number:
                run.Foreground = NumberBrush;
                break;
            case LuaTokenClasses.Keyword:
                run.Foreground = KeywordBrush;
                run.FontWeight = FontWeights.SemiBold;
                break;
            case LuaTokenClasses.Bracket:
                run.Foreground = ResourceBrush("TextAccent", KeywordBrush);
                break;
            default:
                run.Foreground = ResourceBrush("TextPrimary", Brushes.Gainsboro);
                break;
        }
    }

    private void UpdatePageWidth(string text)
    {
        if (Document == null) return;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var longest = 0.0;
        foreach (var line in (text ?? string.Empty).Split('\n'))
        {
            var formatted = new FormattedText(line.Length == 0 ? " " : line,
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize,
                Brushes.Transparent, pixelsPerDip);
            longest = Math.Max(longest, formatted.WidthIncludingTrailingWhitespace);
        }

        Document.PageWidth = Math.Max(Math.Max(ActualWidth - 30, 240), longest + 80);
        Document.PagePadding = new Thickness(0);
        Document.ColumnWidth = double.PositiveInfinity;
    }

    private double MeasureLineHeight()
    {
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var formatted = new FormattedText("Ag", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return Math.Max(formatted.Height, FontSize * 1.15);
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == FontFamilyProperty || e.Property == FontSizeProperty ||
            e.Property == FontWeightProperty || e.Property == FontStyleProperty)
        {
            _lineHeight = MeasureLineHeight();
            if (_appliedText.Length > 0) QueueHighlight();
        }
    }

    private void RememberSelection()
    {
        if (_suppress) return;
        _lastKnownCaret = Math.Clamp(OffsetOf(Selection.Start), 0, _appliedText.Length);
        _lastKnownSelectionEnd = Math.Clamp(OffsetOf(Selection.End), _lastKnownCaret, _appliedText.Length);
    }

    private string ReadDocumentText()
    {
        var text = new TextRange(Document.ContentStart, Document.ContentEnd).Text;
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private int OffsetOf(TextPointer pointer)
    {
        if (pointer == null) return 0;
        var text = new TextRange(Document.ContentStart, pointer).Text;
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Length;
    }

    private TextPointer PositionAt(int offset)
    {
        if (_lines.Count == 0) return Document.ContentStart;
        var remaining = Math.Max(0, offset);
        for (var i = 0; i < _lines.Count; i++)
        {
            var info = _lines[i];
            var length = info.Length;
            if (remaining <= length || i == _lines.Count - 1)
            {
                var local = Math.Min(remaining, length);
                foreach (var run in info.Runs)
                {
                    if (local <= run.Text.Length)
                        return run.ContentStart.GetPositionAtOffset(local, LogicalDirection.Forward)
                               ?? run.ContentEnd;
                    local -= run.Text.Length;
                }
                return info.Runs.Count > 0 ? info.Runs[^1].ContentEnd : Document.ContentStart;
            }

            remaining -= length + 1; // LineBreak
        }

        return Document.ContentEnd;
    }

    private void PushUndo(string text, bool coalesce)
    {
        var now = DateTime.UtcNow;
        if (coalesce && (now - _lastTypingPush).TotalMilliseconds < 650 && _undo.Count > 0)
        {
            _lastTypingPush = now;
            return;
        }

        _undo.Push(new Snapshot(text, _lastKnownCaret, _lastKnownSelectionEnd));
        while (_undo.Count > 200) TrimBottom(_undo);
        _redo.Clear();
        _lastTypingPush = now;
    }

    private static void TrimBottom(Stack<Snapshot> stack)
    {
        var items = stack.ToArray();
        stack.Clear();
        for (var i = items.Length - 2; i >= 0; i--) stack.Push(items[i]);
    }

    private void OnUndo(object sender, ExecutedRoutedEventArgs e) => Restore(_undo, _redo);

    private void OnRedo(object sender, ExecutedRoutedEventArgs e) => Restore(_redo, _undo);

    private void CanUndoCommand(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _undo.Count > 0;

    private void CanRedoCommand(object sender, CanExecuteRoutedEventArgs e) => e.CanExecute = _redo.Count > 0;

    private void Restore(Stack<Snapshot> from, Stack<Snapshot> to)
    {
        if (from.Count == 0) return;
        var snapshot = from.Pop();
        to.Push(new Snapshot(_appliedText, _lastKnownCaret, _lastKnownSelectionEnd));

        _restoringHistory = true;
        try
        {
            _lastKnownCaret = snapshot.Caret;
            _lastKnownSelectionEnd = snapshot.SelectionEnd;
            _appliedText = snapshot.Text;
            SetCurrentValue(EditorTextProperty, snapshot.Text);
            GetBindingExpression(EditorTextProperty)?.UpdateSource();
            RebuildDocument(snapshot.Text, snapshot.Caret, snapshot.SelectionEnd);
            ScrollToEditorLine(LineOf(snapshot.Caret));
        }
        finally
        {
            _restoringHistory = false;
        }
    }

    private int LineOf(int offset)
    {
        var line = 0;
        for (var i = 0; i < Math.Min(offset, _appliedText.Length); i++)
            if (_appliedText[i] == '\n') line++;
        return line;
    }

    private Brush ResourceBrush(string key, Brush fallback) =>
        TryFindResource(key) as Brush ?? fallback;

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private sealed class LineInfo
    {
        public List<Run> Runs { get; } = new();
        public int Length => Runs.Sum(run => run.Text.Length);
    }

    private readonly record struct Snapshot(string Text, int Caret, int SelectionEnd);
}
