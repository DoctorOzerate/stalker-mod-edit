namespace XRayForge.Core.Text;

public static class XmlEscaping
{
    public static string EscapeText(string? v) =>
        (v ?? string.Empty).Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public static string EscapeAttribute(string? v) =>
        EscapeText(v).Replace("\"", "&quot;").Replace("'", "&apos;");
}
