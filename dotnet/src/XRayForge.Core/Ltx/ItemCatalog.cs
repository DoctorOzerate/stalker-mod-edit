using System.Text.RegularExpressions;

namespace XRayForge.Core.Ltx;

/// <summary>
/// Справочники предметов LTX — порт `ITEM_CATEGORIES`, `COMMUNITY_ICONS`,
/// `ITEM_COMMON_FIELDS`, `ITEM_FIELD_PROFILES`, `FIELD_DESCRIPTIONS` и функций
/// `classifyItem`, `itemFieldProfile`, `itemFileCategory`, `isItemFile`.
/// </summary>
public static class ItemCatalog
{
    /// <summary>ITEM_CATEGORIES: категории предметов для фильтров и профилей полей.</summary>
    public static readonly List<string> Categories = new()
    {
        "Оружие", "Боеприпасы", "Броня", "Артефакты", "Юзабельные", "Детекторы",
        "Апгрейды", "Квестовое", "Предметы", "Аномалии", "Иммунитеты", "Базовые", "Прочее"
    };

    /// <summary>COMMUNITY_ICONS: эмодзи сообществ и монстров (в WPF нужен fallback-шрифт).</summary>
    public static readonly Dictionary<string, string> CommunityIcons = new(StringComparer.Ordinal)
    {
        ["actor"] = "🎭",
        ["actor_dolg"] = "🛡️",
        ["actor_freedom"] = "🕊️",
        ["stalker"] = "🧍",
        ["bandit"] = "🔪",
        ["military"] = "🎖️",
        ["army"] = "🎖️",
        ["dolg"] = "🛡️",
        ["freedom"] = "🕊️",
        ["monolith"] = "☢️",
        ["ecolog"] = "🔬",
        ["scientist"] = "🔬",
        ["zombied"] = "🧟",
        ["zombie"] = "🧟",
        ["killer"] = "🔫",
        ["stranger"] = "❓",
        ["trader"] = "💰",
        ["arena_enemy"] = "⚔️",
        ["human"] = "🧍",
        ["boar"] = "🐗",
        ["bloodsucker"] = "🩸",
        ["flesh"] = "👹",
        ["dog"] = "🐕",
        ["pseudodog"] = "🐕",
        ["cat"] = "🐈",
        ["chimera"] = "🦁",
        ["giant"] = "👾",
        ["burer"] = "🧌",
        ["controller"] = "🧠",
        ["poltergeist"] = "👻",
        ["snork"] = "🧟",
        ["fracture"] = "🦴",
        ["bird"] = "🐦",
        ["rat"] = "🐀",
        ["tushkano"] = "🐀",
        ["arena_monstr"] = "👾"
    };

    /// <summary>ITEM_COMMON_FIELDS: общие поля, добавляемые к каждому профилю.</summary>
    public static readonly List<string> CommonFields = new()
    {
        "$spawn", "$prefetch", "$npc", "visual", "cform", "cost", "inv_name",
        "inv_name_short", "description", "inv_weight", "inv_grid_width",
        "inv_grid_height", "inv_grid_x", "inv_grid_y", "death_items"
    };

