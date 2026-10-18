using System.Text;
using System.Text.RegularExpressions;
using XRayForge.Core.Models;

namespace XRayForge.Core.Dialogs;

/// <summary>
/// Полная карта файлов (путь → текст) для нескольких групп диалогов (#2):
/// dialogs_&lt;fileName&gt;.xml, dialogs.ltx и string_table по локалям.
/// </summary>
public static class DialogueFiles
{
    public static Dictionary<string, string> GameFilesFromDialogues(
        IEnumerable<DialogueFileGroup?>? groups,
        string? cfg,
        string? encoding,
        IReadOnlyList<Language>? languages,
        string? projectName)
    {
        var enc = encoding == "utf-8" ? "UTF-8" : "windows-1251";
        var cfgDir = cfg == "configs" ? "configs" : "config";
        var decl = "<?xml version=\"1.0\" encoding=\"" + enc + "\"?>";

        var data = new Dictionary<string, string>();
        var allIds = new List<string>();
        var allPhrases = new List<DialogueNode>();

        foreach (var group in groups ?? Enumerable.Empty<DialogueFileGroup?>())
        {
            if (group == null) continue;

            var dialogs = group.Dialogues.Where(d => d != null).ToList();
            foreach (var d in dialogs)
            {
                allIds.Add(!string.IsNullOrEmpty(d.Id) ? d.Id : d.Name);
                foreach (var n in d.Nodes.Where(n => n.Type == NodeTypes.Phrase)) allPhrases.Add(n);
            }

            var sb = new StringBuilder(decl).Append("\n<game_dialogs>");
            foreach (var d in dialogs) sb.Append(DialogueExport.DialogueToXml(d));
            sb.Append("</game_dialogs>\n");

            data["gamedata/" + cfgDir + "/gameplay/dialogs_" + SafeName(group.FileName) + ".xml"] = sb.ToString();
        }

        if (allIds.Count > 0)
            data["gamedata/" + cfgDir + "/gameplay/dialogs.ltx"] = DialogueExport.DialogsLtx(allIds);

        var safe = SafeName(projectName);
        var langs = languages != null && languages.Count > 0
            ? languages
            : new List<Language> { new() { Locale = "rus", Name = "Русский" } };

        foreach (var lang in langs)
        {
            var byId = new Dictionary<string, string>();
            foreach (var e in lang.Entries) byId[e.Id] = e.Text;

            foreach (var n in allPhrases)
            {
                var d = n.Data ?? new NodeData();
                if (d.TextId.Length > 0 && !byId.ContainsKey(d.TextId) && d.Text.Length > 0)
                    byId[d.TextId] = d.Text;
            }

            var table = new StringBuilder(decl).Append("\n<string_table>");
            foreach (var kv in byId)
                table.Append("<string id=\"").Append(DialogueExport.Esc(kv.Key))
                    .Append("\"><text>").Append(DialogueExport.Esc(kv.Value)).Append("</text></string>");
            table.Append("</string_table>\n");

            var locale = lang.Locale.Length > 0 ? lang.Locale : "rus";
            data["gamedata/" + cfgDir + "/text/" + locale + "/st_" + safe + ".xml"] = table.ToString();
        }

        return data;
    }

    /// <summary>Безопасное имя файла/группы: только латиница, цифры, `_` и `-`.</summary>
    public static string SafeName(string? value)
    {
        var s = Regex.Replace(string.IsNullOrEmpty(value) ? "dialogue" : value, "[^a-zA-Z0-9_-]", "_");
        return s.Length > 0 ? s : "dialogue";
    }
}