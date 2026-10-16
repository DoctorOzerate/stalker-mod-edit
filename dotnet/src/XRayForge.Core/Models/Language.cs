namespace XRayForge.Core.Models;

public sealed class LanguageEntry
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>Локализация: код локали, отображаемое имя, путь-источник и записи string_table.</summary>
public sealed class Language
{
    public string Locale { get; set; } = "";
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public List<LanguageEntry> Entries { get; set; } = new();
}