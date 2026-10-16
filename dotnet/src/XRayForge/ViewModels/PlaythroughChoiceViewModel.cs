namespace XRayForge.ViewModels;

/// <summary>
/// Вариант перехода в панели прогона диалога (`choices.map` из `DialoguesTab.js`): id узла-цели
/// и готовая подпись «→ Тип: текст» (`GraphPlaythrough.ChoiceLabel`). Список пересобирается на
/// каждом шаге прогона, поэтому VM неизменяемый.
/// </summary>
public sealed class PlaythroughChoiceViewModel
{
    public PlaythroughChoiceViewModel(string id, string label)
    {
        Id = id;
        Label = label;
    }

    /// <summary>Id узла, на который перейдёт прогон (`stepPlaythrough(c.id)`).</summary>
    public string Id { get; }

    /// <summary>Подпись кнопки перехода.</summary>
    public string Label { get; }
}
