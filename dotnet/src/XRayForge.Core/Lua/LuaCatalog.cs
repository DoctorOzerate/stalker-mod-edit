namespace XRayForge.Core.Lua;

/// <summary>Классы токенов подсветки Lua (значения совпадают с cls из lua.js).</summary>
public static class LuaTokenClasses
{
    public const string Comment = "comment";
    public const string StringLiteral = "string";
    public const string Number = "number";
    public const string Keyword = "keyword";
    public const string Ident = "ident";
    public const string Bracket = "bracket";
    public const string Plain = "plain";
}

/// <summary>Справочники Lua для редактора: ключевые слова, глобальные имена движка, API и сниппеты.</summary>
public static class LuaCatalog
{
    public static readonly HashSet<string> Keywords = new()
    {
        "function", "end", "if", "then", "else", "elseif", "for", "while", "do",
        "return", "local", "nil", "true", "false", "and", "or", "not", "in",
        "break", "repeat", "until"
    };

    /// <summary>Известные глобальные имена движка X-Ray (не считаем ошибкой).</summary>
    public static readonly string[] EngineGlobals =
    {
        "_G", "self", "db", "alife", "level", "game", "device", "profiler", "fs",
        "math", "string", "table", "os", "io", "coroutine", "tonumber", "tostring",
        "pairs", "ipairs", "type", "pcall", "xpcall", "print", "require", "unpack",
        "select", "setmetatable", "getmetatable", "rawget", "rawset", "next",
        "assert", "error", "loadstring", "loadfile", "dofile", "collectgarbage",
        "news_manager", "xr_effects", "xr_sound", "dialogs", "actor_menu_inventory",
        "utils", "bit", "ini_file", "time_global", "class_registrator", "game_registrator",
        "ui_registrator", "state_mgr", "xr_logic", "xr_conditions", "xr_evaluators_id",
        "xr_actions_id", "treasure_manager", "trade_manager", "death_manager", "task_manager"
    };

    /// <summary>Частые функции API движка для автодополнения (#27, #49).</summary>
    public static readonly string[] Api =
    {
        "db.actor", "db.actor:give_item", "db.actor:take_item", "db.actor:give_money",
        "db.actor:give_info_portion", "db.actor:disable_info_portion", "db.actor:has_info",
        "db.actor:alive", "db.actor:position", "db.actor:set_character_rank", "db.actor:change_reputation",
        "db.actor:level_vertex_id", "db.actor:game_vertex_id", "db.actor:iterate_inventory", "db.actor:active_item",
        "db.actor:object", "db.actor:is_actor_outdoors", "db.storage", "db.server",
        "alife():create", "alife():release", "alife():object", "alife():level_name", "alife():switch_distance",
        "level.add_cam_effector", "level.remove_cam_effector", "level.get_game_time",
        "level.vertex_position", "level.game_vertex_id", "level.set_weather", "level.set_time_factor",
        "level.spawn_item", "level.spawn_phantom", "level.add_pp_effector",
        "game.translate_string", "game.get_game_time", "game.start_tutorial", "game.get_env_value",
        "news_manager.send_tip", "news_manager.send_tip_nosound",
        "xr_effects.set_sound_volume", "xr_sound.set_sound_volume",
        "dialogs.relocate_wounded", "dialogs.start", "dialogs.break_dialog",
        "actor_menu_inventory.show", "utils.cfg", "utils.xr_ini", "device().set_internal_event",
        "level.patrol", "level.add_death_callback", "level.add_call"
    };

    /// <summary>Сниппеты, специфичные для моддинга STALKER (#32, #50).</summary>
    public static readonly LuaSnippet[] Snippets =
    {
        new() { Label = "has_info", Text = "if db.actor:has_info(\"%+quest_done%\") then\n\t\nend" },
        new() { Label = "give_item", Text = "db.actor:give_item(\"medkit\")" },
        new() { Label = "give_money", Text = "db.actor:give_money(5000)" },
        new() { Label = "info", Text = "db.actor:give_info_portion(\"quest_given\")" },
        new() { Label = "news tip", Text = "news_manager.send_tip(db.actor, \"tip_id\", nil, nil, 5000)" },
        new() { Label = "alife", Text = "alife():create(\"object\", db.actor:position(), db.actor:level_vertex_id(), db.actor:game_vertex_id())" },
        new() { Label = "death cb", Text = "level.add_death_callback(character_id, function(victim, who)\n\t\nend)" },
        new() { Label = "logic", Text = "xr_logic.pstor_retrieve(obj, section)" },
        new() { Label = "translate", Text = "game.translate_string(\"text_id\")" },
        new() { Label = "weather", Text = "level.set_weather(\"weather_id\", true)" }
    };

    public static bool IsKeyword(string? word) => word != null && Keywords.Contains(word);
}