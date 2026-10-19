using System.Windows;
using System.Windows.Input;
using XRayForge.Core.Validation;

namespace XRayForge.Views;

/// <summary>Окно встроенной самопроверки проекта (этап 8).</summary>
public partial class SelfCheckWindow : Window
{
    public SelfCheckWindow(SelfCheckReport report)
    {
        InitializeComponent();
        DataContext = report;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }
}
