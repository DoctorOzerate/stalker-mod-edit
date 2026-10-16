namespace XRayForge.Core.Models;

/// <summary>Группа диалогов, экспортируемая в один файл dialogs_&lt;fileName&gt;.xml (#2).</summary>
public sealed class DialogueFileGroup
{
    public string FileName { get; set; } = "";
    public List<DialogueGraph> Dialogues { get; set; } = new();
}