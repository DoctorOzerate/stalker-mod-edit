using XRayForge.Core.Storage;

namespace XRayForge.Core.Ui;

/// <summary>Строка настройки горячей клавиши: действие и разобранная комбинация.</summary>
public sealed record HotkeyBinding(HotkeyAction Action, string Combo, HotkeyGesture? Gesture)
{
    /// <summary>Комбинацию удалось разобрать (её можно повесить на `KeyBinding`).</summary>
    public bool IsValid => Gesture != null;

    /// <summary>Подпись для интерфейса (`Ctrl+S`, `Esc`); битая комбинация — как в настройках.</summary>
    public string Display => Gesture?.Display ?? Combo;
}

/// <summary>
/// Реестр горячих клавиш настроек (этап 5): id → разобранная комбинация для `KeyBinding`
/// и подписи в диалоге настроек. Проверяются конфликты и битые комбинации.
/// </summary>
public static class HotkeyCatalog
{
    /// <summary>Разобранные комбинации только для корректных значений (`mod+s` → Ctrl+S).</summary>
    public static IReadOnlyDictionary<string, HotkeyGesture> Resolve(IReadOnlyDictionary<string, string>? hotkeys)
    {
        var map = new Dictionary<string, HotkeyGesture>(StringComparer.Ordinal);
        foreach (var action in ProgramSettingsCatalog.HotkeyActions)
        {
            // Пустая комбинация → значение по умолчанию (в JS `hk[id] || defaultHotkeys()[id]`).
            var gesture = HotkeyGesture.Parse(ComboFor(hotkeys, action));
            if (gesture != null) map[action.Id] = gesture;
        }
        return map;
    }

    /// <summary>Комбинация действия: из настроек, иначе значение по умолчанию.</summary>
    public static string ComboFor(IReadOnlyDictionary<string, string>? hotkeys, HotkeyAction action)
    {
        if (hotkeys != null && hotkeys.TryGetValue(action.Id, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        return action.Default;
    }

    /// <summary>Все действия реестра с текущими комбинациями (для диалога настроек).</summary>
    public static IReadOnlyList<HotkeyBinding> Bindings(IReadOnlyDictionary<string, string>? hotkeys)
    {
        var list = new List<HotkeyBinding>(ProgramSettingsCatalog.HotkeyActions.Length);
        foreach (var action in ProgramSettingsCatalog.HotkeyActions)
        {
            var combo = ComboFor(hotkeys, action);
            list.Add(new HotkeyBinding(action, combo, HotkeyGesture.Parse(combo)));
        }
        return list;
    }

    /// <summary>
    /// Действия с неуникальными комбинациями (id → id, с которым конфликтует; первое в реестре остаётся).
    /// Пустые/битые комбинации не конфликтуют между собой.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Duplicates(IReadOnlyDictionary<string, string>? hotkeys)
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var binding in Bindings(hotkeys))
        {
            if (binding.Gesture == null) continue;
            var key = binding.Gesture.Display;
            if (seen.TryGetValue(key, out var owner)) result[binding.Action.Id] = owner;
            else seen[key] = binding.Action.Id;
        }
        return result;
    }

    /// <summary>Нажатие соответствует действию с учётом настроек (`comboMatches` этапа 4).</summary>
    public static string? MatchAction(IReadOnlyDictionary<string, string>? hotkeys, bool ctrl, bool shift, bool alt, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var action in ProgramSettingsCatalog.HotkeyActions)
        {
            var combo = ComboFor(hotkeys, action);
            if (ProgramSettingsCatalog.ComboMatches(combo, ctrl, shift, alt, key)) return action.Id;
        }
        return null;
    }

    /// <summary>Номер вкладки для `Ctrl+1…9` (`null`, если комбинация другая или вне диапазона).</summary>
    public static NavigationItem? TabForNumber(bool ctrl, bool shift, bool alt, string? key)
    {
        if (!ctrl || shift || alt) return null;
        if (key == null || key.Length != 1 || key[0] < '1' || key[0] > '9') return null;
        return NavigationCatalog.ByHotkeyIndex(key[0] - '0');
    }
}
