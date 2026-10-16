namespace XRayForge.Core.Ui;

/// <summary>
/// Горячая клавиша в формате JS-реестра (`mod+s`, `shift+z`, `F5`, `Delete`;
/// `mod` = Ctrl/Cmd). Используется и для `KeyBinding` в WPF, и для подписи в настройках.
/// </summary>
public sealed record HotkeyGesture(bool Ctrl, bool Shift, bool Alt, string Key)
{
    /// <summary>Аккорд `Ctrl` (`Ctrl+1…9` — переключение вкладок).</summary>
    public static readonly HotkeyGesture CtrlNumberHint = new(true, false, false, "1");

    /// <summary>Разобрать комбинацию; `null` — пустая/некорректная строка.</summary>
    public static HotkeyGesture? Parse(string? combo)
    {
        if (string.IsNullOrWhiteSpace(combo)) return null;
        var parts = combo.ToLowerInvariant().Split('+');
        var key = NormalizeKey(parts[^1]);
        if (key.Length == 0) return null;

        var ctrl = false;
        var shift = false;
        var alt = false;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].Trim())
            {
                case "mod":
                case "ctrl":
                case "cmd":
                    ctrl = true;
                    break;
                case "shift":
                    shift = true;
                    break;
                case "alt":
                    alt = true;
                    break;
                default:
                    return null;
            }
        }

        return new HotkeyGesture(ctrl, shift, alt, key);
    }

    /// <summary>Нормализация имени клавиши (`esc` → `escape`, `return` → `enter`, регистр — нижний).</summary>
    public static string NormalizeKey(string? key)
    {
        var value = (key ?? "").Trim().ToLowerInvariant();
        return value switch
        {
            "esc" => "escape",
            "return" => "enter",
            "del" => "delete",
            "ins" => "insert",
            "pgup" => "pageup",
            "pgdn" => "pagedown",
            "space" or "spacebar" => "space",
            _ => value
        };
    }

    /// <summary>Подпись для интерфейса: `Ctrl+S`, `Esc`, `F5`.</summary>
    public string Display
    {
        get
        {
            var parts = new List<string>(4);
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            parts.Add(KeyDisplay(Key));
            return string.Join("+", parts);
        }
    }

    /// <summary>Как `comboMatches` из App.js: сравнение с фактическим нажатием.</summary>
    public bool Matches(bool ctrl, bool shift, bool alt, string? key) =>
        Ctrl == ctrl && Shift == shift && Alt == alt && Key == NormalizeKey(key);

    /// <summary>Подпись одиночной клавиши (`escape` → `Esc`, `s` → `S`).</summary>
    public static string KeyDisplay(string key) => key switch
    {
        "escape" => "Esc",
        "delete" => "Del",
        "enter" => "Enter",
        "space" => "Space",
        "pageup" => "PgUp",
        "pagedown" => "PgDn",
        "insert" => "Ins",
        "backspace" => "Backspace",
        "tab" => "Tab",
        "left" => "←",
        "right" => "→",
        "up" => "↑",
        "down" => "↓",
        _ => key.Length == 1 ? key.ToUpperInvariant() : key.Length > 1 ? char.ToUpperInvariant(key[0]) + key[1..] : key
    };
}
