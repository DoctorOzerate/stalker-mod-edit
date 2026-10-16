using XRayForge.Core.Ltx;

namespace XRayForge.Tests;

/// <summary>Порт `test/ltx.test.mjs` (24 теста) в xUnit.</summary>
public class LtxTests
{
    private static LtxSection Sec(
        string name, string? cls = null, string[]? parents = null, (string Key, string Value)[]? entries = null)
    {
        var list = entries != null
            ? entries.Select(e => new LtxEntry { Key = e.Key, Value = e.Value }).ToList()
            : cls != null
                ? new List<LtxEntry> { new() { Key = "class", Value = cls } }
                : new List<LtxEntry>();

        return new LtxSection
        {
            Name = name,
            Parents = parents?.ToList() ?? new List<string>(),
            Entries = list
        };
    }

    [Fact]
    public void ValidateRefs_flags_missing_ammo_class_but_not_known_parent()
    {
        const string text = "[wpn_ak74]:wpn_base\nammo_class = ammo_9x18_fmj, ammo_missing\n";
        var issues = LtxInheritance.ValidateRefs(text, new[] { "wpn_base", "ammo_9x18_fmj" });

        Assert.Contains(issues, i => i.Code == "ltx_ref_value" && i.Message.Contains("ammo_missing"));
        Assert.DoesNotContain(issues, i => i.Code == "ltx_ref_parent");
    }

    [Fact]
    public void ValidateRefs_flags_missing_parent()
    {
        var issues = LtxInheritance.ValidateRefs("[child]:ghost_parent\n", Array.Empty<string>());

        Assert.Contains(issues, i => i.Code == "ltx_ref_parent" && i.Message.Contains("ghost_parent"));
    }

    [Fact]
    public void FindDuplicateSections_lists_multi_file_sections()
    {
        var map = new Dictionary<string, List<string>>
        {
            ["shared"] = new() { "a.ltx", "b.ltx" },
            ["single"] = new() { "a.ltx" }
        };

        var dups = LtxInheritance.FindDuplicateSections(map);

        Assert.Single(dups);
        Assert.Equal("shared", dups[0].Name);
    }

    [Fact]
    public void FindInheritanceCycles_detects_a_b_a()
    {
        var issues = LtxInheritance.FindInheritanceCycles("[a]:b\n[b]:a\n");

        Assert.NotEmpty(issues);
        Assert.Equal("ltx_inherit_cycle", issues[0].Code);
    }

    [Fact]
    public void RenameSection_keeps_parents()
    {
        Assert.Equal("[new_sec]:base\nx = 1\n",
            LtxTextTools.RenameSection("[old]:base\nx = 1\n", "old", "new_sec"));
    }

    [Fact]
    public void ReplaceFieldValues_mass_replaces_value_substring()
    {
        Assert.Equal("[a]\ncost = 1X\n[b]\ncost = 2X\n",
            LtxTextTools.ReplaceFieldValues("[a]\ncost = 100\n[b]\ncost = 200\n", "cost", "00", "X"));
    }

    [Fact]
    public void ItemFieldProfile_maps_category_to_fields()
    {
        Assert.Contains("ammo_class", ItemCatalog.ItemFieldProfile(Sec("wpn_ak74")));
        Assert.Contains("hit_power", ItemCatalog.ItemFieldProfile(Sec("wpn_ak74")));
        Assert.Contains("snd_shoot", ItemCatalog.ItemFieldProfile(Sec("wpn_ak74")));
        Assert.Contains("animation_slot", ItemCatalog.ItemFieldProfile(Sec("wpn_ak74")));
        Assert.Contains("upgrade_scheme", ItemCatalog.ItemFieldProfile(Sec("wpn_ak74")));
        Assert.Contains("k_dist", ItemCatalog.ItemFieldProfile(Sec("ammo_9x18_fmj")));
        Assert.Contains("burn_protection", ItemCatalog.ItemFieldProfile(Sec("novice_outfit")));
        Assert.Contains("actor_visual", ItemCatalog.ItemFieldProfile(Sec("novice_outfit")));
        Assert.Contains("buy_condition", ItemCatalog.ItemFieldProfile(Sec("trade_barman")));
        // только общие поля — профиля категории нет
        Assert.DoesNotContain("ammo_class", ItemCatalog.ItemFieldProfile(Sec("misc_item")));
        Assert.True(ItemCatalog.FieldProfiles.Count >= 6);
        Assert.Contains("eat_satiety",
            ItemCatalog.ItemFieldProfile(Sec("bread", null, null, new[] { ("class", "II_FOOD") })));
        Assert.Contains("quest_item",
            ItemCatalog.ItemFieldProfile(Sec("jup_a9_conservation_info", null, null, new[] { ("quest_item", "true") })));
    }

