using System.Windows.Controls;

namespace XRayForge.Views;

/// <summary>
/// Раздел без редактора: шапка вкладки и строки данных проекта.
/// Данные приходят из `ShellViewModel` (`ActiveTab`, `Rows`, `HasRows`) — DataContext наследуется
/// от хоста содержимого вкладки (`MainWindow`).
/// </summary>
public partial class SectionStubView : UserControl
{
    public SectionStubView() => InitializeComponent();
}
