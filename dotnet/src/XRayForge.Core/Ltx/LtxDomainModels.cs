namespace XRayForge.Core.Ltx;

/// <summary>Пара «имя — значение» из шкал рангов/репутации.</summary>
public sealed class ScaleMark
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Шкалы game_relations.ltx: ранги, репутация, доброжелательность (#19).</summary>
public sealed class RatingScales
{
    public List<ScaleMark> Rating { get; set; } = new();
    public List<ScaleMark> Reputation { get; set; } = new();
    public List<ScaleMark> Goodwill { get; set; } = new();
}

/// <summary>Отряд из squad_descr.ltx (#78).</summary>
public sealed class SquadDesc
{
    public string Id { get; set; } = "";
    public List<string> Parents { get; set; } = new();
    public string Faction { get; set; } = "";
    public string Npc { get; set; } = "";
    public string NpcRandom { get; set; } = "";
    public string NpcInSquad { get; set; } = "";
    public string TargetSmart { get; set; } = "";
    public string SpawnPoint { get; set; } = "";
    public string StoryId { get; set; } = "";
    public string OnDeath { get; set; } = "";
    public string Sympathy { get; set; } = "";
    public string Relationship { get; set; } = "";
}

/// <summary>Содержимое тайника (секция трежеров secrets.ltx).</summary>
public sealed class SecretTreasure
{
    public string Id { get; set; } = "";
    public List<string> Parents { get; set; } = new();
    public List<TradeItem> Items { get; set; } = new();
}

/// <summary>Тайники secrets.ltx: списки из секции [list] и содержимое (#79).</summary>
public sealed class SecretsInfo
{
    public List<string> Lists { get; set; } = new();
    public List<SecretTreasure> Treasures { get; set; } = new();
}

/// <summary>Спавн-секция spawn_sections.ltx (#14).</summary>
public sealed class SpawnSectionInfo
{
    public string Id { get; set; } = "";
    public string Base { get; set; } = "";
    public string CharacterProfile { get; set; } = "";
    public string Community { get; set; } = "";
    public string SpecRank { get; set; } = "";
    public string StoryId { get; set; } = "";
    public string Spawn { get; set; } = "";
    public string Npc { get; set; } = "";
    public string Logic { get; set; } = "";
}

/// <summary>Монстр из monsters.ltx / M_*.ltx: секции с `class = SM_*` (#25).</summary>
public sealed class MonsterInfo
{
    public string Id { get; set; } = "";
    public string Base { get; set; } = "";
    public string Class { get; set; } = "";
    public string Visual { get; set; } = "";
    public string Icon { get; set; } = "";
    public string ScriptBinding { get; set; } = "";
    public string Health { get; set; } = "";
}

/// <summary>Пара «ключ — значение» для полного списка полей актора.</summary>
public sealed class LtxKeyValue
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Секция [actor] из actor.ltx (#26); null, если секции нет.</summary>
public sealed class ActorInfo
{
    public string MaxItemMass { get; set; } = "";
    public string JumpSpeed { get; set; } = "";
    public string RunCoef { get; set; } = "";
    public string SprintKoef { get; set; } = "";
    public string Money { get; set; } = "";
    public string Rank { get; set; } = "";
    public string Visual { get; set; } = "";
    public string Class { get; set; } = "";
    public string ScriptBinding { get; set; } = "";
    public List<LtxKeyValue> Entries { get; set; } = new();
}

/// <summary>Настройки и список выбросов surge_manager.ltx (#80).</summary>
public sealed class SurgeManagerInfo
{
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.Ordinal);
    public List<string> List { get; set; } = new();
}

/// <summary>Торговец trade_*.ltx (#38).</summary>
public sealed class TradeTrader
{
    public string Id { get; set; } = "";
    public string BuyCondition { get; set; } = "";
    public string SellCondition { get; set; } = "";
    public string BuySupplies { get; set; } = "";
}

/// <summary>Позиция ассортимента/снабжения торговца.</summary>
public sealed class TradeItem
{
    public string Item { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>Секция-ассортимент trade_*.ltx.</summary>
public sealed class TradeSupply
{
    public string Id { get; set; } = "";
    public List<TradeItem> Items { get; set; } = new();
}

/// <summary>Итог разбора торговли: торговцы и их ассортимент (#38).</summary>
public sealed class TradeInfo
{
    public List<TradeTrader> Traders { get; set; } = new();
    public List<TradeSupply> Supplies { get; set; } = new();
}

/// <summary>Сообщества и матрица отношений из game_relations.ltx (#9).</summary>
public sealed class GameRelationsInfo
{
    public List<string> Communities { get; set; } = new();
    public Dictionary<string, List<string>> Relations { get; set; } = new(StringComparer.Ordinal);
    public List<string> MonsterCommunities { get; set; } = new();
}