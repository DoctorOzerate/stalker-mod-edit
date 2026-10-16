using XRayForge.Core.Models;

namespace XRayForge.Core.Validation;

/// <summary>Проверка персонажа (visual, icon, community, портрет).</summary>
public static class CharacterValidator
{
    public static List<string> Validate(
        Character? character,
        IReadOnlyList<string>? knownCommunities,
        IReadOnlyDictionary<string, object?>? textureMap)
    {
        var issues = new List<string>();

        var visual = character?.Visual ?? string.Empty;
        var icon = character?.Icon ?? string.Empty;
        var community = character?.Community ?? string.Empty;

        if (string.IsNullOrWhiteSpace(visual))
            issues.Add("не указан visual (модель)");
        if (string.IsNullOrWhiteSpace(icon))
            issues.Add("не указана icon (портрет)");
        if (community.Length > 0
            && knownCommunities is { Count: > 0 }
            && !knownCommunities.Contains(community))
            issues.Add("группировка не найдена: " + community);
        if (icon.Length > 0
            && textureMap is { Count: > 0 }
            && !textureMap.ContainsKey(icon))
            issues.Add("портрет не найден в дескрипторах текстур: " + icon);

        return issues;
    }
}
