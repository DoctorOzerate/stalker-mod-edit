using System.Text.RegularExpressions;

namespace XRayForge.Core.Text;

public static class Paths
{
    /// <summary>
    /// Нормализация относительного пути: заменяет обратные слеши на прямые,
    /// схлопывает дубли и разрешает «.» / «..». Абсолютные пути и протоколы → пустая строка.
    /// </summary>
    public static string Normalize(string? value)
    {
        var p = Regex.Replace((value ?? string.Empty).Replace('\\', '/'), "/+", "/");
        if (Regex.IsMatch(p, @"^(?:[a-z]:|//)", RegexOptions.IgnoreCase)) return string.Empty;

        var parts = new List<string>();
        foreach (var x in p.Split('/'))
        {
            if (x.Length == 0 || x == ".") continue;
            if (x == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                else return string.Empty;
            }
            else
            {
                parts.Add(x);
            }
        }

        return string.Join('/', parts);
    }
}
