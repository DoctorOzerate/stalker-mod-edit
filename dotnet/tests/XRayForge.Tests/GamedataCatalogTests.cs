using XRayForge.Core.Gamedata;
using XRayForge.Core.Projects;
using XRayForge.Core.Suggestions;

namespace XRayForge.Tests;

/// <summary>
/// Каталог gamedata (`catalog.csv`): разбор CSV, фильтрация по серии и подсказки для LTX/Lua/XML.
/// </summary>
public class GamedataCatalogTests
{
    private const string Header =
        "\uFEFF№;Проект;Глава;Группа;Путь;Имя файла;Расширение;Тип;Размер (байт);Размер;Строк;" +
        "Описание;Ключевые элементы;Аналог в другом проекте;MD5\n";

    private static GamedataCatalog Catalog() => GamedataCatalog.LoadFromCsv(
        Header +
        "1;CoP;CoP — конфигурация;Оружие;CoP/gamedata/configs/weapons/w_abakan.ltx;" +
        "w_abakan.ltx;.ltx;LTX;100;100 Б;10;Оружие;[wpn_abakan], [wpn_abakan_up];" +
        "SoC/gamedata/config/weapons/w_abakan.ltx;md5-1\n" +
        "2;CoP;CoP — конфигурация;Корень;CoP/gamedata/configs/game.ltx;game.ltx;.ltx;LTX;" +
        "200;200 Б;7;\"Описание; с точкой с запятой\";" +
        "#include: game_maps_single.ltx, game_maps_mp.ltx (+1);;md5-2\n" +
        "3;CoP;CoP — скрипты Lua;Условия;CoP/gamedata/scripts/custom_conditions.script;" +
        "custom_conditions.script;.script;Lua;300;300 Б;40;Условия;is_wounded, is_alive (+3);;md5-3\n" +
        "4;CoP;CoP — локализация;Строки;CoP/gamedata/configs/text/rus/st_foo.xml;" +
        "st_foo.xml;.xml;XML (строки);400;400 Б;12;Строки;" +
        "41 строк: st_foo_name, st_foo_descr;;md5-4\n" +
        "5;CoP;CoP — конфигурация;Копии;CoP/gamedata/configs/weapons/w_abakan~.ltx;" +
        "w_abakan~.ltx;.ltx;LTX (копия);100;100 Б;10;Копия;[wpn_copy];;md5-5\n" +
        "6;SoC;SoC — конфигурация;Корень;SoC/gamedata/config/alife.ltx;alife.ltx;.ltx;LTX;" +
        "50;50 Б;5;A-Life;[alife];;md5-6\n");

    [Fact]
    public void LoadFromCsv_parses_header_quoted_cells_and_file_metadata()
    {
        var catalog = Catalog();

        Assert.Equal(6, catalog.Entries.Count);

        var abakan = catalog.Entries.First(entry => entry.FileName == "w_abakan.ltx");
        Assert.Equal("cop", abakan.Series);
        Assert.Equal(".ltx", abakan.Extension);
        Assert.True(abakan.IsLtx);
        Assert.Equal(10, abakan.Lines);
        Assert.Equal("100 Б", abakan.SizeLabel);
        Assert.Equal("[wpn_abakan], [wpn_abakan_up]", abakan.KeyElements);
        Assert.Equal("SoC/gamedata/config/weapons/w_abakan.ltx", abakan.AnalogPath);
        Assert.Equal("md5-1", abakan.Md5);

        var game = catalog.Entries.First(entry => entry.FileName == "game.ltx");
        Assert.Equal("Описание; с точкой с запятой", game.Description);

        Assert.Empty(GamedataCatalog.LoadFromCsv(null).Entries);
        Assert.Empty(GamedataCatalog.LoadFromCsv(Header).Entries);
        Assert.Empty(GamedataCatalog.LoadFromCsv("без заголовка;совсем\n1;2\n").Entries);
    }

    [Fact]
    public void ForSeries_filters_soc_and_cop_but_keeps_both_for_cs_and_unknown()
    {
        var catalog = Catalog();

        Assert.Equal(new[] { "w_abakan.ltx", "game.ltx", "custom_conditions.script",
            "st_foo.xml", "w_abakan~.ltx" },
            catalog.ForSeries("CoP").Select(entry => entry.FileName));
        Assert.Equal(new[] { "alife.ltx" },
            catalog.ForSeries("soc").Select(entry => entry.FileName));
        Assert.Equal(6, catalog.ForSeries("cs").Count);
        Assert.Equal(6, catalog.ForSeries("").Count);
        Assert.Equal(5, catalog.ForSeries("Зов Припяти").Count);
    }

