using XRayForge.Core.Ui;

namespace XRayForge.Tests;

public class ToolbarCatalogTests
{
    [Fact]
    public void Actions_are_empty_without_open_project()
    {
        Assert.Empty(ToolbarCatalog.Actions(null));
        Assert.Empty(ToolbarCatalog.Actions(null, debug: true));
    }

    [Fact]
    public void Scripts_section_offers_only_save()
    {
        var actions = ToolbarCatalog.Actions(NavigationCatalog.ScriptsId, debug: true);

        var action = Assert.Single(actions);
        Assert.Equal(ToolbarCatalog.SaveId, action.Id);
    }

    [Fact]
    public void Unknown_section_falls_back_to_save_only()
    {
        var actions = ToolbarCatalog.Actions("stub-section", debug: true);

        Assert.Single(actions);
        Assert.Equal(ToolbarCatalog.SaveId, actions[0].Id);
    }

    [Fact]
    public void Dialogues_section_offers_export_check_and_debug_xml()
    {
        var actions = ToolbarCatalog.Actions(NavigationCatalog.DialoguesId, debug: true).Select(item => item.Id).ToList();

        Assert.Equal(new[]
        {
            ToolbarCatalog.SaveId,
            ToolbarCatalog.ExportGameDialogsId,
            ToolbarCatalog.CheckGraphId,
            ToolbarCatalog.ExportXmlId
        }, actions);
    }

    [Fact]
    public void Dialogues_section_hides_debug_xml_in_release()
    {
        var actions = ToolbarCatalog.Actions(NavigationCatalog.DialoguesId).Select(item => item.Id).ToList();

        Assert.DoesNotContain(ToolbarCatalog.ExportXmlId, actions);
        Assert.Contains(ToolbarCatalog.ExportGameDialogsId, actions);
    }

    [Fact]
    public void Overview_section_offers_package_and_self_check()
    {
        var actions = ToolbarCatalog.Actions(NavigationCatalog.OverviewId).Select(item => item.Id).ToList();

        Assert.Equal(new[]
        {
            ToolbarCatalog.SaveId,
            ToolbarCatalog.ExportGameZipId,
            ToolbarCatalog.SelfCheckId
        }, actions);
        Assert.DoesNotContain(ToolbarCatalog.ExportGameDialogsId, actions);
        Assert.DoesNotContain(ToolbarCatalog.CheckGraphId, actions);
    }

    [Fact]
    public void Every_action_has_glyph_and_localization_key()
    {
        foreach (var action in ToolbarCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.Glyph));
            Assert.False(string.IsNullOrWhiteSpace(action.LabelKey));
            Assert.Equal(action.Id, ToolbarCatalog.Find(action.Id)?.Id);
        }
    }
}