    /// <summary>FIELD_DESCRIPTIONS: подсказки по полям предметов (#5).</summary>
    public static readonly Dictionary<string, string> FieldDescriptions = new(StringComparer.Ordinal)
    {
        ["cost"] = "Цена",
        ["inv_weight"] = "Вес в инвентаре",
        ["inv_name"] = "Название в инвентаре (text_id)",
        ["inv_name_short"] = "Короткое название",
        ["description"] = "Описание (text_id)",
        ["visual"] = "Модель (OGF)",
        ["class"] = "Класс предмета (для движка)",
        ["$spawn"] = "Секция спавна в редакторе уровней",
        ["ammo_class"] = "Используемые патроны (секции)",
        ["ammo_mag_size"] = "Размер магазина",
        ["ammo_limit"] = "Лимит патронов",
        ["ammo_elapsed"] = "Патроны в магазине",
        ["fire_modes"] = "Режимы огня (1/0, -1 — авто)",
        ["fire_distance"] = "Дальность стрельбы",
        ["bullet_speed"] = "Начальная скорость пули",
        ["rpm"] = "Скорострельность (выстр/мин)",
        ["hit_power"] = "Урон",
        ["hit_impulse"] = "Импульс (отдача цели)",
        ["hit_type"] = "Тип ранения",
        ["fire_dispersion_base"] = "Базовое рассеивание",
        ["grenade_class"] = "Подствольные гранаты",
        ["slot"] = "Слот инвентаря",
        ["animation_slot"] = "Тип анимации",
        ["hud"] = "Секция HUD",
        ["position"] = "Позиция в руках",
        ["orientation"] = "Ориентация в руках",
        ["snd_shoot"] = "Звук выстрела",
        ["snd_reload"] = "Звук перезарядки",
        ["snd_draw"] = "Звук доставания",
        ["snd_holster"] = "Звук убирания",
        ["misfire_probability"] = "Вероятность осечки",
        ["condition_shot_dec"] = "Износ за выстрел",
        ["use_aim_bullet"] = "Баллистика по прицелу",
        ["time_to_aim"] = "Время прицеливания",
        ["burn_protection"] = "Защита от ожогов",
        ["strike_protection"] = "Защита от ударов",
        ["shock_protection"] = "Защита от электричества",
        ["wound_protection"] = "Защита от ран",
        ["radiation_protection"] = "Защита от радиации",
        ["telepatic_protection"] = "Защита от пси-воздействия",
        ["chemical_burn_protection"] = "Защита от хим. ожогов",
        ["explosion_protection"] = "Защита от взрывов",
        ["fire_wound_protection"] = "Защита от огнестрела",
        ["immunities_sect"] = "Секция иммунитетов",
        ["actor_visual"] = "Модель на игроке",
        ["full_icon_name"] = "Полная иконка NPC",
        ["power_loss"] = "Потеря энергии",
        ["night_vision_sect"] = "ПНВ",
        ["artefact_count"] = "Слотов под артефакты",
        ["speed_coef"] = "Модификатор скорости",
        ["eat_health"] = "Восстановление здоровья",
        ["eat_power"] = "Восстановление энергии",
        ["eat_satiety"] = "Насыщение",
        ["eat_radiation"] = "Вывод радиации",
        ["eat_alcohol"] = "Опьянение",
        ["eat_max_power"] = "Макс. энергия",
        ["eat_portions_num"] = "Порций",
        ["k_dist"] = "Дистанция (боеприпас)",
        ["k_disp"] = "Рассеивание (боеприпас)",
        ["k_impulse"] = "Импульс (боеприпас)",
        ["k_pierce"] = "Бронепробитие",
        ["k_ap"] = "Пробитие брони",
        ["k_hit"] = "Урон (боеприпас)",
        ["k_bullet_speed"] = "Скорость пули",
        ["inv_grid_x"] = "X иконки в атласе",
        ["inv_grid_y"] = "Y иконки в атласе",
        ["inv_grid_width"] = "Ширина иконки (ячейки)",
        ["inv_grid_height"] = "Высота иконки (ячейки)",
        ["death_items"] = "Предметы, выпадающие при смерти",
        ["scope_name"] = "Секция прицела",
        ["silencer_name"] = "Секция глушителя",
        ["grenade_launcher_name"] = "Секция подствольника",
        ["upgrades"] = "Доступные апгрейды",
        ["installed_upgrades"] = "Установленные апгрейды",
        ["upgrade_scheme"] = "Схема апгрейдов",
        ["prerequisites"] = "Требования апгрейда",
        ["effects"] = "Эффекты апгрейда",
        ["quest_item"] = "Квестовый предмет",
        ["story_id"] = "ID для скриптов",
        ["can_trade"] = "Можно продавать",
        ["buy_condition"] = "Условие покупки",
        ["sell_condition"] = "Условие продажи",
        ["buy_supplies"] = "Условие ассортимента"
    };

