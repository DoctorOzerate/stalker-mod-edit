using System.Text.Json;
using XRayForge.Core.Graph;
using XRayForge.Core.Localization;
using XRayForge.Core.Models;
using XRayForge.Core.Projects;
using XRayForge.Core.Storage;
using XRayForge.Core.Ui;

namespace XRayForge.Tests;

/// <summary>Этап 5: темы/токены, навигация, горячие клавиши, серии игры (каркас UI).</summary>
public class UiTests
{
    [Fact]
    public void ThemeTokensValidateHexAndContrast()
    {
        Assert.True(ThemeTokens.IsHex("#2FE06A"));
        Assert.True(ThemeTokens.IsHex("#abcdef"));
        Assert.False(ThemeTokens.IsHex("2FE06A"));
        Assert.False(ThemeTokens.IsHex("#2FE06"));
        Assert.False(ThemeTokens.IsHex("#2FE06Z"));
        Assert.False(ThemeTokens.IsHex(null));

        Assert.Equal(21.0, ThemeTokens.ContrastRatio("#FFFFFF", "#000000"), 5);
        Assert.Equal(1.0, ThemeTokens.ContrastRatio("#2FE06A", "#2FE06A"), 5);

        Assert.Equal(0, ThemeTokens.RelativeLuminance("#000000"), 5);
        Assert.Equal(1, ThemeTokens.RelativeLuminance("#FFFFFF"), 5);
    }

    [Fact]
    public void ThemeTokensMixInterpolatesChannels()
    {
        Assert.Equal("#808080", ThemeTokens.Mix("#000000", "#FFFFFF", 0.5));
        Assert.Equal("#000000", ThemeTokens.Mix("#000000", "#FFFFFF", 0));
        Assert.Equal("#FFFFFF", ThemeTokens.Mix("#000000", "#FFFFFF", 1));
        Assert.Equal("#2FE06A", ThemeTokens.Mix("#2FE06A", "#FFFFFF", 0));
        // Некорректный вход не бросает исключение.
        Assert.Equal("#2FE06A", ThemeTokens.Mix("#2FE06A", "oops", 0.5));
    }

    [Fact]
    public void ThemeTokensExposeStableKeySet()
    {
        Assert.Equal(20, ThemeTokens.Keys.Length);
        Assert.Equal(ThemeTokens.Keys.Length, ThemeTokens.Keys.Distinct().Count());
        Assert.Contains(nameof(ThemeTokens.ChromeBackdrop), ThemeTokens.Keys);
        Assert.Contains(nameof(ThemeTokens.GridLine), ThemeTokens.Keys);
        Assert.Equal(ThemeTokens.Keys.OrderBy(x => x, StringComparer.Ordinal),
            ThemeCatalog.Steel.ToDictionary().Keys.OrderBy(x => x, StringComparer.Ordinal));
        Assert.True(ThemeCatalog.Steel.IsValid());

        var tokens = ThemeCatalog.Steel.WithAccent("#4C8CE0", "#2F6FC0");
        Assert.Equal("#4C8CE0", tokens.AccentMain);
        Assert.Equal("#2F6FC0", tokens.AccentHover);
        Assert.True(tokens.IsValid());
        Assert.NotEqual(ThemeCatalog.Steel.AccentDim, tokens.AccentDim);
    }

