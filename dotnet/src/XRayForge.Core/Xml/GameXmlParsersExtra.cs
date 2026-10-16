using XRayForge.Core.Models;

namespace XRayForge.Core.Xml;

/// <summary>Разбор encyclopedia_*.xml, npc_profile.xml и дескрипторов текстур (#17, #18, #63).</summary>
public static partial class GameXmlParsers
{
    /// <summary>encyclopedia_*.xml (#63).</summary>
    public static List<EncyclopediaArticle> ParseEncyclopedia(string? text)
    {
        var doc = XmlHelpers.TryParse(text);
        var root = doc?.Root;
        if (root == null || root.Name.LocalName != "encyclopedia") return new List<EncyclopediaArticle>();

        var result = new List<EncyclopediaArticle>();
        foreach (var a in root.Descendants().Where(e => e.Name.LocalName == "article"))
        {
            var id = (string?)a.Attribute("id") ?? "";
            if (id.Length == 0) continue;

            result.Add(new EncyclopediaArticle
            {
                Id = id,
                Name = (string?)a.Attribute("name") ?? "",
                Group = (string?)a.Attribute("group") ?? "",
                Ltx = XmlHelpers.ChildText(a, "ltx"),
                Text = XmlHelpers.ChildText(a, "text")
            });
        }

        return result;
    }

    /// <summary>npc_profile.xml (#17).</summary>
    public static List<NpcProfileCharacter> ParseNpcProfile(string? text)
    {
        var doc = XmlHelpers.TryParse(text);
        var root = doc?.Root;
        if (root == null) return new List<NpcProfileCharacter>();

        var result = new List<NpcProfileCharacter>();
        foreach (var c in root.Descendants().Where(e => e.Name.LocalName == "character"))
        {
            var id = (string?)c.Attribute("id") ?? "";
            if (id.Length == 0) continue;

            result.Add(new NpcProfileCharacter
            {
                Id = id,
                Class = XmlHelpers.ChildText(c, "class"),
                SpecificCharacter = XmlHelpers.ChildText(c, "specific_character")
            });
        }

        return result;
    }

    /// <summary>Дескриптор текстур ui_*.xml (#18).</summary>
    public static TextureDescriptor? ParseTextureDescriptor(string? text)
    {
        var doc = XmlHelpers.TryParse(text);
        var root = doc?.Root;
        if (root == null) return null;

        var fileEl = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "file");
        if (fileEl == null) return null;

        var textures = new List<TextureEntry>();
        foreach (var t in root.Descendants().Where(e => e.Name.LocalName == "texture"))
        {
            var id = (string?)t.Attribute("id") ?? "";
            if (id.Length == 0) continue;

            textures.Add(new TextureEntry
            {
                Id = id,
                X = XmlHelpers.Num((string?)t.Attribute("x")),
                Y = XmlHelpers.Num((string?)t.Attribute("y")),
                Width = XmlHelpers.Num((string?)t.Attribute("width")),
                Height = XmlHelpers.Num((string?)t.Attribute("height"))
            });
        }

        return new TextureDescriptor { File = (string?)fileEl.Attribute("name") ?? "", Textures = textures };
    }
}