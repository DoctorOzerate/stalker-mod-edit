using XRayForge.Core.Models;

namespace XRayForge.Core.Validation;

/// <summary>
/// Код проблемы персонажа (`validateCharacter` в `App.js`): строки собирает
/// <see cref="CharacterValidator.Validate"/> для паритета с JS, а вкладка «Персонажи»
/// подставляет свои ключи локализации.
/// </summary>
public enum CharacterIssue
{
    /// <summary>«не указан visual (модель)».</summary>
    MissingVisual,

    /// <summary>«не указана icon (портрет)».</summary>
    MissingIcon,

    /// <summary>community нет в `project.generalCommunities`.</summary>
    UnknownCommunity,

    /// <summary>icon нет в `project.textureMap`.</summary>
    UnknownTexture
}

/// <summary>Проверка персонажа (visual, icon, community, портрет).</summary>
public static class CharacterValidator
{
    /// <summary>`validateCharacter`: те же строки и порядок, что в JS.</summary>
    public static List<string> Validate(
        Character? character,
        IReadOnlyList<string>? knownCommunities,
        IReadOnlyDictionary<string, object?>? textureMap)
    {
        var issues = new List<string>();
        foreach (var issue in Issues(character, knownCommunities, textureMap))
            issues.Add(Describe(issue, character));
        return issues;
    }

    /// <summary>Коды проблем для локализованной подписи (`validateCharacter` без строк).</summary>
    public static List<CharacterIssue> Issues(
        Character? character,
        IReadOnlyList<string>? knownCommunities,
        IReadOnlyDictionary<string, object?>? textureMap)
    {
        var issues = new List<CharacterIssue>();

        var visual = character?.Visual ?? string.Empty;
        var icon = character?.Icon ?? string.Empty;
        var community = character?.Community ?? string.Empty;

        if (string.IsNullOrWhiteSpace(visual))
            issues.Add(CharacterIssue.MissingVisual);
        if (string.IsNullOrWhiteSpace(icon))
            issues.Add(CharacterIssue.MissingIcon);
        if (community.Length > 0
            && knownCommunities is { Count: > 0 }
            && !knownCommunities.Contains(community))
            issues.Add(CharacterIssue.UnknownCommunity);
        if (icon.Length > 0
            && textureMap is { Count: > 0 }
            && !textureMap.ContainsKey(icon))
            issues.Add(CharacterIssue.UnknownTexture);

        return issues;
    }

    /// <summary>Строка проблемы (паритет с `validateCharacter` в JS, включая значение поля).</summary>
    private static string Describe(CharacterIssue issue, Character? character) => issue switch
    {
        CharacterIssue.MissingVisual => "не указан visual (модель)",
        CharacterIssue.MissingIcon => "не указана icon (портрет)",
        CharacterIssue.UnknownCommunity => "группировка не найдена: " + (character?.Community ?? string.Empty),
        CharacterIssue.UnknownTexture => "портрет не найден в дескрипторах текстур: " + (character?.Icon ?? string.Empty),
        _ => issue.ToString()
    };
}