    /// <summary>ITEM_FIELD_PROFILES: наборы полей по категориям предметов (#15-20).</summary>
    public static readonly Dictionary<string, List<string>> FieldProfiles = new(StringComparer.Ordinal)
    {
        ["Оружие"] = new()
        {
            "ammo_class", "ammo_mag_size", "ammo_limit", "ammo_elapsed", "fire_modes",
            "fire_distance", "bullet_speed", "rpm", "rpm_empty_click", "hit_power",
            "hit_impulse", "hit_type", "fire_dispersion_base", "grenade_class",
            "launch_speed", "scope_status", "scope_name", "scope_zoom_factor",
            "silencer_status", "silencer_name", "grenade_launcher_status",
            "grenade_launcher_name", "zoom_enabled", "weapon_class", "slot",
            "animation_slot", "hand_dependence", "single_handed", "upgrades",
            "installed_upgrades", "upgrade_scheme", "hud", "position", "orientation",
            "snd_shoot", "snd_empty", "snd_reload", "snd_draw", "snd_holster",
            "fire_point", "shell_point", "flame_particles", "smoke_particles",
            "misfire_probability", "condition_shot_dec", "use_aim_bullet", "time_to_aim",
            "kill_msg_x", "kill_msg_y", "kill_msg_width", "kill_msg_height", "cost", "inv_weight"
        },
        ["Боеприпасы"] = new()
        {
            "k_dist", "k_disp", "k_impulse", "k_pierce", "k_ap", "k_hit", "k_air_resistance",
            "k_bullet_speed", "k_cam_dispersion", "impair", "buck_shot", "tracers",
            "box_size", "inv_weight"
        },
        ["Броня"] = new()
        {
            "burn_protection", "strike_protection", "shock_protection", "wound_protection",
            "radiation_protection", "telepatic_protection", "chemical_burn_protection",
            "explosion_protection", "fire_wound_protection", "additional_inventory_weight",
            "additional_inventory_weight2", "ef_equipment_type", "bones_koeff_protection",
            "immunities_sect", "actor_visual", "full_icon_name", "slot", "power_loss",
            "night_vision_sect", "artefact_count", "speed_coef", "upgrades", "upgrade_scheme",
            "cost", "inv_weight"
        },
        ["Артефакты"] = new()
        {
            "health_restore_speed", "radiation_restore_speed", "satiety_restore_speed",
            "power_restore_speed", "bleeding_restore_speed", "additional_inventory_weight",
            "hit_absorbation", "cost"
        },
        ["Юзабельные"] = new()
        {
            "eat_health", "eat_power", "eat_satiety", "eat_radiation", "eat_alcohol",
            "eat_max_power", "eat_portions_num", "cost", "inv_weight"
        },
        ["Квестовое"] = new() { "quest_item", "story_id", "can_trade", "cost", "inv_weight" },
        ["Апгрейды"] = new()
        {
            "cost", "hit_power", "bullet_speed", "rpm", "hit_impulse", "ammo_mag_size",
            "ammo_limit", "scheme_index", "parent", "condition", "prerequisites", "effects",
            "icon", "name"
        },
        ["Детекторы"] = new()
        {
            "detect_actor", "detect_artefacts", "hud", "af_sound", "af_rank", "cost"
        },
        ["Торговля"] = new()
        {
            "buy_condition", "sell_condition", "buy_supplies", "buy_price", "sell_price"
        }
    };

