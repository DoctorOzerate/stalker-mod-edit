using System.Text.RegularExpressions;

namespace XRayForge.Core.Ltx;

/// <summary>
/// Доменные парсеры LTX-файлов игры — порт `src/lib/ltx.js`: parseRatingScales,
/// parseSquadDescr, parseSecrets, parseSpawnSections, parseMonsters, parseActor.
/// </summary>
public static class LtxDomainParsers
{
    /// <summary>Значение поля по ключу (первое совпадение) или пустая строка.</summary>
    private static string Get(LtxSection sec, string key) =>
        sec.Entries.FirstOrDefault(e => e.Key == key)?.Value ?? "";

    /// <summary>parseRatingScales: шкалы rating / reputation / goodwill_names из game_relations (#19).</summary>
    public static RatingScales ParseRatingScales(string? text)
    {
        var result = new RatingScales();
        var sec = LtxParser.Parse(text).Sections.FirstOrDefault(s => s.Name == "game_relations");
        if (sec == null) return result;

        List<ScaleMark> Pairs(string key)
        {
            var value = sec.Entries.FirstOrDefault(x => x.Key == key)?.Value;
            if (value == null) return new List<ScaleMark>();

            var parts = value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var list = new List<ScaleMark>();
            for (var i = 0; i + 1 < parts.Count; i += 2)
                list.Add(new ScaleMark { Name = parts[i], Value = parts[i + 1] });
            return list;
        }

        result.Rating = Pairs("rating");
        result.Reputation = Pairs("reputation");
        result.Goodwill = Pairs("goodwill_names");
        return result;
    }

    /// <summary>parseSquadDescr: отряды squad_descr.ltx, кроме служебной online_offline_group (#78).</summary>
    public static List<SquadDesc> ParseSquadDescr(string? text) =>
        LtxParser.Parse(text).Sections
            .Where(s => s.Name.Length > 0 && s.Name.ToLowerInvariant() != "online_offline_group")
            .Select(s => new SquadDesc
            {
                Id = s.Name,
                Parents = s.Parents,
                Faction = Get(s, "faction"),
                Npc = Get(s, "npc"),
                NpcRandom = Get(s, "npc_random"),
                NpcInSquad = Get(s, "npc_in_squad"),
                TargetSmart = Get(s, "target_smart"),
                SpawnPoint = Get(s, "spawn_point"),
                StoryId = Get(s, "story_id"),
                OnDeath = Get(s, "on_death"),
                Sympathy = Get(s, "sympathy"),
                Relationship = Get(s, "relationship")
            })
            .ToList();

    /// <summary>parseSecrets: тайники secrets.ltx — списки из [list] и содержимое (#79).</summary>
    public static SecretsInfo ParseSecrets(string? text)
    {
        var parsed = LtxParser.Parse(text);
        var listSec = parsed.Sections.FirstOrDefault(s => s.Name.ToLowerInvariant() == "list");

        return new SecretsInfo
        {
            Lists = listSec != null ? listSec.Parents : new List<string>(),
            Treasures = parsed.Sections
                .Where(s => s.Name.ToLowerInvariant() != "list")
                .Select(s => new SecretTreasure
                {
                    Id = s.Name,
                    Parents = s.Parents,
                    Items = s.Entries.Select(e => new TradeItem { Item = e.Key, Value = e.Value }).ToList()
                })
                .ToList()
        };
    }

    /// <summary>parseSpawnSections: спавн-секции spawn_sections.ltx (все секции файла) (#14).</summary>
    public static List<SpawnSectionInfo> ParseSpawnSections(string? text) =>
        LtxParser.Parse(text).Sections.Select(s => new SpawnSectionInfo
        {
            Id = s.Name,
            Base = s.Parents.Count > 0 ? s.Parents[0] : "",
            CharacterProfile = Get(s, "character_profile"),
            Community = Get(s, "community"),
            SpecRank = Get(s, "spec_rank"),
            StoryId = Get(s, "story_id"),
            Spawn = Get(s, "$spawn"),
            Npc = Get(s, "npc"),
            Logic = Get(s, "logic")
        }).ToList();

