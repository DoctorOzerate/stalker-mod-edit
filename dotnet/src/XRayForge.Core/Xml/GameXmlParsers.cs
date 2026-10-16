using XRayForge.Core.Models;
using XRayForge.Core.Text;

namespace XRayForge.Core.Xml;

/// <summary>Разбор character_desc_general.xml и info_portions.xml (#9, #60, #61).</summary>
public static partial class GameXmlParsers
{
    /// <summary>Полный разбор character_desc_general.xml: сообщества с отношениями (#9).</summary>
    public static CharacterDescGeneral? ParseCharacterDescGeneral(string? text)
    {
        var doc = XmlHelpers.TryParse(text);
        var root = doc?.Root;
        if (root == null || root.Name.LocalName != "character_desc_general") return null;

        var result = new CharacterDescGeneral();
        foreach (var c in root.Descendants().Where(e => e.Name.LocalName == "community"))
        {
            var id = (string?)c.Attribute("id") ?? "";
            if (id.Length == 0) continue;

            var name = XmlHelpers.ChildText(c, "name");
            if (name.Length == 0) name = (string?)c.Attribute("name") ?? "";
            var icon = XmlHelpers.ChildText(c, "icon");
            if (icon.Length == 0) icon = (string?)c.Attribute("icon") ?? "";

            result.Communities.Add(new Community
            {
                Id = id,
                Name = name,
                Icon = icon,
                Relations = XmlHelpers.Children(c, "relation")
                    .Select(r => new CommunityRelation
                    {
                        Community = (string?)r.Attribute("community") ?? "",
                        Value = r.Value.Trim()
                    })
                    .ToList()
            });
        }

        return result;
    }

    /// <summary>Сериализация character_desc_general обратно в XML (#9).</summary>
    public static string CharacterDescGeneralToXml(CharacterDescGeneral? data)
    {
        var lines = new List<string>
        {
            "<?xml version=\"1.0\" encoding=\"windows-1251\"?>",
            "<character_desc_general>",
            "\t<communities>"
        };

        foreach (var c in data?.Communities ?? new List<Community>())
        {
            lines.Add("\t\t<community id=\"" + XmlEscaping.EscapeAttribute(c.Id) + "\">");
            if (c.Name.Length > 0) lines.Add("\t\t\t<name>" + XmlEscaping.EscapeText(c.Name) + "</name>");
            if (c.Icon.Length > 0) lines.Add("\t\t\t<icon>" + XmlEscaping.EscapeText(c.Icon) + "</icon>");
            foreach (var r in c.Relations)
                lines.Add("\t\t\t<relation community=\"" + XmlEscaping.EscapeAttribute(r.Community) + "\">"
                          + XmlEscaping.EscapeText(r.Value) + "</relation>");
            lines.Add("\t\t</community>");
        }

        lines.Add("\t</communities>");
        lines.Add("</character_desc_general>");
        return string.Join('\n', lines);
    }

    /// <summary>info_portions.xml (#60, #61).</summary>
    public static List<InfoPortion> ParseInfoPortions(string? text)
    {
        var doc = XmlHelpers.TryParse(text);
        var root = doc?.Root;
        if (root == null || root.Name.LocalName != "game_information_portions") return new List<InfoPortion>();

        var result = new List<InfoPortion>();
        foreach (var ip in root.Descendants().Where(e => e.Name.LocalName == "info_portion"))
        {
            var id = (string?)ip.Attribute("id") ?? "";
            if (id.Length == 0) continue;

            result.Add(new InfoPortion
            {
                Id = id,
                Tasks = XmlHelpers.KidTexts(ip, "task"),
                Dialogs = XmlHelpers.KidTexts(ip, "dialog"),
                Disables = XmlHelpers.KidTexts(ip, "disable"),
                Articles = XmlHelpers.KidTexts(ip, "article")
            });
        }

        return result;
    }
}