using XRayForge.Core.Graph;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Storage;

namespace XRayForge.Tests;

/// <summary>Профиль `%APPDATA%`: настройки, список проектов, бэкапы, снапшоты gamedata, автосейв.</summary>
public class StorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "xrayforge-profile-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
        catch (IOException) { }
    }

    private static Project NewProject(string name = "p1")
    {
        var project = ProjectMigration.Empty();
        project.ProjectName = name;
        project.DialogueId = name;
        project.Nodes.Add(GraphData.SafeNode(new DialogueNode { Id = name + "__start", Type = NodeTypes.Start }, 0));
        project.LtxFiles!["config/system.ltx"] = "[system]\n";
        project.Scripts!.Add(new ScriptFile { Name = "bind.script", Text = "t", Original = "t" });
        return project;
    }

    [Fact]
    public void Program_settings_round_trip_and_normalize()
    {
        var store = new AppDataService(_root);
        store.SaveProgramSettings(new ProgramSettings
        {
            Accent = "purple",
            Density = "compact",
            Locale = "en",
            EditorFontSize = 40,
            AutosaveInterval = 15
        });

        var loaded = store.LoadProgramSettings();
        Assert.Equal("purple", loaded.Accent);
        Assert.Equal("compact", loaded.Density);
        Assert.Equal("en", loaded.Locale);
        Assert.Equal(24, loaded.EditorFontSize); // clamp 10..24
        Assert.Equal(15, loaded.AutosaveInterval);
        Assert.Equal("mod+s", loaded.Hotkeys["save"]);
    }

    [Fact]
    public void Program_settings_fall_back_to_defaults_for_unknown_values()
    {
        var normalized = ProgramSettingsCatalog.Normalize(new ProgramSettings
        {
            Accent = "nope",
            Density = "wide",
            Locale = "de",
            Theme = "",
            EditorFontSize = 2,
            AutosaveInterval = null,
            Hotkeys = new Dictionary<string, string> { ["save"] = "F5", ["extra"] = "X" }
        });

        Assert.Equal("green", normalized.Accent);
        Assert.Equal("normal", normalized.Density);
        Assert.Equal("ru", normalized.Locale);
        Assert.Equal("dark", normalized.Theme);
        Assert.Equal(10, normalized.EditorFontSize);
        Assert.Equal(0, normalized.AutosaveInterval);
        Assert.Equal("F5", normalized.Hotkeys["save"]);
        Assert.Equal("X", normalized.Hotkeys["extra"]);
        Assert.Equal(12, normalized.Hotkeys.Count);

        Assert.Equal("dark", ProgramSettingsCatalog.FromJson("{oops").Theme);
    }

    [Fact]
    public void Hotkey_combo_matching_matches_js()
    {
        Assert.True(ProgramSettingsCatalog.ComboMatches("mod+s", ctrlOrCmd: true, shift: false, alt: false, key: "S"));
        Assert.False(ProgramSettingsCatalog.ComboMatches("mod+s", ctrlOrCmd: false, shift: false, alt: false, key: "s"));
        Assert.True(ProgramSettingsCatalog.ComboMatches("shift+z", ctrlOrCmd: false, shift: true, alt: false, key: "z"));
        Assert.False(ProgramSettingsCatalog.ComboMatches("", ctrlOrCmd: false, shift: false, alt: false, key: ""));
    }

    [Fact]
    public void Current_project_is_saved_without_gamedata_fields()
    {
        var store = new AppDataService(_root);
        store.SaveCurrentProject(NewProject());

        var loaded = store.LoadCurrentProject();
        Assert.NotNull(loaded);
        Assert.Equal("p1", loaded!.ProjectName);
        Assert.Single(loaded.Nodes);
        Assert.Empty(loaded.LtxFiles!);
        Assert.Empty(loaded.Scripts!);
        Assert.True(File.Exists(store.PathFor(AppDataService.CurrentProjectFileName)));
    }

    [Fact]
    public void Current_project_is_null_when_file_is_absent_or_broken()
    {
        var store = new AppDataService(_root);
        Assert.Null(store.LoadCurrentProject());

        Directory.CreateDirectory(_root);
        File.WriteAllText(store.PathFor(AppDataService.CurrentProjectFileName), "{oops");
        Assert.Null(store.LoadCurrentProject());
    }

    [Fact]
    public void Projects_list_upserts_newest_first_and_renames()
    {
        var store = new AppDataService(_root);
        store.SaveProject(NewProject("alpha"));
        store.SaveProject(NewProject("beta"));
        Assert.Equal(new[] { "beta", "alpha" }, store.ListProjects().Select(p => p.Name));
        Assert.True(store.ListProjects()[0].UpdatedAt > 0);

        store.SaveProject(NewProject("alpha")); // повторное сохранение поднимает проект наверх
        Assert.Equal(new[] { "alpha", "beta" }, store.ListProjects().Select(p => p.Name));

        store.RenameProject("beta", "gamma");
        Assert.Equal(new[] { "alpha", "gamma" }, store.ListProjects().Select(p => p.Name));

        store.RemoveProject("alpha");
        Assert.Equal(new[] { "gamma" }, store.ListProjects().Select(p => p.Name));
    }

    [Fact]
    public void Resumable_project_requires_a_list_entry_and_clears_stale_file()
    {
        var store = new AppDataService(_root);
        store.SaveCurrentProject(NewProject("gone"));

        Assert.Null(store.LoadResumableProject());
        Assert.False(File.Exists(store.PathFor(AppDataService.CurrentProjectFileName)));

        store.SaveProject(NewProject("kept"));
        store.SaveCurrentProject(NewProject("kept"));

        Assert.Equal("kept", store.LoadResumableProject()!.ProjectName);
    }

    [Fact]
    public void Removing_current_project_clears_current_project_file()
    {
        var store = new AppDataService(_root);
        store.SaveProject(NewProject("deleted"));
        store.SaveCurrentProject(NewProject("deleted"));

        store.RemoveProject("deleted");

        Assert.Empty(store.ListProjects());
        Assert.Null(store.LoadCurrentProject());
        Assert.False(File.Exists(store.PathFor(AppDataService.CurrentProjectFileName)));
    }

    [Fact]
    public void Removing_other_project_keeps_current_project_file()
    {
        var store = new AppDataService(_root);
        store.SaveProject(NewProject("keep"));
        store.SaveProject(NewProject("drop"));
        store.SaveCurrentProject(NewProject("keep"));

        store.RemoveProject("drop");

        Assert.Equal("keep", store.LoadCurrentProject()!.ProjectName);
        Assert.Equal(new[] { "keep" }, store.ListProjects().Select(p => p.Name));
    }

    [Fact]
    public void Projects_list_normalizes_broken_entries_like_js()
    {
        var store = new AppDataService(_root);
        Directory.CreateDirectory(_root);
        File.WriteAllText(store.PathFor(AppDataService.ProjectsFileName),
            "[null,{\"updatedAt\":\"abc\"},{\"name\":\"named\",\"updatedAt\":123,\"data\":{\"projectName\":\"named\"}}]");

        var list = store.ListProjects();
        Assert.Equal(2, list.Count);
        Assert.Equal("project_0", list[0].Name);
        Assert.Equal(0, list[0].UpdatedAt);
        Assert.Empty(list[0].Data.Nodes);
        Assert.Equal("named", list[1].Name);
        Assert.Equal(123, list[1].UpdatedAt);
        Assert.Equal("named", list[1].Data.ProjectName);

        File.WriteAllText(store.PathFor(AppDataService.ProjectsFileName), "{oops");
        Assert.Empty(store.ListProjects());
    }

    [Fact]
    public void Backups_are_limited_and_newest_first()
    {
        var store = new AppDataService(_root);
        for (var i = 0; i < 12; i++) store.AddBackup(NewProject("p" + i), timestamp: 1000 + i);

        var backups = store.LoadBackups();
        Assert.Equal(ProjectSchema.BackupsLimit, backups.Count);
        Assert.Equal(1011, backups[0].Timestamp);
        Assert.Equal(1002, backups[^1].Timestamp);
        Assert.Equal("p11", backups[0].Data.ProjectName);
        Assert.Empty(backups[0].Data.LtxFiles!);
    }

    [Fact]
    public void Gamedata_snapshots_are_added_and_removed()
    {
        var store = new AppDataService(_root);
        store.SaveGamedataSnapshot("p1", AppDataService.ToSnapshot(NewProject("p1")));

        var snapshot = store.LoadGamedataSnapshot("p1");
        Assert.NotNull(snapshot);
        Assert.Equal("[system]\n", snapshot!.LtxFiles!["config/system.ltx"]);
        Assert.Single(snapshot.Scripts!);
        Assert.Null(store.LoadGamedataSnapshot("other"));
        Assert.Null(store.LoadGamedataSnapshot(null));

        store.SaveGamedataSnapshot("p2", new GamedataSnapshot());
        store.RemoveGamedataSnapshot("p1");
        Assert.Null(store.LoadGamedataSnapshot("p1"));
        Assert.NotNull(store.LoadGamedataSnapshot("p2"));
    }

    [Fact]
    public void Gamedata_snapshot_restores_heavy_fields_without_wiping_when_empty()
    {
        var store = new AppDataService(_root);
        var source = NewProject("snap");
        store.SaveGamedataSnapshot("snap", AppDataService.ToSnapshot(source));

        var target = ProjectMigration.Empty();
        target.ProjectName = "snap";
        store.LoadGamedataSnapshot("snap")!.ApplyTo(target);

        Assert.Equal("[system]\n", target.LtxFiles!["config/system.ltx"]);
        Assert.Single(target.Scripts!);

        new GamedataSnapshot().ApplyTo(target);
        Assert.Single(target.LtxFiles!);
        Assert.Single(target.Scripts!);

        var blank = new Project { LtxFiles = null, Textures = null, Scripts = null, ItemFiles = null, ItemOrigins = null };
        var snapshot = AppDataService.ToSnapshot(blank);
        Assert.Empty(snapshot.LtxFiles!);
        Assert.Empty(snapshot.Textures!);
        Assert.Empty(snapshot.Scripts!);
        Assert.Empty(snapshot.ItemFiles!);
        Assert.Empty(snapshot.ItemOrigins!);
    }

    [Fact]
    public void Reset_removes_profile_files()
    {
        var store = new AppDataService(_root);
        store.SaveProgramSettings(new ProgramSettings());
        store.SaveCurrentProject(NewProject());
        store.SaveProject(NewProject());
        store.AddBackup(NewProject());
        store.SaveGamedataSnapshot("p1", new GamedataSnapshot());

        store.Reset();

        Assert.False(File.Exists(store.PathFor(AppDataService.SettingsFileName)));
        Assert.False(File.Exists(store.PathFor(AppDataService.ProjectsFileName)));
        Assert.False(File.Exists(store.PathFor(AppDataService.BackupsFileName)));
        Assert.False(File.Exists(store.PathFor(AppDataService.GamedataFileName)));
        Assert.Null(store.LoadCurrentProject());
        Assert.Empty(store.ListProjects());
        Assert.Empty(store.LoadBackups());
        Assert.Null(store.LoadGamedataSnapshot("p1"));
    }

    [Fact]
    public void Default_root_is_xrayforge_in_appdata()
    {
        var store = new AppDataService();
        Assert.Equal("XRayForge", Path.GetFileName(store.Root));
        Assert.EndsWith(AppDataService.DirectoryName, store.Root);
        Assert.EndsWith(AppDataService.SavesDirectoryName, store.SavesDirectory);
    }

    [Fact]
    public void Project_file_service_sanitizes_names_like_js()
    {
        Assert.Equal("project", ProjectFileService.SanitizeName(null));
        Assert.Equal("project", ProjectFileService.SanitizeName(""));
        Assert.Equal("bar-baz_1", ProjectFileService.SanitizeName("bar-baz_1"));
        Assert.Equal("my_project", ProjectFileService.SanitizeName("my project"));
        Assert.Equal("_", ProjectFileService.SanitizeName("мой проект!"));

        var path = ProjectFileService.SaveProjectFile(Path.Combine(_root, "saves"), "мой проект!", NewProject("x"));
        Assert.EndsWith("_.project.json", path);
        Assert.True(File.Exists(path));
        Assert.Equal("x", ProjectSerializer.Load(path)!.ProjectName);
    }

    [Fact]
    public void Autosave_tick_writes_current_project_and_list()
    {
        var store = new AppDataService(_root);
        var project = NewProject("auto");
        using var service = new AutosaveService(store, () => project, () => new ProgramSettings { AutosaveInterval = 0 })
        {
            Dirty = true
        };

        Assert.True(service.Tick());
        Assert.NotNull(service.LastAutosave);
        Assert.False(service.Dirty);
        Assert.Equal("auto", store.LoadCurrentProject()!.ProjectName);
        Assert.Contains(store.ListProjects(), p => p.Name == "auto");
    }

    [Fact]
    public void Autosave_tick_without_project_does_nothing()
    {
        var store = new AppDataService(_root);
        using var service = new AutosaveService(store, () => null, () => new ProgramSettings());

        Assert.False(service.Tick());
        Assert.Null(service.LastAutosave);
        Assert.Empty(store.ListProjects());
    }

    [Fact]
    public void Autosave_timer_fires_and_stops()
    {
        var store = new AppDataService(_root);
        var project = NewProject("timer");
        using var saved = new ManualResetEventSlim(false);
        using var service = new AutosaveService(store, () => project, () => new ProgramSettings { AutosaveInterval = 0.05 });
        service.Saved += (_, _) => saved.Set();

        service.ApplySettings();
        Assert.Equal(0.05, service.IntervalSeconds, 3);
        Assert.True(saved.Wait(TimeSpan.FromSeconds(20)), "автосейв не сработал по таймеру");

        service.ApplySettings(); // тот же интервал — таймер не пересоздаётся
        Assert.Equal(0.05, service.IntervalSeconds, 3);

        service.Stop();
        Assert.Equal(0, service.IntervalSeconds);
    }

    [Fact]
    public void Autosave_is_disabled_when_interval_is_not_positive()
    {
        var store = new AppDataService(_root);
        using var service = new AutosaveService(store, () => null, () => new ProgramSettings { AutosaveInterval = null });
        service.ApplySettings();

        Assert.Equal(0, service.IntervalSeconds);
    }
}