    /// <summary>parseMonsters: секции монстров с `class = SM_*` (#25).</summary>
    public static List<MonsterInfo> ParseMonsters(string? text) =>
        LtxParser.Parse(text).Sections
            .Where(s => s.Entries.Any(e => e.Key == "class" && e.Value.Trim().StartsWith("SM_", StringComparison.Ordinal)))
            .Select(s => new MonsterInfo
            {
                Id = s.Name,
                Base = s.Parents.Count > 0 ? s.Parents[0] : "",
                Class = Get(s, "class"),
                Visual = Get(s, "visual"),
                Icon = Get(s, "icon"),
                ScriptBinding = Get(s, "script_binding"),
                Health = Get(s, "MaxHealthValue")
            })
            .ToList();

    /// <summary>parseActor: секция [actor] из actor.ltx (#26); null, если секции нет.</summary>
    public static ActorInfo? ParseActor(string? text)
    {
        var sec = LtxParser.Parse(text).Sections
            .FirstOrDefault(s => s.Name.ToLowerInvariant() == "actor");
        if (sec == null) return null;

        return new ActorInfo
        {
            MaxItemMass = Get(sec, "max_item_mass"),
            JumpSpeed = Get(sec, "jump_speed"),
            RunCoef = Get(sec, "run_coef"),
            SprintKoef = Get(sec, "sprint_koef"),
            Money = Get(sec, "money"),
            Rank = Get(sec, "rank"),
            Visual = Get(sec, "visual"),
            Class = Get(sec, "class"),
            ScriptBinding = Get(sec, "script_binding"),
            Entries = sec.Entries.Select(e => new LtxKeyValue { Key = e.Key, Value = e.Value }).ToList()
        };
    }

    /// <summary>parseSurgeManager: настройки [settings] и список выбросов [list] (#80).</summary>
    public static SurgeManagerInfo ParseSurgeManager(string? text)
    {
        var result = new SurgeManagerInfo();
        foreach (var sec in LtxParser.Parse(text).Sections)
        {
            var name = sec.Name.ToLowerInvariant();
            if (name == "settings")
            {
                foreach (var e in sec.Entries) result.Settings[e.Key] = e.Value;
            }
            else if (name == "list")
            {
                foreach (var line in sec.RawLines)
                {
                    var v = Regex.Replace(line, ";.*$", "").Trim();
                    if (v.Length > 0 && v[0] != '[' && v[0] != '#') result.List.Add(v);
                }
            }
        }
        return result;
    }

    /// <summary>parseTrade: торговцы (buy/sell_condition, buy_supplies) и их ассортимент (#38).</summary>
    public static TradeInfo ParseTrade(string? text)
    {
        var result = new TradeInfo();
        foreach (var s in LtxParser.Parse(text).Sections)
        {
            var buyCondition = Get(s, "buy_condition");
            var sellCondition = Get(s, "sell_condition");
            var buySupplies = Get(s, "buy_supplies");

            if (buyCondition.Length > 0 || sellCondition.Length > 0 || buySupplies.Length > 0)
            {
                result.Traders.Add(new TradeTrader
                {
                    Id = s.Name,
                    BuyCondition = buyCondition,
                    SellCondition = sellCondition,
                    BuySupplies = buySupplies
                });
            }
            else if (s.Entries.Count > 0)
            {
                result.Supplies.Add(new TradeSupply
                {
                    Id = s.Name,
                    Items = s.Entries.Select(e => new TradeItem { Item = e.Key, Value = e.Value }).ToList()
                });
            }
        }
        return result;
    }

    /// <summary>parseGameRelations: сообщества, матрица отношений и монстры-сообщества (#9).</summary>
    public static GameRelationsInfo ParseGameRelations(string? text)
    {
        var result = new GameRelationsInfo();
        foreach (var sec in LtxParser.Parse(text).Sections)
        {
            if (sec.Name == "communities")
            {
                var e = sec.Entries.FirstOrDefault(x => x.Key == "communities");
                if (e != null)
                {
                    var parts = e.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                    for (var i = 0; i < parts.Count; i += 2) result.Communities.Add(parts[i]);
                }
            }
            else if (sec.Name == "communities_relations")
            {
                foreach (var e in sec.Entries)
                    result.Relations[e.Key] = e.Value.Split(',').Select(x => x.Trim()).ToList();
            }
            else if (sec.Name == "monster_communities")
            {
                var e = sec.Entries.FirstOrDefault(x => x.Key == "communities");
                if (e != null)
                {
                    var parts = e.Value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                    for (var i = 0; i < parts.Count; i += 2) result.MonsterCommunities.Add(parts[i]);
                }
            }
        }

        return result;
    }
}