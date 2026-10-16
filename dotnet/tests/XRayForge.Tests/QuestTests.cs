using XRayForge.Core.Models;
using XRayForge.Core.Quests;

namespace XRayForge.Tests;

public class QuestTests
{
    [Fact]
    public void QuestsToXml_exports_reward_tag()
    {
        var xml = QuestExport.QuestsToXml(
            new[] { new Quest { Id = "q1", Format = "game_tasks", Title = "t", Text = "x", Reward = "money:5000" } },
            "utf-8");

        Assert.Contains("<reward>money:5000</reward>", xml);
    }

    [Fact]
    public void QuestsToXml_task_manager_reward()
    {
        var xml = QuestExport.QuestsToXml(
            new[] { new Quest { Id = "q1", Format = "task_manager", Title = "t", Text = "x", Reward = "wpn_ak74" } },
            "utf-8");

        Assert.Contains("<task id=\"q1\">", xml);
        Assert.Contains("<reward>wpn_ak74</reward>", xml);
    }

    [Fact]
    public void QuestsToXml_writes_objective_children_and_infoportion()
    {
        var xml = QuestExport.QuestsToXml(
            new[]
            {
                new Quest
                {
                    Id = "q1",
                    Format = "game_tasks",
                    Title = "t",
                    InfoportionComplete = "ic",
                    Objectives = new() { new QuestObjective { Text = "o", Icon = "i" } }
                }
            },
            "windows-1251");

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"windows-1251\"?>", xml);
        Assert.Contains("<objective>\n\t\t\t<text>o</text>\n\t\t\t<icon>i</icon>\n\t\t</objective>", xml);
        Assert.Contains("<infoportion set_complete>ic</infoportion>", xml);
    }

    [Fact]
    public void ParseGameTasks_handles_fragment_without_root_and_prio_attr()
    {
        const string xml = "<game_task id=\"q1\" prio=\"5\"><title>t</title><text>x</text><objective><text>o1</text></objective></game_task><game_task id=\"q2\"><title>t2</title></game_task>";

        var q = GameTasksParser.Parse(xml);
        Assert.Equal(2, q.Count);
        Assert.Equal("q1", q[0].Id);
        Assert.Equal("5", q[0].Prior);
        Assert.Equal("o1", q[0].Objectives[0].Text);
        Assert.Equal("game_tasks", q[0].Format);
    }

    [Fact]
    public void ParseGameTasks_handles_game_tasks_data_root_and_includes()
    {
        const string xml = "<game_tasks_data>#include \"gameplay\\tasks_escape.xml\"\n<game_task id=\"q1\"><title>t</title></game_task></game_tasks_data>";

        var q = GameTasksParser.Parse(xml);
        Assert.Single(q);
        Assert.Equal("q1", q[0].Id);
    }

    [Fact]
    public void ParseGameTasks_reads_objective_plain_text_and_short_infoportion()
    {
        const string xml = "<game_tasks><game_task id=\"q1\"><objective>plain</objective><infoportion set_complete>info_done</infoportion></game_task></game_tasks>";

        var q = GameTasksParser.Parse(xml);
        Assert.Equal("plain", q[0].Objectives[0].Text);
        Assert.Equal("info_done", q[0].InfoportionComplete);
    }

    [Fact]
    public void ParseTaskManagerLtx_reads_cop_ltx_tasks()
    {
        const string text = "[test_task]\nicon = ui_icon\nprior = 5\ntitle = test_name\ndescr = test_text\ntarget = test_target\nreward_money = 10000\ncondlist_0 = {+x} complete\nstoryline = true\n";

        var q = TaskManagerParser.Parse(text);
        Assert.Single(q);
        Assert.Equal("test_task", q[0].Id);
        Assert.Equal("money:10000", q[0].Reward);
        Assert.Equal("test_target", q[0].Objectives[0].Text);
        Assert.Equal("{+x} complete", q[0].Condlist![0]);
        Assert.Equal("task_manager", q[0].Format);
    }

    [Fact]
    public void TaskManagerLtx_round_trips_full_fields()
    {
        const string text = "[task_manager]\ntask = tbt_task_1\n[tbt_task_1]\nicon = ui_mt_image_attack_tower\nprior = 4\ntitle = tbt_task_1_name\ndescr = tbt_task_1_text\ntarget = tbt_snork\nreward_money = 10000\ncondlist_0 = {+tbt_task_1_complete} complete\nstoryline = true\n";

        var parsed = TaskManagerParser.Parse(text);
        Assert.Single(parsed);
        var q = parsed[0];
        Assert.Equal("10000", q.RewardMoney);
        Assert.Equal("true", q.Storyline);
        Assert.Equal("{+tbt_task_1_complete} complete", q.Condlist![0]);

        var outText = QuestExport.TaskManagerLtx(parsed);
        Assert.Contains("reward_money = 10000", outText);
        Assert.Contains("storyline = true", outText);
        Assert.Contains("condlist_0 = {+tbt_task_1_complete} complete", outText);
        Assert.Contains("target = tbt_snork", outText);
        Assert.Equal("", QuestExport.TaskManagerLtx(new[] { new Quest { Id = "g", Format = "game_tasks" } }));
    }

    [Fact]
    public void ParseCondlistEntry_and_CondlistEntryToStr_round_trip()
    {
        var p = QuestCatalog.ParseCondlistEntry("{+tbt_task_1_complete} complete");
        Assert.Equal("{+tbt_task_1_complete}", p.Cond);
        Assert.Equal("complete", p.Action);
        Assert.Equal("{+tbt_task_1_complete} complete", QuestCatalog.CondlistEntryToStr(p.Cond, p.Action));

        var p2 = QuestCatalog.ParseCondlistEntry("complete");
        Assert.Equal("", p2.Cond);
        Assert.Equal("complete", p2.Action);
        Assert.Equal("", QuestCatalog.CondlistEntryToStr(null, null));
    }

    [Fact]
    public void QuestChainSvg_renders_nodes_and_edges()
    {
        var quests = new List<Quest>
        {
            new() { Id = "a", Category = "Сюжет" },
            new() { Id = "b", Category = "Сюжет" },
            new() { Id = "c", Category = "Побочный" }
        };

        var svg = QuestChainSvg.Render(new[] { new[] { "a", "b" }, new[] { "a", "c" } }, quests);
        Assert.Contains("<svg", svg);
        Assert.Contains(">a</text>", svg);
        Assert.Contains("<path", svg);
        Assert.Equal("", QuestChainSvg.Render(Array.Empty<string[]>(), quests));
    }

    [Fact]
    public void ValidateQuests_reports_duplicates_prior_and_missing_translations()
    {
        var quests = new List<Quest>
        {
            new()
            {
                Id = "q1", Title = "t1", Text = "x1", Prior = "abc",
                Objectives = new() { new QuestObjective { Text = "o1" } }
            },
            new() { Id = "q1" }
        };
        var langs = new List<Language>
        {
            new()
            {
                Locale = "rus",
                Entries = new() { new LanguageEntry { Id = "t1", Text = "Т" }, new LanguageEntry { Id = "o1", Text = "" } }
            }
        };

        var issues = QuestAnalysis.Validate(quests, langs);
        Assert.Equal(7, issues.Count);
        Assert.Contains(issues, i => i.Severity == "error" && i.Message == "Дубликат id: q1");
        Assert.Contains(issues, i => i.Severity == "error" && i.Message == "Приоритет не число: abc");
        Assert.Contains(issues, i => i.Severity == "warning" && i.Message == "Нет перевода text: x1");
        Assert.Contains(issues, i => i.Severity == "warning" && i.Message == "Нет перевода цели: o1");
        Assert.Contains(issues, i => i.Severity == "warning" && i.Message == "Нет целей");
        Assert.All(issues, i => Assert.Equal("q1", i.Quest));
    }

    [Fact]
    public void ValidateQuests_accepts_null_languages()
    {
        var issues = QuestAnalysis.Validate(
            new[] { new Quest { Id = "q", Title = "t", Text = "x", Objectives = new() { new QuestObjective { Text = "o" } } } },
            null);

        Assert.Equal(3, issues.Count);
        Assert.All(issues, i => Assert.Equal("warning", i.Severity));
    }

    [Fact]
    public void ComputeQuestChains_links_requires_to_infoportion_complete()
    {
        var quests = new List<Quest>
        {
            new() { Id = "a", InfoportionComplete = "ia" },
            new() { Id = "b", Requires = new() { "ia" }, InfoportionComplete = "ib" },
            new() { Id = "c", Requires = new() { "ib" } }
        };

        var chains = QuestAnalysis.ComputeChains(quests);
        Assert.Single(chains);
        Assert.Equal(new[] { "a", "b", "c" }, chains[0]);

        quests.Add(new Quest { Id = "d", Requires = new() { "unknown" } });
        var chains2 = QuestAnalysis.ComputeChains(quests);
        Assert.Equal(2, chains2.Count);
        Assert.Equal(new[] { "d" }, chains2[1]);
        Assert.Empty(QuestAnalysis.ComputeChains(null));
    }
}