    [Fact]
    public void ClassifyItem_uses_class_field_and_fallback()
    {
        Assert.Equal("Оружие", ItemCatalog.ClassifyItem(Sec("wpn_ak74", "WP_AK74")));
        Assert.Equal("Юзабельные", ItemCatalog.ClassifyItem(Sec("bread", "II_FOOD")));
        Assert.Equal("Боеприпасы", ItemCatalog.ClassifyItem(Sec("ammo_9x18_fmj", "AMMO")));
        Assert.Equal("Артефакты", ItemCatalog.ClassifyItem(Sec("af_medusa", "ARTEFACT")));
        Assert.Equal("Апгрейды", ItemCatalog.ClassifyItem(Sec("wpn_addon_scope", "WP_SCOPE")));
        Assert.Equal("Аномалии", ItemCatalog.ClassifyItem(Sec("zone_radio", "Z_RADIO")));
        Assert.Equal("Детекторы", ItemCatalog.ClassifyItem(Sec("device_pda", "D_PDA")));
        Assert.Equal("Броня", ItemCatalog.ClassifyItem(Sec("novice_outfit", "E_STLK")));
        Assert.Equal("Детекторы", ItemCatalog.ClassifyItem(Sec("wpn_binoc", "WP_BINOC")));
        Assert.Equal("Оружие", ItemCatalog.ClassifyItem(Sec("wpn_abakan", "")));
    }

    [Fact]
    public void ClassifyItem_separates_immunities_and_base_sections()
    {
        Assert.Equal("Иммунитеты", ItemCatalog.ClassifyItem(
            Sec("sect_novice_outfit_immunities", "", Array.Empty<string>(), new[] { ("burn_immunity", "0.04") })));
        Assert.Equal("Базовые", ItemCatalog.ClassifyItem(
            Sec("outfit_base", "", Array.Empty<string>(), new[] { ("additional_inventory_weight", "0") })));
        Assert.Equal("Базовые", ItemCatalog.ClassifyItem(
            Sec("identity_immunities", "", Array.Empty<string>(), Array.Empty<(string, string)>())));
        Assert.Equal("Боеприпасы", ItemCatalog.ClassifyItem(Sec("ammo_9x18_fmj", "AMMO")));
        Assert.Equal("Оружие", ItemCatalog.ClassifyItem(Sec("wpn_ak74", "WP_AK74")));
    }

    [Fact]
    public void CommunityIcons_covers_real_communities()
    {
        string[] communities =
        {
            "actor", "actor_dolg", "actor_freedom", "stalker", "monolith", "military",
            "killer", "ecolog", "dolg", "freedom", "bandit", "zombied", "stranger",
            "trader", "arena_enemy"
        };

        foreach (var c in communities)
            Assert.True(ItemCatalog.CommunityIcons.ContainsKey(c), "missing " + c);
    }

    [Fact]
    public void ParseGameRelations_reads_communities_and_relation_matrix()
    {
        const string text = "[communities]\ncommunities = actor, 0, stalker, 5, bandit, 12\n" +
                            "[communities_relations]\nactor = 0, -5000\nstalker = -5000, 5000\n";

        var r = LtxDomainParsers.ParseGameRelations(text);

        Assert.Equal(new[] { "actor", "stalker", "bandit" }, r.Communities);
        Assert.Equal(2, r.Relations["actor"].Count);
        Assert.Equal("5000", r.Relations["stalker"][1]);
    }

    [Fact]
    public void ParseRatingScales_reads_rating_reputation_scales()
    {
        const string text = "[game_relations]\nrating = novice, 300, experienced, 600\n" +
                            "reputation = neutral, 50, good, 150\ngoodwill_names = enemy, -400, friendly\n";

        var r = LtxDomainParsers.ParseRatingScales(text);

        Assert.Equal(2, r.Rating.Count);
        Assert.Equal("novice", r.Rating[0].Name);
        Assert.Equal("300", r.Rating[0].Value);
        Assert.Equal("neutral", r.Reputation[0].Name);
        Assert.Single(r.Goodwill);
    }

    [Fact]
    public void ParseSquadDescr_reads_squads()
    {
        const string text = "[zat_b38_bloodsucker_1_lair]:online_offline_group\n" +
                            "faction = monster_predatory_night\nnpc = zat_b38_bloodsucker_1\n" +
                            "target_smart = zat_b38\nstory_id = zat_b38_bloodsucker_1_lair\n";

        var s = LtxDomainParsers.ParseSquadDescr(text);

        Assert.Single(s);
        Assert.Equal("monster_predatory_night", s[0].Faction);
        Assert.Equal("zat_b38_bloodsucker_1", s[0].Npc);
    }

    [Fact]
    public void ItemFieldProfile_includes_common_fields()
    {
        var prof = ItemCatalog.ItemFieldProfile(Sec("wpn_ak74"));

        Assert.Contains("$spawn", prof);
        Assert.Contains("inv_grid_x", prof);
        Assert.Contains("ammo_class", prof);
    }

    [Fact]
    public void ClassifyItem_detects_quest_item_flag()
    {
        Assert.Equal("Квестовое", ItemCatalog.ClassifyItem(
            Sec("jup_a9_conservation_info", null, new[] { "device_pda" }, new[] { ("quest_item", "true") })));
        Assert.Equal("Детекторы", ItemCatalog.ClassifyItem(
            Sec("detector_simple", null, null, new[] { ("quest_item", "false") })));
    }

