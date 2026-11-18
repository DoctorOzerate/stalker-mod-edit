using System.Windows.Input;

namespace XRayForge.ViewModels;

/// <summary>
/// Кнопка верхней панели активного раздела: подпись уже переведена оболочкой
/// (`RefreshLocalization` пересобирает коллекцию), команда — из <see cref="ShellViewModel"/>.
/// </summary>
public sealed record ToolbarActionViewModel(string Id, string Glyph, string Label, ICommand? Command);
