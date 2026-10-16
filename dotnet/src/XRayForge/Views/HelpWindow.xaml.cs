using System.Windows;
using System.Windows.Input;

namespace XRayForge.Views;

/// <summary>
/// Окно справки (`help`): статические разделы из словаря локализации Core
/// (проекты, вкладки, горячие клавиши, gamedata, тема).
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow() => InitializeComponent();

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }
}
