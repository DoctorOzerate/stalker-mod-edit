using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using XRayForge.Core.Suggestions;

namespace XRayForge.Controls;

/// <summary>
/// TextBox с выпадающим списком подсказок. Используется в редакторах предметов, персонажей
/// и (на следующем шаге) Lua-скриптов. Источник — <see cref="SuggestionLibrary"/> или любая
/// коллекция <see cref="SuggestionEntry"/>; поддерживаются поля со списками через запятую.
/// </summary>
public sealed class SuggestionTextBox : TextBox
{
    public static readonly DependencyProperty SuggestionsProperty = DependencyProperty.Register(
        nameof(Suggestions), typeof(IEnumerable), typeof(SuggestionTextBox),
        new FrameworkPropertyMetadata(null, OnSuggestionsChanged));

    public static readonly DependencyProperty MinimumPrefixLengthProperty = DependencyProperty.Register(
        nameof(MinimumPrefixLength), typeof(int), typeof(SuggestionTextBox),
        new FrameworkPropertyMetadata(1));

    public static readonly DependencyProperty MaxSuggestionsProperty = DependencyProperty.Register(
        nameof(MaxSuggestions), typeof(int), typeof(SuggestionTextBox),
        new FrameworkPropertyMetadata(12));

    public static readonly DependencyProperty TokenSeparatorsProperty = DependencyProperty.Register(
        nameof(TokenSeparators), typeof(string), typeof(SuggestionTextBox),
        new FrameworkPropertyMetadata(","));

    private readonly Popup _popup;
    private readonly ListBox _list;
    private bool _accepting;

    public SuggestionTextBox()
    {
        _list = new ListBox
        {
            MaxHeight = 240,
            MinWidth = 280,
            Focusable = false,
            IsTabStop = false
        };
        _list.SetResourceReference(Control.FontFamilyProperty, "MonoFontFamily");
        _list.SetResourceReference(Control.FontSizeProperty, "DensitySmallFontSize");
        _list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        _list.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        if (Application.Current?.TryFindResource("SuggestionItemTemplate") is DataTemplate template)
            _list.ItemTemplate = template;
        else
            _list.DisplayMemberPath = nameof(SuggestionEntry.Value);
        _list.PreviewMouseLeftButtonUp += (_, args) =>
        {
            if (AcceptSuggestion()) args.Handled = true;
        };

        var chrome = new Border
        {
            Child = _list,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(1)
        };
        chrome.SetResourceReference(Border.BackgroundProperty, "SurfacePanel");
        chrome.SetResourceReference(Border.BorderBrushProperty, "BorderStrong");

        _popup = new Popup
        {
            Child = chrome,
            AllowsTransparency = true,
            Placement = PlacementMode.Bottom,
            PlacementTarget = this,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade,
            VerticalOffset = 2,
            MinWidth = 280
        };
        _popup.SetBinding(FrameworkElement.WidthProperty,
            new Binding(nameof(ActualWidth)) { Source = this });

        Unloaded += (_, _) => ClosePopup();
    }

    /// <summary>Коллекция <see cref="SuggestionEntry"/> для текущего поля.</summary>
    public IEnumerable? Suggestions
    {
        get => (IEnumerable?)GetValue(SuggestionsProperty);
        set => SetValue(SuggestionsProperty, value);
    }

    /// <summary>Сколько символов нужно ввести до открытия списка (по умолчанию 1).</summary>
    public int MinimumPrefixLength
    {
        get => (int)GetValue(MinimumPrefixLengthProperty);
        set => SetValue(MinimumPrefixLengthProperty, value);
    }

    public int MaxSuggestions
    {
        get => (int)GetValue(MaxSuggestionsProperty);
        set => SetValue(MaxSuggestionsProperty, value);
    }

    /// <summary>Разделители токенов: подсказка заменяет только последний токен (`a, b`).</summary>
    public string TokenSeparators
    {
        get => (string)GetValue(TokenSeparatorsProperty);
        set => SetValue(TokenSeparatorsProperty, value);
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        if (!_accepting) UpdateSuggestions();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        UpdateSuggestions();
    }

    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        Dispatcher.BeginInvoke(new Action(UpdateSuggestions), DispatcherPriority.Background);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_popup.IsOpen)
        {
            switch (e.Key)
            {
                case Key.Down:
                    MoveSelection(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    MoveSelection(-1);
                    e.Handled = true;
                    return;
                case Key.Enter:
                    if (AcceptSuggestion()) e.Handled = true;
                    else ClosePopup();
                    return;
                case Key.Tab:
                    if (AcceptSuggestion())
                    {
                        e.Handled = true;
                        return;
                    }
                    ClosePopup();
                    break;
                case Key.Escape:
                    ClosePopup();
                    e.Handled = true;
                    return;
            }
        }

        base.OnPreviewKeyDown(e);
    }

    private static void OnSuggestionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (SuggestionTextBox)d;
        if (box.IsKeyboardFocusWithin) box.UpdateSuggestions();
        else box.ClosePopup();
    }

    private void UpdateSuggestions()
    {
        if (!IsKeyboardFocusWithin || _accepting) return;

        var text = Text ?? "";
        var caret = Math.Clamp(CaretIndex, 0, text.Length);
        var start = FindTokenStart(text, caret);
        var prefix = start <= caret ? text[start..caret].Trim() : "";
        if (prefix.Length < Math.Max(0, MinimumPrefixLength) || Suggestions == null)
        {
            ClosePopup();
            return;
        }

        var source = Suggestions.OfType<SuggestionEntry>();
        var matches = SuggestionMatcher
            .Filter(source, prefix, Math.Max(1, MaxSuggestions))
            .Where(entry => !string.Equals(entry.Value, prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            ClosePopup();
            return;
        }

        _list.ItemsSource = matches;
        _list.SelectedIndex = 0;
        _list.ScrollIntoView(matches[0]);
        _popup.IsOpen = true;
    }

    private void MoveSelection(int delta)
    {
        var count = _list.Items.Count;
        if (count == 0) return;

        var index = _list.SelectedIndex;
        index = index < 0 ? (delta > 0 ? 0 : count - 1) : (index + delta + count) % count;
        _list.SelectedIndex = index;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private bool AcceptSuggestion()
    {
        if (!_popup.IsOpen || _list.SelectedItem is not SuggestionEntry entry) return false;

        var text = Text ?? "";
        var caret = Math.Clamp(CaretIndex, 0, text.Length);
        var start = FindTokenStart(text, caret);
        var end = FindTokenEnd(text, caret);

        _accepting = true;
        Text = text[..start] + entry.Value + text[end..];
        CaretIndex = start + entry.Value.Length;
        SelectionLength = 0;
        _accepting = false;

        GetBindingExpression(TextProperty)?.UpdateSource();
        ClosePopup();
        return true;
    }

    private int FindTokenStart(string text, int caret)
    {
        var separators = TokenSeparators ?? ",";
        var start = 0;
        for (var i = caret - 1; i >= 0; i--)
        {
            if (separators.IndexOf(text[i]) < 0) continue;
            start = i + 1;
            break;
        }

        while (start < caret && char.IsWhiteSpace(text[start])) start++;
        return Math.Min(start, caret);
    }

    private int FindTokenEnd(string text, int caret)
    {
        var separators = TokenSeparators ?? ",";
        for (var i = caret; i < text.Length; i++)
            if (separators.IndexOf(text[i]) >= 0) return i;
        return text.Length;
    }

    private void ClosePopup()
    {
        _popup.IsOpen = false;
        _list.ItemsSource = null;
        _list.SelectedIndex = -1;
    }
}
