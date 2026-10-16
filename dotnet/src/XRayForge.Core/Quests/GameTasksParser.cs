using System.Text.RegularExpressions;
using System.Xml.Linq;
using XRayForge.Core.Models;
using XRayForge.Core.Xml;

namespace XRayForge.Core.Quests;

/// <summary>
/// Разбор game_tasks / game_tasks_data / tasks_*.xml (фрагментов без корня).
/// Поддерживает короткие теги &lt;infoportion set_complete&gt; и prio вместо prior.
/// </summary>
public static class GameTasksParser
{
    private static readonly Regex ShortBoolAttr = new(@"\b(set_complete|set_fail)\b(?=\s*[>\/])");
    private static readonly Regex IncludeLine = new(@"^\s*#include\b[^\n]*$", RegexOptions.Multiline);

    public static List<Quest> Parse(string? text)
    {
        var src = ShortBoolAttr.Replace(text ?? string.Empty, "$1=\"true\"");
        src = IncludeLine.Replace(src, "");

        var doc = XmlHelpers.TryParse(src);
        var rootIsFragment = doc?.Root != null
                             && doc.Root.Name.LocalName is "game_task" or "task";

        if (doc == null || rootIsFragment)
        {
            // Фрагмент без корневого тега (tasks_*.xml) — обернуть
            doc = XmlHelpers.TryParse("<game_tasks>" + src + "</game_tasks>");
            if (doc == null) return new List<Quest>();
        }

        var root = doc.Root;
        if (root == null) return new List<Quest>();

        var rn = root.Name.LocalName.ToLowerInvariant();
        var format = rn switch
        {
            "task_manager" => "task_manager",
            "game_tasks" or "game_tasks_data" => "game_tasks",
            _ => null
        };
        if (format == null) return new List<Quest>();

        var tag = format == "task_manager" ? "task" : "game_task";
        var result = new List<Quest>();

        foreach (var gt in root.Descendants().Where(e => e.Name.LocalName == tag))
        {
            var id = (string?)gt.Attribute("id") ?? "";
            if (id.Length == 0) continue;

            result.Add(new Quest
            {
                Id = id,
                Prior = (string?)gt.Attribute("prior") ?? (string?)gt.Attribute("prio") ?? "",
                Title = FirstText(gt, "title", "name"),
                Text = FirstText(gt, "text", "brief"),
                Objectives = ParseObjectives(gt),
                InfoportionComplete = ParseInfoportion(gt, "set_complete"),
                InfoportionFail = ParseInfoportion(gt, "set_fail"),
                Icon = XmlHelpers.ChildText(gt, "icon"),
                Location = XmlHelpers.ChildText(gt, "location"),
                Article = XmlHelpers.ChildText(gt, "article"),
                Format = format
            });
        }

        return result;
    }

    private static string FirstText(XElement el, string tag1, string tag2)
    {
        var t = XmlHelpers.ChildText(el, tag1);
        return t.Length > 0 ? t : XmlHelpers.ChildText(el, tag2);
    }

    private static List<QuestObjective> ParseObjectives(XElement gt)
    {
        var objectives = new List<QuestObjective>();
        foreach (var obj in XmlHelpers.Children(gt, "objective"))
        {
            var hasElementChildren = obj.Elements().Any();
            var text = XmlHelpers.ChildText(obj, "text");
            if (text.Length == 0 && !hasElementChildren) text = obj.Value.Trim();

            var objective = new QuestObjective
            {
                Text = text,
                Icon = XmlHelpers.ChildText(obj, "icon"),
                Infoportion = XmlHelpers.ChildText(obj, "infoportion"),
                Article = XmlHelpers.ChildText(obj, "article")
            };

            if (objective.Text.Length > 0 || objective.Icon.Length > 0
                || objective.Infoportion.Length > 0 || objective.Article.Length > 0)
                objectives.Add(objective);
        }

        return objectives;
    }

    /// <summary>Значение инфопорции: атрибут-идентификатор либо текст тега (или «true» → текст).</summary>
    private static string ParseInfoportion(XElement gt, string attribute)
    {
        var value = "";
        foreach (var ip in XmlHelpers.Children(gt, "infoportion"))
        {
            var attr = (string?)ip.Attribute(attribute);
            if (attr == null) continue;

            var txt = ip.Value.Trim();
            value = attr.Length > 0 && attr != "true" ? attr : txt;
        }
        return value;
    }
}