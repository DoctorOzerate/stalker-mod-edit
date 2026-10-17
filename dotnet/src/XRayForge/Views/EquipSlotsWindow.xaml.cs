using System.Windows;
using System.Windows.Input;
using XRayForge.ViewModels;

namespace XRayForge.Views;

/// <summary>
/// Окно «Настройка слотов»: раскладка в общем прямоугольнике, список слотов и поля
/// подписи/позиции/размера. «ОК» подтверждает временные правки, «Отмена» их отбрасывает
/// (снимок восстанавливает <see cref="ItemsViewModel.OpenEquipSlotSettingsCommand"/>).
/// </summary>
public partial class EquipSlotsWindow : Window
{
    public EquipSlotsWindow() => InitializeComponent();

    public EquipSlotsWindow(ItemsViewModel model)
        : this()
    {
        DataContext = model;
    }

    private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        try { DragMove(); }
        catch (InvalidOperationException) { /* кнопка отпущена до перетаскивания */ }
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
