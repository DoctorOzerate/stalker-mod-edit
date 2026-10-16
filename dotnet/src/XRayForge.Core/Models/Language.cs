using System.Text.Json;
using System.Text.Json.Serialization;

namespace XRayForge.Core.Models;

public sealed class LanguageEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
}

/// <summary>Локализация: код локали, отображаемое имя, путь-источник и записи string_table.</summary>
public sealed class Language
{
    [JsonPropertyName("locale")] public string Locale { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("entries")] public List<LanguageEntry> Entries { get; set; } = new();

    /// <summary>Поля вне модели (сохраняются как есть).</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}