    [Fact]
    public void ParseSecrets_reads_lists_and_treasures()
    {
        const string text = "[list]:list_zaton, list_pripyat\n[pri_b36_tower_treasure]\n" +
                            "medkit_army = 3, 1\nammo_gauss = 1, 1\n";

        var r = LtxDomainParsers.ParseSecrets(text);

        Assert.Equal(new[] { "list_zaton", "list_pripyat" }, r.Lists);
        Assert.Single(r.Treasures);
        Assert.Equal("medkit_army", r.Treasures[0].Items[0].Item);
    }

    [Fact]
    public void ParseSpawnSections_reads_spawn_sections()
    {
        const string text = "[zat_b14_stalker_1]:stalker\n$spawn = \"respawn\\zat_b14_stalker_1\"\n" +
                            "character_profile = zat_b14_stalker_1\nspec_rank = regular\n" +
                            "community = stalker\nstory_id = zat_b14_stalker_1\n";

        var r = LtxDomainParsers.ParseSpawnSections(text);

        Assert.Single(r);
        Assert.Equal("zat_b14_stalker_1", r[0].Id);
        Assert.Equal("stalker", r[0].Base);
        Assert.Equal("zat_b14_stalker_1", r[0].CharacterProfile);
        Assert.Equal("regular", r[0].SpecRank);
    }

    [Fact]
    public void ParseMonsters_reads_SM_class_sections()
    {
        const string text = "[m_bloodsucker_e]:monster_base\nclass = SM_BLOOD\n" +
                            "visual = monsters\\krovosos\\krovosos\nscript_binding = bind_monster.bind\n" +
                            "[some_item]\nclass = II_FOOD\n";

        var r = LtxDomainParsers.ParseMonsters(text);

        Assert.Single(r);
        Assert.Equal("m_bloodsucker_e", r[0].Id);
        Assert.Equal("SM_BLOOD", r[0].Class);
        Assert.Equal("bind_monster.bind", r[0].ScriptBinding);
    }

    [Fact]
    public void ParseActor_reads_actor_section()
    {
        const string text = "[actor]:common_ph\nclass = S_ACTOR\nmax_item_mass = 35\n" +
                            "jump_speed = 6.\nmoney = 15000\n";

        var r = LtxDomainParsers.ParseActor(text);

        Assert.NotNull(r);
        Assert.Equal("S_ACTOR", r!.Class);
        Assert.Equal("35", r.MaxItemMass);
        Assert.Equal("15000", r.Money);
        Assert.Equal(4, r.Entries.Count);
        Assert.Null(LtxDomainParsers.ParseActor("[other]\nx = 1\n"));
    }

    [Fact]
    public void ParseSurgeManager_reads_settings_and_list()
    {
        const string text = "[settings]\ncondlist = {-pri_a16} false, true\n" +
                            "survive = {+actor_marked} true, false\n[list]\n" +
                            "zat_surge_hide_a2\nzat_surge_hide_b42\n";

        var r = LtxDomainParsers.ParseSurgeManager(text);

        Assert.Contains("pri_a16", r.Settings["condlist"]);
        Assert.Equal(new[] { "zat_surge_hide_a2", "zat_surge_hide_b42" }, r.List);
    }

    [Fact]
    public void ParseTrade_reads_traders_and_supplies()
    {
        const string text = "[trader]\nbuy_condition = barman_generic_buy\nsell_condition = barman_start_sell\n" +
                            "[barman_generic_buy]\naf_medusa = 1, 0.3\nammo_9x18_fmj = 0.9, 0.1\n";

        var r = LtxDomainParsers.ParseTrade(text);

        Assert.Single(r.Traders);
        Assert.Equal("barman_generic_buy", r.Traders[0].BuyCondition);
        Assert.Single(r.Supplies);
        Assert.Equal("af_medusa", r.Supplies[0].Items[0].Item);
    }

    [Fact]
    public void Highlight_colors_sections_keys_comments()
    {
        var html = LtxHighlight.ToHtml("[wpn_ak74]:base\n; comment\ncost = 2000 ; price\n");

        Assert.Contains("#a78bfa", html); // секция
        Assert.Contains("#fbbf24", html); // ключ
        Assert.Contains("#6b7280", html); // комментарий
    }

    [Fact]
    public void InheritanceTree_builds_parent_children_graph()
    {
        var tree = LtxInheritance.InheritanceTree("[weapon_base]\n[wpn_ak74]:weapon_base\n[wpn_ak74u]:wpn_ak74\n");

        Assert.Equal(new[] { "weapon_base" }, tree.Roots);
        Assert.Contains("wpn_ak74", tree.Children["weapon_base"]);
        Assert.Contains("wpn_ak74u", tree.Children["wpn_ak74"]);
    }

    [Fact]
    public void FieldDescriptions_covers_common_fields()
    {
        Assert.Equal("Цена", ItemCatalog.FieldDescriptions["cost"]);
        Assert.Equal("Урон", ItemCatalog.FieldDescriptions["hit_power"]);
        Assert.Equal("Защита от ожогов", ItemCatalog.FieldDescriptions["burn_protection"]);
    }
}