    /// <summary>
    /// classifyItem: категория предмета по полю `class` (авторитетный признак) с фолбэком
    /// на имя секции; порядок проверок повторяет JS-оригинал.
    /// </summary>
    public static string ClassifyItem(LtxSection? section)
    {
        var name = (section?.Name ?? "").ToLowerInvariant();
        var entries = section?.Entries ?? new List<LtxEntry>();
        var cls = (entries.FirstOrDefault(e => e.Key == "class")?.Value ?? "").ToLowerInvariant();

        // 1. секции иммунитетов (на них ссылаются броней через immunities_sect)
        if (Regex.IsMatch(name, "^sect_.*immunit")) return "Иммунитеты";
        // 2. базовые/служебные секции по имени
        if (Regex.IsMatch(name, "_base$|_manager$|_table$|immunities$|_properties$|_relations$|_damages$")) return "Базовые";
        // 3. по классу (авторитетно)
        if (Regex.IsMatch(cls, "^wp_scope$|^w_silenc|^w_glaunc|^ii_attch")) return "Апгрейды";
        if (Regex.IsMatch(cls, "binoc")) return "Детекторы";
        if (Regex.IsMatch(cls, "^wp_|^w_")) return "Оружие";
        if (Regex.IsMatch(cls, "^ammo$|^a_")) return "Боеприпасы";
        if (Regex.IsMatch(cls, "^g_")) return "Боеприпасы";
        if (Regex.IsMatch(cls, "artefact|scrptart")) return "Артефакты";
        if (Regex.IsMatch(cls, "^e_|equ_|outfit")) return "Броня";
        if (Regex.IsMatch(cls, "^d_|torch")) return "Детекторы";
        if (Regex.IsMatch(cls, "ii_food|ii_medki|ii_antir|ii_bandg|ii_bottl|ii_bolt")) return "Юзабельные";
        if (Regex.IsMatch(cls, "^z_|^zs_")) return "Аномалии";
        if (Regex.IsMatch(cls, "^o_")) return "Предметы";
        // 3b. флаг quest_item — квестовые предметы (#27)
        var questEntry = entries.FirstOrDefault(e => e.Key == "quest_item");
        if (questEntry != null && Regex.IsMatch(questEntry.Value.Trim(), "^true|yes|1$", RegexOptions.IgnoreCase))
            return "Квестовое";
        // 4. по имени (фолбэк)
        if (Regex.IsMatch(name, "af_|artefact|artifact")) return "Артефакты";
        if (Regex.IsMatch(name, "ammo|grenade")) return "Боеприпасы";
        if (Regex.IsMatch(name, "wpn_|^w_|weapon")) return "Оружие";
        if (Regex.IsMatch(name, "outfit|helm|suit|armor|specops")) return "Броня";
        if (Regex.IsMatch(name, "medkit|bandage|food|vodka|antirad|drink|bread|kolbasa|conserva|energy_drink")) return "Юзабельные";
        if (Regex.IsMatch(name, "detector|device|_pda|torch|binoc")) return "Детекторы";
        if (Regex.IsMatch(name, "^upgr_|upgrade|silencer|scope")) return "Апгрейды";
        if (Regex.IsMatch(name, "quest|document")) return "Квестовое";
        if (Regex.IsMatch(name, "zone|anomal")) return "Аномалии";
        if (Regex.IsMatch(name, "item|misc")) return "Предметы";
        // 5. корневые секции-базы без своих предметных свойств
        var parents = section?.Parents ?? new List<string>();
        var hasOwnProps = entries.Any(e => e.Key == "inv_name" || e.Key == "class" || e.Key == "cost");
        if (parents.Count == 0 && !hasOwnProps && cls.Length == 0) return "Базовые";
        return "Прочее";
    }

    /// <summary>itemFieldProfile: профиль полей секции (по категории; trade_* — торговля).</summary>
    public static List<string> ItemFieldProfile(LtxSection? section)
    {
        var name = (section?.Name ?? "").ToLowerInvariant();
        var prof = name.StartsWith("trade", StringComparison.Ordinal) || name.Contains("_trade")
            ? FieldProfiles["Торговля"]
            : FieldProfiles.TryGetValue(ClassifyItem(section), out var found) ? found : new List<string>();

        var result = new List<string>(CommonFields);
        result.AddRange(prof);
        return result;
    }

    /// <summary>itemFileCategory: категория файла предметов по его имени (иначе null).</summary>
    public static string? ItemFileCategory(string? name)
    {
        var baseName = ItemFileBaseName(name);
        if (Regex.IsMatch(baseName, "weapon|wpn")) return "Оружие";
        if (Regex.IsMatch(baseName, "ammo|grenade")) return "Боеприпасы";
        if (Regex.IsMatch(baseName, "outfit|armor|helm|suit")) return "Броня";
        if (Regex.IsMatch(baseName, "artefact|artifact")) return "Артефакты";
        if (Regex.IsMatch(baseName, "detector")) return "Детекторы";
        if (Regex.IsMatch(baseName, "quest")) return "Квестовое";
        if (Regex.IsMatch(baseName, "food|drug|med|vodka|drink")) return "Юзабельные";
        return null;
    }

    /// <summary>isItemFile: похоже ли имя файла на файл предметов.</summary>
    public static bool IsItemFile(string? name) => Regex.IsMatch(
        ItemFileBaseName(name),
        "item|weapon|wpn|ammo|grenade|outfit|armor|helm|suit|artefact|artifact|detector|quest|food|drug|med|vodka|drink");

    /// <summary>Базовое имя файла без `.ltx` и пути (как в JS: `split('/').pop()`).</summary>
    private static string ItemFileBaseName(string? name) =>
        Regex.Replace((name ?? "").ToLowerInvariant(), @"\.ltx$", "").Split('/').Last();
}