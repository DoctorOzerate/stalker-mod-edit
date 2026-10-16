using System.Windows;
using System.Windows.Input;

namespace XRayForge.Views;

/// <summary>
/// Диалог ввода одной строки (`prompt` из JS): имя нового проекта, URL синхронизации.
/// Окно без системного хрома — заголовок перетаскивается, Esc отменяет.
/// </summary>
public partial class InputWindow : Window
{
    public InputWindow()
        : this("", "", "")
    {
    }

    public InputWindow(string title, string label, string initial)
    {
        InitializeComponent();

        Caption = title;
        Label = label;
        Value = initial ?? "";
        DataContext = this;

        Loaded += OnLoaded;
    }

    /// <summary>Заголовок окна (передаётся из `IDialogService.Prompt`).</summary>
    public string Caption { get; }

    /// <summary>Подпись поля ввода.</summary>
    public string Label { get; }

    /// <summary>Значение поля (двусторонняя привязка, обновляется по мере ввода).</summary>
    public string Value { get; set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Input.SelectAll();
        Input.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }
}