    [Fact]
    public void NormalizeSeries_accepts_keys_and_game_titles()
    {
        Assert.Equal("soc", GamedataCatalog.NormalizeSeries("SoC"));
        Assert.Equal("cop", GamedataCatalog.NormalizeSeries(" CoP "));
        Assert.Equal("cs", GamedataCatalog.NormalizeSeries("cs"));
        Assert.Equal("cop", GamedataCatalog.NormalizeSeries("Зов Припяти"));
        Assert.Equal("soc", GamedataCatalog.NormalizeSeries("Тень Чернобыля"));
        Assert.Equal("cs", GamedataCatalog.NormalizeSeries("Чистое Небо"));
        Assert.Equal("unknown", GamedataCatalog.NormalizeSeries("unknown"));
    }


    [Fact]
    public void Suggestions_cover_sections_includes_scripts_functions_and_text_ids()
    {
        var suggestions = Catalog().Suggestions("cop");

        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.Section && entry.Value == "wpn_abakan" &&
            entry.Detail == "w_abakan.ltx");
        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.Section && entry.Value == "wpn_abakan_up");
        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.Include && entry.Value == "weapons\\w_abakan.ltx");
        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.Include && entry.Value == "game_maps_single.ltx");

        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.Script && entry.Value == "custom_conditions");
        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.ScriptFunction && entry.Value == "is_wounded");
        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.ScriptBinding &&
            entry.Value == "custom_conditions.is_wounded");

        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.TextId && entry.Value == "st_foo_name");
        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.TextId && entry.Value == "st_foo_descr");
        Assert.DoesNotContain(suggestions, entry => entry.Value.Contains("строк:", StringComparison.Ordinal));

        Assert.DoesNotContain(suggestions, entry => entry.Value == "wpn_copy");
        Assert.DoesNotContain(suggestions, entry => entry.Value == "alife");
        Assert.DoesNotContain(suggestions, entry => entry.Value == "функций: 3");
    }

    [Fact]
    public void Suggestions_for_soc_use_soc_paths_and_avoid_cop_entries()
    {
        var suggestions = Catalog().Suggestions("soc");

        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.Section && entry.Value == "alife");
        Assert.Contains(suggestions, entry =>
            entry.Kind == SuggestionKind.Include && entry.Value == "alife.ltx");
        Assert.DoesNotContain(suggestions, entry => entry.Value == "wpn_abakan");
        Assert.DoesNotContain(suggestions, entry => entry.Value == "st_foo_name");
    }

    [Fact]
    public void IncludeSuggestions_return_only_include_paths()
    {
        var catalog = Catalog();
        var includes = catalog.IncludeSuggestions("cop");

        Assert.NotEmpty(includes);
        Assert.All(includes, entry => Assert.Equal(SuggestionKind.Include, entry.Kind));
        Assert.Contains(includes, entry => entry.Value == "weapons\\w_abakan.ltx");
        Assert.Contains(includes, entry => entry.Value == "game_maps_mp.ltx");
        Assert.DoesNotContain(includes, entry => entry.Value == "wpn_abakan");
        Assert.Same(includes, catalog.IncludeSuggestions("cop"));
    }

    [Fact]
    public void Embedded_catalog_is_available_for_both_series()
    {
        var catalog = GamedataCatalog.Default;

        Assert.True(catalog.Entries.Count >= 3000, $"entries={catalog.Entries.Count}");
        Assert.True(catalog.ForSeries("cop").Count > 0, "cop entries are empty");
        Assert.True(catalog.ForSeries("soc").Count > 0, "soc entries are empty");

        var cop = catalog.Suggestions("cop");
        Assert.Contains(cop, entry => entry.Kind == SuggestionKind.Script);
        Assert.Contains(cop, entry => entry.Kind == SuggestionKind.TextId);

        var soc = catalog.Suggestions("soc");
        Assert.Contains(soc, entry => entry.Kind == SuggestionKind.Script);

        var includes = catalog.IncludeSuggestions("cop");
        Assert.NotEmpty(includes);
        Assert.All(includes, entry =>
            Assert.True(entry.Value.EndsWith(".ltx", StringComparison.OrdinalIgnoreCase),
                entry.Value));
        Assert.Contains(includes, entry => entry.Value == "weapons\\w_abakan.ltx");
    }

    [Fact]
    public void SuggestionLibrary_adds_catalog_after_project_values_without_duplicates()
    {
        var project = ProjectMigration.Empty();
        project.Series = "cop";
        project.ItemsLtx = "[wpn_abakan]\nclass = WP_ABAKAN\n";

        var library = SuggestionLibrary.Build(project, catalog: Catalog());

        Assert.Single(library.OfKind(SuggestionKind.Item), entry => entry.Value == "wpn_abakan");
        Assert.Contains(library.OfKind(SuggestionKind.Script), entry => entry.Value == "custom_conditions");
        Assert.Contains(library.OfKind(SuggestionKind.TextId), entry => entry.Value == "st_foo_name");
        Assert.Contains(library.ForInclude(), entry => entry.Value == "game_maps_single.ltx");
        Assert.DoesNotContain(library.OfKind(SuggestionKind.Section), entry => entry.Value == "alife");
    }
}

