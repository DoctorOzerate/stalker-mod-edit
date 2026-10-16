using System.Text;
using XRayForge.Core.Models;
using XRayForge.Core.Text;

namespace XRayForge.Core.Quests;

/// <summary>Экспорт квестов в game_tasks XML и task_manager LTX (#37, #64).</summary>
public static class QuestExport
{
    private const string Nl = "\n";

    public static string QuestsToXml(IEnumerable<Quest?>? quests, string? encoding)
    {
        var enc = encoding == "utf-8" ? "utf-8" : "windows-1251";
        var list = quests?.Where(q => q != null).Select(q => q!).ToList() ?? new List<Quest>();
        var games = list.Where(q => q.Format != "task_manager").ToList();
        var managers = list.Where(q => q.Format == "task_manager").ToList();
        var sb = new StringBuilder();

        sb.Append("<?xml version=\"1.0\" encoding=\"").Append(enc).Append("\"?>");

        if (games.Count > 0)
        {
            sb.Append(Nl).Append("<game_tasks>");
            foreach (var q in games)
            {
                sb.Append(Nl).Append("\t<game_task id=\"").Append(Escape(q.Id)).Append('"')
                  .Append(q.Prior.Length > 0 ? " prior=\"" + Escape(q.Prior) + "\"" : "").Append('>');
                if (q.Title.Length > 0) sb.Append(Nl).Append("\t\t<title>").Append(Escape(q.Title)).Append("</title>");
                if (q.Text.Length > 0) sb.Append(Nl).Append("\t\t<text>").Append(Escape(q.Text)).Append("</text>");
                if (q.Icon.Length > 0) sb.Append(Nl).Append("\t\t<icon>").Append(Escape(q.Icon)).Append("</icon>");

                foreach (var o in q.Objectives)
                {
                    var txt = o.Text;
                    var ic = o.Icon ?? "";
                    var ip = o.Infoportion ?? "";
                    var art = o.Article ?? "";
                    if (ic.Length > 0 || ip.Length > 0 || art.Length > 0)
                    {
                        sb.Append(Nl).Append("\t\t<objective>");
                        if (txt.Length > 0) sb.Append(Nl).Append("\t\t\t<text>").Append(Escape(txt)).Append("</text>");
                        if (ic.Length > 0) sb.Append(Nl).Append("\t\t\t<icon>").Append(Escape(ic)).Append("</icon>");
                        if (ip.Length > 0) sb.Append(Nl).Append("\t\t\t<infoportion>").Append(Escape(ip)).Append("</infoportion>");
                        if (art.Length > 0) sb.Append(Nl).Append("\t\t\t<article>").Append(Escape(art)).Append("</article>");
                        sb.Append(Nl).Append("\t\t</objective>");
                    }
                    else if (txt.Length > 0)
                    {
                        sb.Append(Nl).Append("\t\t<objective><text>").Append(Escape(txt)).Append("</text></objective>");
                    }
                }

                if (q.InfoportionComplete.Length > 0)
                    sb.Append(Nl).Append("\t\t<infoportion set_complete>").Append(Escape(q.InfoportionComplete)).Append("</infoportion>");
                if (q.InfoportionFail.Length > 0)
                    sb.Append(Nl).Append("\t\t<infoportion set_fail>").Append(Escape(q.InfoportionFail)).Append("</infoportion>");
                if (q.Location.Length > 0)
                    sb.Append(Nl).Append("\t\t<location>").Append(Escape(q.Location)).Append("</location>");
                if (q.Article.Length > 0)
                    sb.Append(Nl).Append("\t\t<article>").Append(Escape(q.Article)).Append("</article>");
                if (q.Reward.Length > 0)
                    sb.Append(Nl).Append("\t\t<reward>").Append(Escape(q.Reward)).Append("</reward>");

                sb.Append(Nl).Append("\t</game_task>");
            }
            sb.Append(Nl).Append("</game_tasks>");
        }

        if (managers.Count > 0)
        {
            sb.Append(Nl).Append("<task_manager>");
            foreach (var q in managers)
            {
                sb.Append(Nl).Append("\t<task id=\"").Append(Escape(q.Id)).Append('"')
                  .Append(q.Prior.Length > 0 ? " prior=\"" + Escape(q.Prior) + "\"" : "").Append('>');
                if (q.Title.Length > 0) sb.Append(Nl).Append("\t\t<title>").Append(Escape(q.Title)).Append("</title>");
                if (q.Text.Length > 0) sb.Append(Nl).Append("\t\t<brief>").Append(Escape(q.Text)).Append("</brief>");
                if (q.Icon.Length > 0) sb.Append(Nl).Append("\t\t<icon>").Append(Escape(q.Icon)).Append("</icon>");
                foreach (var o in q.Objectives)
                {
                    var txt = o.Text;
                    if (txt.Length > 0)
                        sb.Append(Nl).Append("\t\t<objective><text>").Append(Escape(txt)).Append("</text></objective>");
                }
                if (q.Reward.Length > 0)
                    sb.Append(Nl).Append("\t\t<reward>").Append(Escape(q.Reward)).Append("</reward>");
                sb.Append(Nl).Append("\t</task>");
            }
            sb.Append(Nl).Append("</task_manager>");
        }

        return sb.ToString();
    }

    public static string TaskManagerLtx(IEnumerable<Quest?>? quests)
    {
        var list = quests?.Where(q => q != null && q.Format == "task_manager").Select(q => q!).ToList()
                   ?? new List<Quest>();
        if (list.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append("[task_manager]").Append(Nl);
        sb.Append("task = ").Append(string.Join(", ", list.Select(q => q.Id)));

        foreach (var q in list)
        {
            sb.Append(Nl).Append(Nl).Append('[').Append(q.Id).Append(']');
            sb.Append(Nl).Append("type = ").Append(q.Type.Length > 0 ? q.Type : "single");
            if (q.Icon.Length > 0) sb.Append(Nl).Append("icon = ").Append(q.Icon);
            if (q.Prior.Length > 0) sb.Append(Nl).Append("prior = ").Append(q.Prior);
            if (q.Storyline.Length > 0) sb.Append(Nl).Append("storyline = ").Append(q.Storyline);
            if (q.Title.Length > 0) sb.Append(Nl).Append("title = ").Append(q.Title);
            if (q.Text.Length > 0) sb.Append(Nl).Append("descr = ").Append(q.Text);

            var target = q.Objectives.Count > 0 ? q.Objectives[0].Text : "";
            if (target.Length > 0) sb.Append(Nl).Append("target = ").Append(target);
            if (q.RewardMoney.Length > 0) sb.Append(Nl).Append("reward_money = ").Append(q.RewardMoney);
            if (q.RewardItem.Length > 0) sb.Append(Nl).Append("reward_item = ").Append(q.RewardItem);
            if (q.RewardTreasure.Length > 0) sb.Append(Nl).Append("reward_treasure = ").Append(q.RewardTreasure);

            var condlist = q.Condlist ?? new List<string>();
            for (var i = 0; i < condlist.Count; i++)
                sb.Append(Nl).Append("condlist_").Append(i).Append(" = ").Append(condlist[i]);
        }

        return sb.ToString();
    }

    private static string Escape(string? v) => XmlEscaping.EscapeAttribute(v);
}