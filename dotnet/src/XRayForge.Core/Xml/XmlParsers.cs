using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using XRayForge.Core.Models;

namespace XRayForge.Core.Xml;

/// <summary>Парсинг игровых XML: персонажи (specific_character) и диалоги (game_dialogs).</summary>
public static class XmlParsers
{
    public static List<Character> ParseCharactersXml(string? text)
    {
        var cleaned = Regex.Replace(text ?? string.Empty, @"^\s*#include\b[^\n]*$", "", RegexOptions.Multiline);

        XDocument doc;
        try { doc = XDocument.Parse(cleaned); }
        catch (XmlException) { return new List<Character>(); }

        var result = new List<Character>();
        foreach (var x in doc.Descendants("specific_character"))
        {
            var id = (string?)x.Attribute("id") ?? string.Empty;
            if (id.Length == 0) continue;

            var name = Pick(x, "name", "name");
            if (name.Length == 0) name = id;

            var community = Pick(x, "community", "community");
            if (community.Length == 0) community = (string?)x.Attribute("team") ?? "";

            var mi = Children(x, "map_icon").FirstOrDefault();
            var suppliesEl = Children(x, "supplies").FirstOrDefault();

            result.Add(new Character
            {
                Id = id,
                Name = name,
                Community = community,
                Rank = Pick(x, "rank", "rank"),
                Reputation = Pick(x, "reputation", "reputation"),
                Class = Pick(x, "class", "class"),
                TerrainSect = Pick(x, "terrain_sect", "terrain_sect"),
                SndConfig = Pick(x, "snd_config", "snd_config"),
                MapIcon = mi == null
                    ? null
                    : new MapIcon { X = (string?)mi.Attribute("x") ?? "", Y = (string?)mi.Attribute("y") ?? "" },
                Bio = Pick(x, "bio", "bio"),
                Icon = Pick(x, "icon", "icon"),
                Visual = Pick(x, "visual", "visual"),
                Supplies = suppliesEl == null ? "" : suppliesEl.Value.Trim(),
                StartDialog = Children(x, "start_dialog").Select(e => e.Value.Trim()).ToList(),
                ActorDialog = Children(x, "actor_dialog").Select(e => e.Value.Trim()).ToList(),
                Source = "gamedata"
            });
        }

        return result;
    }

    public static List<DialogueGraph> ParseDialogueXml(string? text)
    {
        XDocument doc;
        try { doc = XDocument.Parse(text ?? string.Empty); }
        catch (XmlException) { throw new InvalidOperationException("Некорректный XML диалогов"); }

        var root = doc.Root;
        List<XElement> dialogs;
        if (root == null) dialogs = new List<XElement>();
        else if (root.Name.LocalName == "game_dialogs") dialogs = Children(root, "dialog").ToList();
        else if (root.Name.LocalName == "dialog") dialogs = new List<XElement> { root };
        else dialogs = new List<XElement>();

        var result = new List<DialogueGraph>();
        for (var di = 0; di < dialogs.Count; di++)
        {
            var dialog = dialogs[di];
            var name = (string?)dialog.Attribute("id") ?? $"dialogue_{di}";
            var list = Children(dialog, "phrase_list").FirstOrDefault();
            var warnings = new List<string>();

            if (list == null)
            {
                warnings.Add($"{name}: отсутствует точный тег phrase_list");
                result.Add(new DialogueGraph { Id = name, Name = name, Warnings = warnings, Imported = true });
                continue;
            }

            var phrases = Children(list, "phrase").ToList();
            var nodes = new List<DialogueNode>
            {
                new() { Id = $"{name}__start", Type = NodeTypes.Start, Data = new NodeData() }
            };
            var edges = new List<DialogueEdge>();
            var occurrences = new Dictionary<string, int>();
            var raws = new List<(string From, string To)>();

            for (var index = 0; index < phrases.Count; index++)
            {
                var p = phrases[index];
                var phraseId = (string?)p.Attribute("id") ?? index.ToString();
                occurrences.TryGetValue(phraseId, out var count);
                occurrences[phraseId] = count + 1;
                if (count > 0)
                    warnings.Add($"{name}: дублирующийся phrase id \"{phraseId}\" (связи ведут к первому)");
                var id = $"{name}__phrase_{phraseId}__{count}";

                var meta = new Dictionary<string, List<string>>();
                foreach (var tag in NodeTypes.MetaKeys)
                    meta[tag] = Children(p, tag).Select(e => e.Value).ToList();

                var te = Children(p, "text").FirstOrDefault();
                var textValue = te?.Value ?? string.Empty;
                var actor = ((string?)p.Attribute("actor") ?? "").ToLowerInvariant();
                var role = Regex.IsMatch(actor, "actor|player") ? "player" : "unknown";
                var snd = Children(p, "sound").FirstOrDefault() ?? Children(p, "voice").FirstOrDefault();
                var voice = snd?.Value.Trim() ?? "";

                nodes.Add(new DialogueNode
                {
                    Id = id,
                    Type = NodeTypes.Phrase,
                    Data = new NodeData
                    {
                        PhraseId = phraseId,
                        Role = role,
                        RoleSource = actor.Length > 0 ? "xml_actor" : "unknown",
                        TextId = textValue,
                        UseId = textValue.Length > 0,
                        VoiceId = voice,
                        Metadata = meta
                    }
                });

                foreach (var n in Children(p, "next"))
                    raws.Add((id, n.Value.Trim()));
            }

            string? Resolve(string pid)
            {
                var n = nodes.Find(x => x.Data != null && x.Data.PhraseId == pid);
                return n?.Id;
            }

            var first = nodes.Find(n => n.Type == NodeTypes.Phrase);
            if (first != null)
                edges.Add(new DialogueEdge
                {
                    Id = $"e_{name}_start",
                    Source = $"{name}__start",
                    Target = Resolve("0") ?? first.Id
                });

            for (var i = 0; i < raws.Count; i++)
            {
                var (from, to) = raws[i];
                var target = Resolve(to);
                edges.Add(new DialogueEdge
                {
                    Id = $"e_{name}_{i}",
                    Source = from,
                    Target = target ?? $"{name}__missing_{to}",
                    Broken = target == null,
                    OriginalNext = to
                });
            }

            foreach (var n in nodes
                         .Where(n => n.Type == NodeTypes.Phrase && !edges.Any(e => e.Source == n.Id && !e.Broken))
                         .ToList())
            {
                var end = $"{name}__end_{n.Id}";
                nodes.Add(new DialogueNode { Id = end, Type = NodeTypes.End, Data = new NodeData() });
                edges.Add(new DialogueEdge { Id = $"e_{end}", Source = n.Id, Target = end });
            }

            result.Add(new DialogueGraph
            {
                Id = name,
                Name = name,
                Nodes = nodes,
                Edges = edges,
                Warnings = warnings,
                Imported = true
            });
        }

        return result;
    }

    private static IEnumerable<XElement> Children(XElement el, string name) =>
        el.Elements().Where(e => e.Name.LocalName == name);

    private static string Pick(XElement el, string tag, string attr)
    {
        var a = (string?)el.Attribute(attr);
        if (!string.IsNullOrEmpty(a)) return a;
        var child = Children(el, tag).FirstOrDefault();
        if (child == null) return string.Empty;
        var textChild = Children(child, "text").FirstOrDefault();
        return (textChild != null ? textChild.Value : child.Value).Trim();
    }
}