    [Fact]
    public void ThemeTokensFromMapFallsBackToBase()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SurfacePanel"] = "#101010",
            ["AccentMain"] = "not-a-color"
        };
        var tokens = ThemeTokens.FromMap(map);
        Assert.Equal("#101010", tokens.SurfacePanel);
        Assert.Equal(ThemeCatalog.Steel.AccentMain, tokens.AccentMain);
        Assert.Equal(ThemeCatalog.Steel.SurfaceBase, tokens.SurfaceBase);
    }

    [Fact]
    public void ThemeTokensWithReplacesSingleToken()
    {
        var hazard = ThemeCatalog.Olive.With("AccentMain", "#FF2A2A");
        Assert.Equal("#FF2A2A", hazard.AccentMain);
        Assert.Equal(ThemeCatalog.Olive.SurfaceBase, hazard.SurfaceBase);
    }

    [Fact]
    public void CatalogDeclaresThemesWithPaletteValues()
    {
        Assert.Equal(7, ThemeCatalog.Themes.Count);
        Assert.Equal(new[] { "steel", "olive", "hazard", "amber", "cobalt", "arctic", "graphite" },
            ThemeCatalog.Themes.Select(t => t.Id));
        Assert.Equal(ThemeCatalog.Themes.Count, ThemeCatalog.Themes.Select(t => t.Id).Distinct().Count());
        Assert.All(ThemeCatalog.Themes, theme => Assert.True(theme.Tokens.IsValid(), theme.Id));
        Assert.All(ThemeCatalog.Themes, theme => Assert.False(string.IsNullOrWhiteSpace(theme.DescriptionKey), theme.Id));

        Assert.Equal("work.png", ThemeCatalog.Themes[0].Reference);
        Assert.False(ThemeCatalog.Themes[0].IsAccentState);
        Assert.True(ThemeCatalog.Themes[1].IsAccentState);
        Assert.True(ThemeCatalog.Themes[2].IsAccentState);
        Assert.False(ThemeCatalog.Themes[3].IsAccentState);
        Assert.False(ThemeCatalog.Themes[4].IsAccentState);
        Assert.False(ThemeCatalog.Themes[5].IsAccentState);
        Assert.False(ThemeCatalog.Themes[6].IsAccentState);

        // Значения из tokens/00-palette.tokens.md.
        Assert.Equal("#101010", ThemeCatalog.Steel.SurfaceBase);
        Assert.Equal("#2FE06A", ThemeCatalog.Steel.AccentMain);
        Assert.Equal("#16210F", ThemeCatalog.Olive.SurfacePanel);
        Assert.Equal("#4CE04C", ThemeCatalog.Olive.AccentMain);
        Assert.Equal("#1A0E0E", ThemeCatalog.Hazard.SurfacePanel);
        Assert.Equal("#FF2A2A", ThemeCatalog.Hazard.AccentMain);

        // Расширенные темы: CRT, холодная, светлая и монохромная.
        Assert.Equal("#FFB000", ThemeCatalog.Amber.AccentMain);
        Assert.Equal("#4C9AFF", ThemeCatalog.Cobalt.AccentMain);
        Assert.Equal("#E8EEF2", ThemeCatalog.Arctic.SurfaceBase);
        Assert.Equal("#C3CBD5", ThemeCatalog.Graphite.AccentMain);
        Assert.Equal("#202020", ThemeCatalog.Steel.ChromeBackdrop);
        Assert.Equal("#DCE5EC", ThemeCatalog.Arctic.ChromeBackdrop);
    }

    [Fact]
    public void ThemeAliasesMapLegacyValuesToSteel()
    {
        Assert.Equal("steel", ThemeCatalog.NormalizeThemeId("dark"));
        Assert.Equal("arctic", ThemeCatalog.NormalizeThemeId("light"));
        Assert.Equal("steel", ThemeCatalog.NormalizeThemeId("auto"));
        Assert.Equal("steel", ThemeCatalog.NormalizeThemeId(""));
        Assert.Equal("steel", ThemeCatalog.NormalizeThemeId("unknown"));
        Assert.Equal("steel", ThemeCatalog.NormalizeThemeId(null));
        Assert.Equal("olive", ThemeCatalog.NormalizeThemeId("olive"));
        Assert.Equal("hazard", ThemeCatalog.NormalizeThemeId("Hazard"));
        Assert.Equal("amber", ThemeCatalog.NormalizeThemeId("amber"));
        Assert.Equal("cobalt", ThemeCatalog.NormalizeThemeId("Cobalt"));
        Assert.Equal("arctic", ThemeCatalog.NormalizeThemeId("arctic"));
        Assert.Equal("graphite", ThemeCatalog.NormalizeThemeId("graphite"));
        Assert.Equal("Olive Panel", ThemeCatalog.Theme("olive").Name);
        Assert.Equal("Amber CRT", ThemeCatalog.Theme("amber").Name);
    }

    [Fact]
    public void ThemeTextKeepsReadableContrast()
    {
        foreach (var theme in ThemeCatalog.Themes)
        {
            var tokens = theme.Tokens;
            Assert.True(ThemeTokens.ContrastRatio(tokens.TextPrimary, tokens.SurfaceBase) >= 4.5,
                theme.Id + ": основной текст");
            Assert.True(ThemeTokens.ContrastRatio(tokens.TextMuted, tokens.SurfacePanel) >= 3.0,
                theme.Id + ": приглушённый текст");
            Assert.True(ThemeTokens.ContrastRatio(tokens.TextInverted, tokens.AccentMain) >= 4.5,
                theme.Id + ": текст на акценте");
        }
    }

    [Fact]
    public void DensityPresetsMatchProgramSettingsIds()
    {
        Assert.Equal(ProgramSettingsCatalog.Densities.OrderBy(x => x, StringComparer.Ordinal),
            ThemeCatalog.DensityPresets.Keys.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal("compact", ThemeCatalog.DensityPresets["compact"].Id);
        Assert.Equal("normal", ThemeCatalog.Density(null).Id);
        Assert.Equal("compact", ThemeCatalog.Density("compact").Id);
        Assert.Equal("normal", ThemeCatalog.Density("normal").Id);
        Assert.Equal("normal", ThemeCatalog.Density("nope").Id);
        Assert.Equal("roomy", ThemeCatalog.Density("roomy").Id);

        var compact = ThemeCatalog.DensityPresets["compact"];
        var normal = ThemeCatalog.DensityPresets["normal"];
        var roomy = ThemeCatalog.DensityPresets["roomy"];
        Assert.True(compact.RowHeight < normal.RowHeight && normal.RowHeight < roomy.RowHeight);
        Assert.True(compact.PanelGap < normal.PanelGap && normal.PanelGap < roomy.PanelGap);
        Assert.True(compact.PanelPadding < normal.PanelPadding && normal.PanelPadding < roomy.PanelPadding);
        Assert.True(compact.FontSize < normal.FontSize && normal.FontSize < roomy.FontSize);
        Assert.True(compact.TitleFontSize < normal.TitleFontSize && normal.TitleFontSize < roomy.TitleFontSize);
    }

    [Fact]
    public void AccentsCoverProgramSettingsPresets()
    {
        Assert.Equal(ProgramSettingsCatalog.AccentPresets.Keys.OrderBy(x => x, StringComparer.Ordinal),
            ThemeCatalog.Accents.Keys.OrderBy(x => x, StringComparer.Ordinal));

        foreach (var pair in ThemeCatalog.Accents)
        {
            Assert.Equal(2, pair.Value.Length);
            Assert.True(ThemeTokens.IsHex(pair.Value[0]), pair.Key + ": основной");
            Assert.True(ThemeTokens.IsHex(pair.Value[1]), pair.Key + ": hover");
            Assert.True(ThemeTokens.ContrastRatio(pair.Value[0], ThemeCatalog.Steel.SurfaceBase) >= 3.0,
                pair.Key + ": читаемость на фоне");
            Assert.Equal(pair.Value[0], ThemeCatalog.AccentPair("steel", pair.Key)[0]);
        }
    }

    [Fact]
    public void ResolveMergesThemeAccentAndDensity()
    {
        var fallback = ThemeCatalog.Resolve("dark", "green", "normal");
        Assert.Equal("steel", fallback.ThemeId);
        Assert.Equal("green", fallback.AccentId);
        Assert.Equal("normal", fallback.DensityId);
        Assert.Equal("#2FE06A", fallback.Tokens.AccentMain);

        var olive = ThemeCatalog.Resolve("olive", "green", "compact");
        Assert.Equal("olive", olive.ThemeId);
        Assert.Equal("#4CE04C", olive.Tokens.AccentMain);
        Assert.Equal("#7CFF7C", olive.Tokens.AccentGlow);
        Assert.Equal("compact", olive.Metrics.Id);

        var custom = ThemeCatalog.Resolve("hazard", "purple", "roomy");
        Assert.Equal("hazard", custom.ThemeId);
        Assert.Equal("purple", custom.AccentId);
        Assert.Equal("#A86FE0", custom.Tokens.AccentMain);
        Assert.Equal(ThemeCatalog.Hazard.SurfaceBase, custom.Tokens.SurfaceBase);
        Assert.True(custom.Tokens.IsValid());

        // Неизвестные значения → акцент темы и `normal`.
        var unknown = ThemeCatalog.Resolve("nope", "nope", "nope");
        Assert.Equal("steel", unknown.ThemeId);
        Assert.Equal("green", unknown.AccentId);
        Assert.Equal("normal", unknown.DensityId);

        var hazard = ThemeCatalog.Resolve("hazard", null, "normal");
        Assert.Equal("red", hazard.AccentId);
        Assert.Equal("#FF2A2A", hazard.Tokens.AccentMain);

        // Новые темы используют собственный токен акцента по умолчанию.
        var amber = ThemeCatalog.Resolve("amber", null, "normal");
        Assert.Equal("amber", amber.AccentId);
        Assert.Equal("#FFB000", amber.Tokens.AccentMain);
        Assert.Equal("amber", ThemeCatalog.DefaultAccentId("amber"));

        var cobalt = ThemeCatalog.Resolve("cobalt", null, "normal");
        Assert.Equal("blue", cobalt.AccentId);
        Assert.Equal("#4C9AFF", cobalt.Tokens.AccentMain);
        Assert.Equal("#0A1018", cobalt.Tokens.SurfaceBase);

        var arctic = ThemeCatalog.Resolve("light", null, "normal");
        Assert.Equal("arctic", arctic.ThemeId);
        Assert.Equal("teal", arctic.AccentId);
        Assert.Equal("#E8EEF2", arctic.Tokens.SurfaceBase);
        Assert.Equal("#DCE5EC", arctic.Tokens.ChromeBackdrop);

        var graphiteDefault = ThemeCatalog.Resolve("graphite", null, "normal");
        Assert.Equal("silver", graphiteDefault.AccentId);
        Assert.Equal("#C3CBD5", graphiteDefault.Tokens.AccentMain);

        var graphite = ThemeCatalog.Resolve("graphite", "purple", "compact");
        Assert.Equal("purple", graphite.AccentId);
        Assert.Equal("#A86FE0", graphite.Tokens.AccentMain);
        Assert.Equal(ThemeCatalog.Graphite.SurfaceBase, graphite.Tokens.SurfaceBase);
    }

    [Fact]
    public void ColorResourcesExposeTokensPlusAccentAlias()
    {
        var resources = ThemeCatalog.Resolve("dark", "blue", "normal").ColorResources();
        Assert.Equal(ThemeTokens.Keys.Length + 1, resources.Count);
        foreach (var key in ThemeTokens.Keys) Assert.True(resources.ContainsKey(key), key);
        Assert.Equal("#4C8CE0", resources["Accent"]);
        Assert.Equal("#4C8CE0", resources["AccentMain"]);
    }

    [Fact]
    public void NavigationCatalogKeepsTabOrder()
    {
        var items = NavigationCatalog.Items;
        Assert.Equal(9, items.Count);
        Assert.Equal(new[]
        {
            "dialogues", "characters", "items", "icons", "scripts",
            "system", "languages", "quests", "overview"
        }, items.Select(i => i.Id));
        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
        Assert.Equal(items.Count, items.Select(i => i.LabelKey).Distinct().Count());
        Assert.Equal("dialogues", NavigationCatalog.DefaultTabId);
        Assert.Equal("dialogues", NavigationCatalog.Find("dialogues")!.Id);
        Assert.Null(NavigationCatalog.Find("nope"));
        Assert.Null(NavigationCatalog.Find(null));
        Assert.Equal(0, NavigationCatalog.IndexOf("dialogues"));
        Assert.Equal(8, NavigationCatalog.IndexOf("overview"));
        Assert.Equal(-1, NavigationCatalog.IndexOf("nope"));
    }

    [Fact]
    public void NavigationHotkeysMapCtrlNumbersToTabs()
    {
        Assert.Equal("dialogues", NavigationCatalog.ByHotkeyIndex(1)!.Id);
        Assert.Equal("items", NavigationCatalog.ByHotkeyIndex(3)!.Id);
        Assert.Equal("overview", NavigationCatalog.ByHotkeyIndex(9)!.Id);
        Assert.Null(NavigationCatalog.ByHotkeyIndex(0));
        Assert.Null(NavigationCatalog.ByHotkeyIndex(10));

        Assert.Equal("\U0001F4AC", NavigationCatalog.Find("dialogues")!.Glyph);
        foreach (var item in NavigationCatalog.Items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Glyph));
            Assert.True(item.Glyph.Length <= 2, item.Id + ": ожидается один символ (суррогатная пара)");
        }
    }

    [Fact]
    public void NavigationAccentStatesMatchReferenceScreens()
    {
        var olive = NavigationCatalog.Items.Where(i => i.IsAccentState).Select(i => i.Id).ToArray();
        Assert.Equal(new[] { "items", "icons", "scripts" }, olive);

        foreach (var item in NavigationCatalog.Items)
            Assert.True(item.Accent is "steel" or "olive" or "hazard", item.Id + ": " + item.Accent);

        Assert.Equal("olive", NavigationCatalog.Find("items")!.Accent);
        Assert.Equal("steel", NavigationCatalog.Find("overview")!.Accent);
    }

    [Fact]
    public void NavigationLabelsExistInBothLocales()
    {
        foreach (var item in NavigationCatalog.Items)
        {
            foreach (var locale in new[] { "ru", "en" })
            {
                var label = LocalizationService.Tr(locale, item.LabelKey);
                Assert.NotEqual(item.LabelKey, label);
                Assert.False(string.IsNullOrWhiteSpace(label), item.LabelKey + ": " + locale);
            }
        }
    }

    [Fact]
    public void HotkeyGestureParsesAndDisplaysCombos()
    {
        var save = HotkeyGesture.Parse("mod+s")!;
        Assert.True(save.Ctrl);
        Assert.False(save.Shift);
        Assert.False(save.Alt);
        Assert.Equal("s", save.Key);
        Assert.Equal("Ctrl+S", save.Display);

        var redo = HotkeyGesture.Parse("shift+z")!;
        Assert.False(redo.Ctrl);
        Assert.True(redo.Shift);
        Assert.Equal("Shift+Z", redo.Display);

        var complex = HotkeyGesture.Parse("Ctrl+Shift+Alt+Delete")!;
        Assert.Equal(new HotkeyGesture(true, true, true, "delete"), complex);
        Assert.Equal("Ctrl+Shift+Alt+Del", complex.Display);

        var function = HotkeyGesture.Parse("F5")!;
        Assert.Equal("f5", function.Key);
        Assert.Equal("F5", function.Display);

        var digit = HotkeyGesture.Parse("1")!;
        Assert.False(digit.Ctrl);
        Assert.Equal("1", digit.Display);

        Assert.Null(HotkeyGesture.Parse(null));
        Assert.Null(HotkeyGesture.Parse(""));
        Assert.Null(HotkeyGesture.Parse("   "));
        Assert.Null(HotkeyGesture.Parse("mod+"));
        Assert.Null(HotkeyGesture.Parse("meta+s"));
    }

    [Fact]
    public void HotkeyGestureNormalizesAliases()
    {
        Assert.Equal("escape", HotkeyGesture.Parse("esc")!.Key);
        Assert.Equal("Esc", HotkeyGesture.Parse("esc")!.Display);
        Assert.Equal("enter", HotkeyGesture.Parse("return")!.Key);
        Assert.Equal("Del", HotkeyGesture.KeyDisplay("delete"));
        Assert.Equal("Space", HotkeyGesture.KeyDisplay("space"));
        Assert.Equal("↓", HotkeyGesture.KeyDisplay("down"));
        Assert.Equal("escape", HotkeyGesture.NormalizeKey(" ESC "));
    }

    [Fact]
    public void HotkeyGestureMatchesPressedKey()
    {
        var save = HotkeyGesture.Parse("mod+s")!;
        Assert.True(save.Matches(true, false, false, "S"));
        Assert.True(save.Matches(true, false, false, "s"));
        Assert.False(save.Matches(false, false, false, "s"));
        Assert.False(save.Matches(true, true, false, "s"));
        Assert.False(save.Matches(true, false, false, "n"));
        Assert.False(save.Matches(true, false, false, null));
    }

    [Fact]
    public void HotkeysStayCompatibleWithComboMatches()
    {
        foreach (var pair in ProgramSettingsCatalog.DefaultHotkeys())
        {
            var gesture = HotkeyGesture.Parse(pair.Value);
            Assert.NotNull(gesture);
            // Разбор понимает все комбинации по умолчанию и согласован с `comboMatches` (этап 4).
            Assert.True(gesture!.Matches(gesture.Ctrl, gesture.Shift, gesture.Alt, gesture.Key), pair.Key);
            Assert.True(ProgramSettingsCatalog.ComboMatches(pair.Value, gesture.Ctrl, gesture.Shift, gesture.Alt, gesture.Key), pair.Key);
        }
    }

    [Fact]
    public void GameSeriesCatalogDescribesXRaySeries()
    {
        Assert.Equal(new[] { "soc", "cs", "cop" }, GameSeriesCatalog.Items.Select(i => i.Value));
        Assert.Equal("soc", GameSeriesCatalog.DefaultSeries);
        Assert.Equal("SoC · Тень Чернобыля", GameSeriesCatalog.Label("soc"));
        Assert.Equal("CS · Чистое Небо", GameSeriesCatalog.Label("cs"));
        Assert.Contains("Зов Припяти", GameSeriesCatalog.Label("cop"));
        Assert.Equal("SoC · Тень Чернобыля", GameSeriesCatalog.Label("nope"));
        Assert.Equal("config", GameSeriesCatalog.ConfigDirectory("soc"));
        Assert.Equal("config", GameSeriesCatalog.ConfigDirectory("cs"));
        Assert.Equal("configs", GameSeriesCatalog.ConfigDirectory("cop"));
        Assert.Equal("config", GameSeriesCatalog.ConfigDirectory(null));
        Assert.Equal("soc", GameSeriesCatalog.Normalize("nope"));
        Assert.Equal("cop", GameSeriesCatalog.Normalize("cop"));
    }

    [Fact]
    public void ProjectSummaryCountsEntitiesAndChips()
    {
        var project = new Project
        {
            ProjectName = "test_project",
            Dialogues = { new DialogueGraph(), new DialogueGraph() },
            Characters = { new Character { Id = "a" } },
            Quests = { new Quest { Id = "q1" }, new Quest { Id = "q2" }, new Quest { Id = "q3" } },
            Scripts = new List<ScriptFile> { new() { Name = "s.script" } },
            Languages =
            {
                new Language { Locale = "ru" }, new Language { Locale = "en" },
                new Language { Locale = "de" }, new Language { Locale = "fr" }
            },
            LtxFiles = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["config/weapons/weapons.ltx"] = "[wpn_ak]\n",
                ["config/items.ltx"] = "[items]\n"
            },
            Textures = new List<ProjectTexture> { new() { Name = "a.dds" } },
            ItemsLtx = "[wpn_pm]\ncost = 10\n\n[wpn_ak]\ncost = 20\n"
        };
        project.ItemFiles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["config/weapons/weapons.ltx"] = "[wpn_ak]\ncost = 21\n\n[wpn_lr300]\ncost = 30\n"
        };

        var summary = ProjectSummary.From(project);
        Assert.Equal(2, summary.Dialogues);
        Assert.Equal(1, summary.Characters);
        Assert.Equal(3, summary.Items); // wpn_pm, wpn_ak, wpn_lr300 — дубликат секции не считается дважды
        Assert.Equal(3, summary.Quests);
        Assert.Equal(1, summary.Scripts);
        Assert.Equal(4, summary.Languages);
        Assert.Equal(2, summary.LtxFiles);
        Assert.Equal(1, summary.Textures);
        Assert.Equal(16, summary.Total);
        Assert.Equal(7, summary.WelcomeCount);

        Assert.Equal(3, summary.Count(NavigationCatalog.ItemsId));
        Assert.Equal(4, summary.Count(NavigationCatalog.LanguagesId));
        Assert.Equal(1, summary.Count(NavigationCatalog.IconsId));
        Assert.Equal(0, summary.Count(NavigationCatalog.OverviewId));

        var chips = summary.Chips();
        Assert.Equal(ProjectSummary.ChipIds.Length, chips.Count);
        Assert.Equal(new[] { "💬", "🧍", "🎒", "🎯", "📜", "🌐", "🧾" }, chips.Select(c => c.Glyph));
        Assert.Equal(
            new[] { "tabDialogues", "tabCharacters", "tabItems", "tabQuests", "tabScripts", "tabLanguages", "tabSystem" },
            chips.Select(c => c.LabelKey));
        Assert.Equal(new[] { 2, 1, 3, 3, 1, 4, 2 }, chips.Select(c => c.Count));
        foreach (var chip in chips)
            Assert.False(string.IsNullOrWhiteSpace(LocalizationService.Tr("ru", chip.LabelKey)));

        Assert.Equal(ProjectSummary.Empty, ProjectSummary.From(null));
        Assert.Equal(0, ProjectSummary.Empty.Total);
        Assert.Equal(0, ProjectSummary.CountItemSections(null));
    }

    [Fact]
    public void HotkeyCatalogResolvesBindingsAndReportsDuplicates()
    {
        var bindings = HotkeyCatalog.Bindings(null);
        var expectedCount = ProgramSettingsCatalog.HotkeyActions
            .Count(action => BuildFeatures.IsDebug || !BuildFeatures.IsDebugOnlyAction(action.Id));
        Assert.Equal(expectedCount, bindings.Count);
        Assert.Equal(BuildFeatures.IsDebug, bindings.Any(b => b.Action.Id == "exportXml"));
        Assert.All(bindings, b => Assert.True(b.IsValid, b.Action.Id));
        Assert.Equal("Ctrl+S", bindings.First(b => b.Action.Id == "save").Display);
        Assert.Equal("Del", bindings.First(b => b.Action.Id == "deleteSelected").Display);
        Assert.Empty(HotkeyCatalog.Duplicates(null));

        var hotkeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["save"] = "mod+s",
            ["exportXml"] = "mod+s",   // конфликт с save
            ["undo"] = "mod+z",
            ["redo"] = "mod+z",        // конфликт с undo
            ["addStart"] = "meta+s",   // неизвестный модификатор — комбинация не разбирается
            ["addPhrase"] = ""         // пустая — берётся значение по умолчанию ("2")
        };

        var duplicates = HotkeyCatalog.Duplicates(hotkeys);
        Assert.Equal("undo", duplicates["redo"]);
        Assert.False(duplicates.ContainsKey("addStart"));

        var resolved = HotkeyCatalog.Resolve(hotkeys);
        Assert.False(resolved.ContainsKey("addStart"));
        Assert.True(resolved.ContainsKey("addPhrase"));
        Assert.Equal("2", resolved["addPhrase"].Key);

        var bindingsWithBroken = HotkeyCatalog.Bindings(hotkeys);
        Assert.False(bindingsWithBroken.First(b => b.Action.Id == "addStart").IsValid);
        Assert.Equal("meta+s", bindingsWithBroken.First(b => b.Action.Id == "addStart").Display);

        // При конфликте побеждает первое действие реестра; `exportXml` здесь перевешен на mod+s.
        Assert.Equal("save", HotkeyCatalog.MatchAction(hotkeys, true, false, false, "S"));
        Assert.Null(HotkeyCatalog.MatchAction(hotkeys, true, false, false, "E"));
        if (BuildFeatures.IsDebug)
        {
            // `exportXml` — debug-only действие: в Debug оно есть в реестре и на Ctrl+E.
            Assert.Equal("save", duplicates["exportXml"]);
            Assert.Equal("exportXml", HotkeyCatalog.MatchAction(null, true, false, false, "E"));
        }
        else
        {
            Assert.False(duplicates.ContainsKey("exportXml"));
            Assert.False(resolved.ContainsKey("exportXml"));
            Assert.Null(HotkeyCatalog.MatchAction(null, true, false, false, "E"));
        }
        // `ComboMatches` (порт App.js) игнорирует незнакомые модификаторы: "meta+s" — это `S` без Ctrl,
        // поэтому в `Resolve`/`Bindings` (строгий разбор) такая комбинация считается битой, а здесь — совпадает.
        Assert.Equal("addStart", HotkeyCatalog.MatchAction(hotkeys, false, false, false, "S"));
        Assert.Null(HotkeyCatalog.MatchAction(null, false, false, false, "S"));
    }

    [Fact]
    public void BuildFeaturesMarkEditorXmlAsDebugOnly()
    {
        Assert.True(BuildFeatures.IsDebugOnlyAction("exportXml"));
        Assert.False(BuildFeatures.IsDebugOnlyAction("save"));
        Assert.False(BuildFeatures.IsDebugOnlyAction("demoGraph"));
        Assert.False(BuildFeatures.IsDebugOnlyAction(null));
    }

    [Fact]
    public void HotkeyCatalogMapsCtrlNumberToTab()
    {
        Assert.Equal("dialogues", HotkeyCatalog.TabForNumber(true, false, false, "1")!.Id);
        Assert.Equal("quests", HotkeyCatalog.TabForNumber(true, false, false, "8")!.Id);
        Assert.Null(HotkeyCatalog.TabForNumber(true, false, false, "0"));
        Assert.Null(HotkeyCatalog.TabForNumber(true, false, false, "q"));
        Assert.Null(HotkeyCatalog.TabForNumber(false, false, false, "1"));
        Assert.Null(HotkeyCatalog.TabForNumber(true, true, false, "1"));
        Assert.Null(HotkeyCatalog.TabForNumber(true, false, false, null));
    }

    [Fact]
    public void DemoGraphSeedBuildsTraderDialogue()
    {
        var (nodes, edges) = DemoGraph.Seed();

        Assert.Equal("trader_demo", DemoGraph.DialogueId);
        Assert.Equal(8, nodes.Count);
        Assert.Equal(8, edges.Count);
        Assert.Equal(new[] { "start", "npc_hi", "player_yes", "player_no", "check_artifact", "reward", "bye", "end" },
            nodes.Select(n => n.Id));
        Assert.Equal(NodeTypes.Start, nodes[0].Type);
        Assert.Equal(NodeTypes.End, nodes[^1].Type);
        Assert.Equal("Ну что, сталкер, принёс артефакт?", nodes[1].Data!.Text);
        Assert.Equal("af_medusa", nodes.Single(n => n.Id == "check_artifact").Data!.Expression);
        Assert.Equal("medkit", nodes.Single(n => n.Id == "reward").Data!.Params);

        // Каждая связь ведёт в существующие узлы (граф валиден — ошибок проверки нет).
        Assert.All(edges, e => Assert.Contains(nodes, n => n.Id == e.Source));
        Assert.All(edges, e => Assert.Contains(nodes, n => n.Id == e.Target));
        Assert.Equal(edges.Count, edges.Select(e => e.Id).Distinct().Count());
        Assert.All(edges, e => Assert.False(e.Broken));
        Assert.Empty(GraphValidator.Validate(nodes, edges));

        var node = DemoGraph.MakeNode(NodeTypes.Phrase, 5, 6);
        Assert.StartsWith("phrase_", node.Id);
        Assert.Equal(5, node.X);
        Assert.Equal(6, node.Y);
        Assert.Equal("manual", node.Data!.RoleSource);
        Assert.NotEqual(DemoGraph.MakeNode(NodeTypes.Phrase, 0, 0).Id, DemoGraph.MakeNode(NodeTypes.Phrase, 0, 0).Id);
    }

    [Fact]
    public void InternalXmlExportsEditorPayloadWithSchema()
    {
        var project = ProjectMigration.Empty();
        project.ProjectName = "demo";
        project.DialogueId = "trader_demo";
        project.Series = "cop";
        project.Nodes.Add(DemoGraph.MakeNode(NodeTypes.Phrase, 10, 20));

        var xml = InternalXml.ToXml(project);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", xml);
        Assert.Contains("<dialogue_editor schema=\"internal-xml-v1\">", xml);
        Assert.Contains("<nodes count=\"1\"", xml);
        Assert.Equal(InternalXml.Schema, InternalXml.PayloadSchema(xml));
        Assert.Equal(InternalXml.RootTag, "dialogue_editor");

        var payload = InternalXml.Payload(xml);
        Assert.NotNull(payload);
        using var document = JsonDocument.Parse(payload!);
        Assert.Equal("trader_demo", document.RootElement.GetProperty("projectName").GetString());
        Assert.Equal("cop", document.RootElement.GetProperty("series").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.Equal(0, document.RootElement.GetProperty("edges").GetArrayLength());

        // Имя берётся из `dialogueId`, иначе — из `projectName`; XML-объекты с payload экранируются.
        var unnamed = ProjectMigration.Empty();
        unnamed.DialogueId = "";
        unnamed.ProjectName = "fallback <name>";
        var unnamedXml = InternalXml.ToXml(unnamed);
        Assert.Contains("nodes count=\"0\"", unnamedXml);
        Assert.Contains("fallback", InternalXml.Payload(unnamedXml)!);
        Assert.Contains("fallback", unnamedXml);
        Assert.DoesNotContain("<name>", unnamedXml);

        Assert.Null(InternalXml.Payload("<dialogue_editor/>"));
        Assert.Null(InternalXml.PayloadSchema("<dialogue_editor/>"));
        Assert.Null(InternalXml.PayloadSchema(null));
        Assert.Contains("new_dialogue", InternalXml.ToXml(null));
    }

    [Fact]
    public void ProjectSearchFindsMatchesInEverySection()
    {
        var project = ProjectMigration.Empty();

        var dialogue = new DialogueGraph { Id = "trader_demo", Name = "Торговец" };
        dialogue.Nodes.Add(DemoGraph.MakeNode(NodeTypes.Phrase, 0, 0));
        dialogue.Nodes[0].Data!.Text = "Ну что, сталкер, принёс артефакт?";
        dialogue.Edges.Add(new DialogueEdge { Id = "e1", Source = dialogue.Nodes[0].Id, Target = dialogue.Nodes[0].Id });
        project.Dialogues.Add(dialogue);

        project.Characters.Add(new Character { Id = "sid", Name = "Сидорович", Community = "trader", Bio = "Торгует у ангара" });
        project.Quests.Add(new Quest
        {
            Id = "quest_sid",
            Title = "Найти декодер",
            Objectives = { new QuestObjective { Text = "Поговорить с Сидоровичем" } }
        });
        project.Languages.Add(new Language
        {
            Locale = "rus",
            Name = "Русский",
            Entries = { new LanguageEntry { Id = "sid_hello", Text = "Привет, сталкер." } }
        });
        project.Scripts = new List<ScriptFile>
        {
            new() { Name = "sid.script", Path = "scripts/sid.script", Text = "function sid() end" }
        };
        project.SystemLtx = "[includes]\n#include \"items.ltx\"\n";
        project.ItemsLtx = "[wpn_ak74]:identity_immunities\ncost = 100\n";
        project.Textures = new List<ProjectTexture>
        {
            new() { Name = "wpn_ak74.dds", Width = 128, Height = 128 }
        };

        // Пустой запрос и `null`-проект результатов не дают.
        Assert.Empty(ProjectSearch.Search(project, ""));
        Assert.Empty(ProjectSearch.Search(project, "   "));
        Assert.Empty(ProjectSearch.Search(null, "сталкер"));
        Assert.Empty(ProjectSearch.Search(project, "сталкер", 0));

        var dialogues = ProjectSearch.Search(project, "сталкер");
        Assert.Contains(dialogues, h => h.TabId == NavigationCatalog.DialoguesId);
        Assert.Contains(dialogues, h => h.TabId == NavigationCatalog.LanguagesId);

        Assert.Contains(ProjectSearch.Search(project, "Сидоров"), h => h.TabId == NavigationCatalog.CharactersId);
        Assert.Contains(ProjectSearch.Search(project, "декодер"), h => h.TabId == NavigationCatalog.QuestsId);
        Assert.Contains(ProjectSearch.Search(project, "сидоровичем"), h => h.Kind == "objective");
        Assert.Contains(ProjectSearch.Search(project, "scripts/sid.script"), h => h.TabId == NavigationCatalog.ScriptsId);

        var items = ProjectSearch.Search(project, "wpn_ak74");
        Assert.Contains(items, h => h.TabId == NavigationCatalog.ItemsId);
        Assert.Contains(items, h => h.TabId == NavigationCatalog.IconsId);
        Assert.Contains(ProjectSearch.Search(project, "includes"), h => h.TabId == NavigationCatalog.SystemId);

        // Подпись и фрагмент записи: строка многоточием не обрезается при малом радиусе.
        var hit = ProjectSearch.Search(project, "принёс").First(h => h.TabId == NavigationCatalog.DialoguesId);
        Assert.Contains("принёс", hit.Snippet);

        Assert.Contains("сталкер", ProjectSearch.Snippet("Ну что,\n  сталкер,   принёс  артефакт?", "сталкер", 5));
        Assert.Equal("", ProjectSearch.Snippet("   \n\t ", "сталкер"));
        Assert.Equal("a b c", ProjectSearch.Collapse(" a \n\t b   c "));
        Assert.Equal("", ProjectSearch.Collapse(null));
        Assert.True(ProjectSearch.Matches("Ну что, сталкер", "СТАЛКЕР"));
        Assert.False(ProjectSearch.Matches("Ну что", "сталкер"));
        Assert.False(ProjectSearch.Matches(null, "сталкер"));
        Assert.False(ProjectSearch.Matches("сталкер", "  "));
    }
}
