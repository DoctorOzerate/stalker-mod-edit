// Работа с LTX и предметами: парсинг, редактирование, инклюды, категории.
// Зависимости: normalizePath из core.js; локальные NL/Q/BS и escapeXml.

import { normalizePath } from './core.js';

const NL = '\n';
const Q = '"';
const BS = '\\';
function escapeXml(v) { return String(v == null ? '' : v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;'); }

export function splitLines(text) {
  return String(text || '').split(NL).map(function (l) { return l.replace(String.fromCharCode(13), ''); });
}

export function parseLtx(text) {
  const lines = splitLines(text);
  const sections = [];
  const includes = [];
  let current = null;
  lines.forEach(function (raw, idx) {
    const lineNo = idx + 1;
    const trimmed = raw.trim();
    const code = trimmed.split(';')[0].trim();
    if (!code) { if (current) current.rawLines.push(raw); return; }
    if (code.toLowerCase().indexOf('#include') === 0 && code.indexOf(Q) !== -1) {
      const a = code.indexOf(Q); const b = code.lastIndexOf(Q);
      if (a !== b) includes.push({ path: code.slice(a + 1, b), line: lineNo, raw: raw });
      return;
    }
    if (/^\[[^\]]+\]\s*(?::.*)?$/.test(code)) {
      var hm = code.match(/^\[([^\]]+)\]\s*(?::\s*(.*))?$/);
      current = { name: hm[1].trim(), parents: (hm[2]||'').split(',').map(function(x){return x.trim();}).filter(Boolean), startLine: lineNo, entries: [], rawLines: [raw] };
      sections.push(current);
      return;
    }
    const eq = code.indexOf('=');
    if (eq > 0) {
      const key = code.slice(0, eq).trim();
      const value = code.slice(eq + 1).trim();
      if (key && key.indexOf(' ') === -1) {
        const entry = { key: key, value: value, line: lineNo };
        if (current) { current.entries.push(entry); current.rawLines.push(raw); }
        else {
          let orphan = sections.find(function (s) { return s.name === ''; });
          if (!orphan) { orphan = { name: '', startLine: 0, entries: [], rawLines: [] }; sections.unshift(orphan); }
          orphan.entries.push(entry);
        }
        return;
      }
    }
    if (current) current.rawLines.push(raw);
  });
  return { sections: sections, includes: includes };
}

export function resolveIncludes(text, files, maxDepth) {
  const results = [];
  const visited = new Set();
  const depthMax = maxDepth || 20;
  function walk(t, depth) {
    if (depth > depthMax) return;
    const parsed = parseLtx(t);
    parsed.includes.forEach(function (inc) {
      const norm = normalizePath(inc.path);
      const found = Object.prototype.hasOwnProperty.call(files, norm);
      results.push({ path: norm, found: found, content: found ? files[norm] : '', line: inc.line, cyclic: visited.has(norm) });
      if (visited.has(norm)) return;
      visited.add(norm);
      if (found) walk(files[norm], depth + 1);
    });
  }
  walk(text, 0);
  return results;
}

export function updateLtxLine(text, lineNo, newLine) {
  const lines = splitLines(text);
  if (lineNo >= 1 && lineNo <= lines.length) {
    lines[lineNo - 1] = newLine;
    return lines.join(NL);
  }
  return text;
}

export function removeSection(text, sectionName) {
  const lines = splitLines(text);
  const parsed = parseLtx(text);
  const sec = parsed.sections.find(function (s) { return s.name.toLowerCase() === String(sectionName).toLowerCase(); });
  if (!sec) return text;
  const next = parsed.sections.find(function (s) { return s.startLine > sec.startLine; });
  const endLine = next ? next.startLine - 1 : lines.length;
  let start = sec.startLine;
  while (start > 1 && lines[start - 2].trim() === '') start--;
  lines.splice(start - 1, endLine - start + 1);
  return lines.join(NL);
}

export function addSection(text, name) {
  const block = '[' + name + ']' + NL + 'inv_name = ' + name + NL;
  return (String(text || '').trim() ? String(text).replace(new RegExp(BS + 's+$'), '') + NL + NL : '') + block;
}

export function addFieldToSection(text, sectionName, key, value) {
  const lines = splitLines(text);
  const parsed = parseLtx(text);
  const sec = parsed.sections.find(function (s) { return s.name.toLowerCase() === String(sectionName).toLowerCase(); });
  if (!sec) return text;
  lines.splice(sec.startLine, 0, key + ' = ' + value);
  return lines.join(NL);
}

export function removeFieldByLine(text, lineNo) {
  const lines = splitLines(text);
  if (lineNo >= 1 && lineNo <= lines.length) {
    lines.splice(lineNo - 1, 1);
    return lines.join(NL);
  }
  return text;
}

export function isValidGridValue(v) { return /^\d+$/.test(String(v).trim()); }
export function isValidGridWH(v) { return /^[1-9]\d*$/.test(String(v).trim()); }
export function iconCoords(gx, gy, cellSize) { return { x: (+gx) * cellSize, y: (+gy) * cellSize }; }
export const ATLAS_PRESETS = [
  { label: '50×50 · 5 колонок', columns: 5, cellW: 50, cellH: 50 },
  { label: '50×50 · 10 колонок', columns: 10, cellW: 50, cellH: 50 },
  { label: '32×32 · 8 колонок', columns: 8, cellW: 32, cellH: 32 },
  { label: '64×64 · 4 колонки', columns: 4, cellW: 64, cellH: 64 }
];

export function isFileLoaded(includePath, ltxFiles) {
  const norm = normalizePath(includePath).toLowerCase();
  if (!norm) return false;
  const keys = Object.keys(ltxFiles || {});
  if (keys.some(function (k) { return k.toLowerCase() === norm; })) return true;
  const base = norm.split('/').pop();
  return keys.some(function (k) { return k.toLowerCase() === base || k.toLowerCase().slice(-(base.length + 1)) === '/' + base; });
}
export function findIncludeSuggestion(includePath, ltxFiles) {
  const base = normalizePath(includePath).toLowerCase().split('/').pop();
  const keys = Object.keys(ltxFiles || {});
  let best = null;
  keys.forEach(function (k) {
    const kb = k.toLowerCase().replace(/\\/g, '/').split('/').pop();
    if (kb === base) best = k;
  });
  return best;
}

export function itemFileCategory(name) {
  const base = String(name || '').toLowerCase().replace(/\.ltx$/i, '').split('/').pop();
  if (/weapon|wpn/.test(base)) return 'Оружие';
  if (/ammo|grenade/.test(base)) return 'Боеприпасы';
  if (/outfit|armor|helm|suit/.test(base)) return 'Броня';
  if (/artefact|artifact/.test(base)) return 'Артефакты';
  if (/detector/.test(base)) return 'Детекторы';
  if (/quest/.test(base)) return 'Квестовое';
  if (/food|drug|med|vodka|drink/.test(base)) return 'Юзабельные';
  return null;
}
export function isItemFile(name) {
  const base = String(name || '').toLowerCase().replace(/\.ltx$/i, '').split('/').pop();
  return /item|weapon|wpn|ammo|grenade|outfit|armor|helm|suit|artefact|artifact|detector|quest|food|drug|med|vodka|drink/.test(base);
}

export function mergedItemEntries(sec, sectionsMap) {
  const out = new Map();
  const visited = new Set();
  function walk(name, inherited, source) {
    if (!name) return;
    const key = String(name).toLowerCase();
    if (visited.has(key)) return;
    const s = sectionsMap[key];
    if (!s) return;
    visited.add(key);
    (s.parents || []).forEach(function (p) { walk(p, true, p); });
    (s.entries || []).forEach(function (e) { out.set(e.key, { key: e.key, value: e.value, inherited: inherited, from: inherited ? source : null, line: e.line }); });
  }
  walk(sec && sec.name, false, null);
  return Array.from(out.values());
}
export function includeTreeRows(text, files, depth, visited, out) {
  if (!text || depth > 6) return;
  parseLtx(text).includes.forEach(function (inc) {
    const norm = normalizePath(inc.path);
    const key = Object.keys(files || {}).find(function (k) { return k.toLowerCase() === norm.toLowerCase() || k.toLowerCase().endsWith('/' + norm.toLowerCase()); });
    const found = key != null;
    out.push({ path: norm, depth: depth, cyclic: visited.has(norm), found: found });
    if (found && !visited.has(norm)) {
      const v2 = new Set(visited); v2.add(norm);
      includeTreeRows(files[key], files, depth + 1, v2, out);
    }
  });
}
export function includeGraphSvg(text, files) {
  const rows = [];
  includeTreeRows(text, files || {}, 0, new Set(), rows);
  if (!rows.length) return '';
  const W = 190, H = 36, pad = 24;
  const maxDepth = rows.reduce(function (m, r) { return Math.max(m, r.depth); }, 0);
  const width = (maxDepth + 2) * W;
  const height = (rows.length + 1) * H + pad;
  const nodeY = function (i) { return (i + 1) * H + pad; };
  const L = [];
  L.push('<svg xmlns="http://www.w3.org/2000/svg" width="' + width + '" height="' + height + '" viewBox="0 0 ' + width + ' ' + height + '">');
  L.push('<rect x="0" y="' + nodeY(-1) + '" width="' + (W - 14) + '" height="' + (H - 10) + '" rx="6" fill="#18181b" stroke="#52525b"/>');
  L.push('<text x="8" y="' + (nodeY(-1) + 20) + '" fill="#e4e4e7" font-size="11" font-weight="bold">system.ltx</text>');
  rows.forEach(function (r, i) {
    const x = (r.depth + 1) * W;
    const y = nodeY(i);
    const color = r.cyclic ? '#7f1d1d' : (r.found ? '#1e293b' : '#78350f');
    L.push('<rect x="' + x + '" y="' + y + '" width="' + (W - 14) + '" height="' + (H - 10) + '" rx="6" fill="' + color + '" stroke="#52525b"/>');
    L.push('<text x="' + (x + 8) + '" y="' + (y + 20) + '" fill="#e4e4e7" font-size="11">' + escapeXml(r.path.split('/').pop()) + (r.found ? '' : ' ✗') + '</text>');
  });
  rows.forEach(function (r, i) {
    let parent = null;
    for (let j = i - 1; j >= 0; j--) { if (rows[j].depth === r.depth - 1) { parent = j; break; } }
    const px = parent == null ? 0 : (rows[parent].depth + 1) * W;
    const py = parent == null ? nodeY(-1) : nodeY(parent);
    const x1 = px + (W - 14), y1 = py + (H - 10) / 2;
    const x2 = (r.depth + 1) * W, y2 = nodeY(i) + (H - 10) / 2;
    const mx = (x1 + x2) / 2;
    L.push('<path d="M ' + x1 + ' ' + y1 + ' C ' + mx + ' ' + y1 + ', ' + mx + ' ' + y2 + ', ' + x2 + ' ' + y2 + '" fill="none" stroke="#52525b" stroke-width="1.5"/>');
  });
  L.push('</svg>');
  return L.join('\n');
}

export const ITEM_CATEGORIES = ['Оружие', 'Боеприпасы', 'Броня', 'Артефакты', 'Юзабельные', 'Детекторы', 'Апгрейды', 'Квестовое', 'Предметы', 'Аномалии', 'Иммунитеты', 'Базовые', 'Прочее'];
export const COMMUNITY_ICONS = { actor: '🎭', actor_dolg: '🛡️', actor_freedom: '🕊️', stalker: '🧍', bandit: '🔪', military: '🎖️', army: '🎖️', dolg: '🛡️', freedom: '🕊️', monolith: '☢️', ecolog: '🔬', scientist: '🔬', zombied: '🧟', zombie: '🧟', killer: '🔫', stranger: '❓', trader: '💰', arena_enemy: '⚔️', human: '🧍', boar: '🐗', bloodsucker: '🩸', flesh: '👹', dog: '🐕', pseudodog: '🐕', cat: '🐈', chimera: '🦁', giant: '👾', burer: '🧌', controller: '🧠', poltergeist: '👻', snork: '🧟', fracture: '🦴', bird: '🐦', rat: '🐀', tushkano: '🐀', arena_monstr: '👾' };
// Классификация по полю `class` (авторитетный признак в items.ltx/weapons.ltx и т.д.) с фолбэком на имя секции.
export function classifyItem(section) {
  const name = String((section && section.name) || '').toLowerCase();
  const entries = (section && section.entries) || [];
  const clsEntry = entries.find(function (e) { return e.key === 'class'; });
  const cls = clsEntry ? String(clsEntry.value).toLowerCase() : '';
  // 1. секции иммунитетов (на них ссылаются броней через immunities_sect)
  if (/^sect_.*immunit/.test(name)) return 'Иммунитеты';
  // 2. базовые/служебные секции по имени
  if (/_base$|_manager$|_table$|immunities$|_properties$|_relations$|_damages$/.test(name)) return 'Базовые';
  // 3. по классу (авторитетно)
  if (/^wp_scope$|^w_silenc|^w_glaunc|^ii_attch/.test(cls)) return 'Апгрейды';
  if (/binoc/.test(cls)) return 'Детекторы';
  if (/^wp_|^w_/.test(cls)) return 'Оружие';
  if (/^ammo$|^a_/.test(cls)) return 'Боеприпасы';
  if (/^g_/.test(cls)) return 'Боеприпасы';
  if (/artefact|scrptart/.test(cls)) return 'Артефакты';
  if (/^e_|equ_|outfit/.test(cls)) return 'Броня';
  if (/^d_|torch/.test(cls)) return 'Детекторы';
  if (/ii_food|ii_medki|ii_antir|ii_bandg|ii_bottl|ii_bolt/.test(cls)) return 'Юзабельные';
  if (/^z_|^zs_/.test(cls)) return 'Аномалии';
  if (/^o_/.test(cls)) return 'Предметы';
  // 3b. флаг quest_item — квестовые предметы (#27)
  const questEntry = entries.find(function (e) { return e.key === 'quest_item'; });
  if (questEntry && /^true|yes|1$/i.test(String(questEntry.value).trim())) return 'Квестовое';
  // 4. по имени (фолбэк)
  if (/af_|artefact|artifact/.test(name)) return 'Артефакты';
  if (/ammo|grenade/.test(name)) return 'Боеприпасы';
  if (/wpn_|^w_|weapon/.test(name)) return 'Оружие';
  if (/outfit|helm|suit|armor|specops/.test(name)) return 'Броня';
  if (/medkit|bandage|food|vodka|antirad|drink|bread|kolbasa|conserva|energy_drink/.test(name)) return 'Юзабельные';
  if (/detector|device|_pda|torch|binoc/.test(name)) return 'Детекторы';
  if (/^upgr_|upgrade|silencer|scope/.test(name)) return 'Апгрейды';
  if (/quest|document/.test(name)) return 'Квестовое';
  if (/zone|anomal/.test(name)) return 'Аномалии';
  if (/item|misc/.test(name)) return 'Предметы';
  // 5. корневые секции-базы без своих предметных свойств
  const parents = (section && section.parents) || [];
  const hasOwnProps = entries.some(function (e) { return e.key === 'inv_name' || e.key === 'class' || e.key === 'cost'; });
  if (parents.length === 0 && !hasOwnProps && !cls) return 'Базовые';
  return 'Прочее';
}

// === Проверка ссылок LTX и дубликатов (#19, #34) ===

// knownSections — Set строк (нижний регистр) всех известных секций либо объект-карта {name:[файлы]}.
export function validateLtxRefs(text, knownSections) {
  const issues = [];
  const known = knownSections instanceof Set
    ? knownSections
    : new Set((knownSections || []).map(function (s) { return String(s).toLowerCase(); }));
  parseLtx(text).sections.forEach(function (sec) {
    (sec.parents || []).forEach(function (p) {
      if (p && !known.has(p.toLowerCase())) issues.push({ severity: 'warning', code: 'ltx_ref_parent', section: sec.name, msg: '[' + sec.name + ']: родитель не найден — ' + p });
    });
    (sec.entries || []).forEach(function (en) {
      if (en.key === 'ammo_class' || en.key === 'grenade_class' || en.key === 'ammo_box_class' || en.key === 'scope_name') {
        String(en.value || '').split(',').forEach(function (v) {
          const vv = v.trim();
          if (vv && !known.has(vv.toLowerCase())) issues.push({ severity: 'warning', code: 'ltx_ref_value', section: sec.name, msg: '[' + sec.name + ']: ' + en.key + ' не найден — ' + vv });
        });
      }
    });
  });
  return issues;
}

// Сводка дубликатов секций по карте {name: [файлы]}.
export function findDuplicateSections(sectionMap) {
  const out = [];
  Object.keys(sectionMap || {}).forEach(function (name) {
    const files = sectionMap[name] || [];
    if (files.length > 1) out.push({ name: name, files: files });
  });
  return out.sort(function (a, b) { return b.files.length - a.files.length || a.name.localeCompare(b.name); });
}

// === Проверка циклов наследования LTX (#33) и массовое редактирование (#21) ===

export function findInheritanceCycles(text) {
  const sections = parseLtx(text).sections;
  const parents = {};
  sections.forEach(function (s) { parents[s.name.toLowerCase()] = (s.parents || []).map(function (p) { return p.toLowerCase(); }); });
  const issues = [];
  sections.forEach(function (s) {
    const key = s.name.toLowerCase();
    const chain = [key];
    const seen = new Set([key]);
    let cur = key, guard = 0;
    while (parents[cur] && parents[cur].length && guard++ < 200) {
      const p = parents[cur][0];
      if (seen.has(p)) { issues.push({ severity: 'error', code: 'ltx_inherit_cycle', section: s.name, msg: 'Цикл наследования: ' + chain.concat([p]).join(' → ') }); break; }
      seen.add(p); chain.push(p); cur = p;
    }
  });
  return issues;
}

// === Шкалы рангов/репутации (#19) ===
export function parseRatingScales(text) {
  const parsed = parseLtx(text);
  const sec = parsed.sections.find(function (s) { return s.name === 'game_relations'; });
  if (!sec) return { rating: [], reputation: [], goodwill: [] };
  const pairs = function (key) {
    const e = sec.entries.find(function (x) { return x.key === key; });
    if (!e) return [];
    const parts = e.value.split(',').map(function (x) { return x.trim(); }).filter(Boolean);
    const out = [];
    for (let i = 0; i + 1 < parts.length; i += 2) out.push({ name: parts[i], value: parts[i + 1] });
    return out;
  };
  return { rating: pairs('rating'), reputation: pairs('reputation'), goodwill: pairs('goodwill_names') };
}

// === Отряды squad_descr.ltx (#78) ===
export function parseSquadDescr(text) {
  const parsed = parseLtx(text);
  return parsed.sections.filter(function (s) { return s.name && s.name.toLowerCase() !== 'online_offline_group'; }).map(function (s) {
    const get = function (key) { const e = s.entries.find(function (x) { return x.key === key; }); return e ? e.value : ''; };
    return {
      id: s.name,
      parents: s.parents,
      faction: get('faction'),
      npc: get('npc'),
      npc_random: get('npc_random'),
      npc_in_squad: get('npc_in_squad'),
      target_smart: get('target_smart'),
      spawn_point: get('spawn_point'),
      story_id: get('story_id'),
      on_death: get('on_death'),
      sympathy: get('sympathy'),
      relationship: get('relationship')
    };
  });
}

// === Общие поля предметов (#28): добавляются к каждому профилю ===
export const ITEM_COMMON_FIELDS = ['$spawn', '$prefetch', '$npc', 'visual', 'cform', 'cost', 'inv_name', 'inv_name_short', 'description', 'inv_weight', 'inv_grid_width', 'inv_grid_height', 'inv_grid_x', 'inv_grid_y', 'death_items'];

// === Выбросы surge_manager.ltx (#80) ===
export function parseSurgeManager(text) {
  const parsed = parseLtx(text);
  const settings = {};
  const list = [];
  parsed.sections.forEach(function (sec) {
    const n = sec.name.toLowerCase();
    if (n === 'settings') sec.entries.forEach(function (e) { settings[e.key] = e.value; });
    else if (n === 'list') (sec.rawLines || []).forEach(function (l) { const v = l.replace(/;.*$/, '').trim(); if (v && v.charAt(0) !== '[' && v.charAt(0) !== '#') list.push(v); });
  });
  return { settings: settings, list: list };
}

// === Торговля trade_*.ltx (#38) ===
export function parseTrade(text) {
  const parsed = parseLtx(text);
  const traders = [];
  const supplies = [];
  parsed.sections.forEach(function (s) {
    const get = function (key) { const e = s.entries.find(function (x) { return x.key === key; }); return e ? e.value : ''; };
    const isTrader = get('buy_condition') || get('sell_condition') || get('buy_supplies');
    if (isTrader) traders.push({ id: s.name, buy_condition: get('buy_condition'), sell_condition: get('sell_condition'), buy_supplies: get('buy_supplies') });
    else if (s.entries.length) supplies.push({ id: s.name, items: s.entries.map(function (e) { return { item: e.key, value: e.value }; }) });
  });
  return { traders: traders, supplies: supplies };
}


// === Монстры monsters.ltx / M_*.ltx (#25) ===
export function parseMonsters(text) {
  const parsed = parseLtx(text);
  return parsed.sections.filter(function (s) {
    const cls = s.entries.find(function (e) { return e.key === 'class'; });
    return cls && /^SM_/.test(String(cls.value).trim());
  }).map(function (s) {
    const get = function (key) { const e = s.entries.find(function (x) { return x.key === key; }); return e ? e.value : ''; };
    return { id: s.name, base: (s.parents || [])[0] || '', class: get('class'), visual: get('visual'), icon: get('icon'), script_binding: get('script_binding'), health: get('MaxHealthValue') };
  });
}

// === Актор actor.ltx (#26) ===
export function parseActor(text) {
  const parsed = parseLtx(text);
  const sec = parsed.sections.find(function (s) { return s.name.toLowerCase() === 'actor'; });
  if (!sec) return null;
  const get = function (key) { const e = sec.entries.find(function (x) { return x.key === key; }); return e ? e.value : ''; };
  return {
    max_item_mass: get('max_item_mass'),
    jump_speed: get('jump_speed'),
    run_coef: get('run_coef'),
    sprint_koef: get('sprint_koef'),
    money: get('money'),
    rank: get('rank'),
    visual: get('visual'),
    class: get('class'),
    script_binding: get('script_binding'),
    entries: sec.entries.map(function (e) { return { key: e.key, value: e.value }; })
  };
}

// === Спавн-секции spawn_sections.ltx (#14) ===
export function parseSpawnSections(text) {
  const parsed = parseLtx(text);
  return parsed.sections.map(function (s) {
    const get = function (key) { const e = s.entries.find(function (x) { return x.key === key; }); return e ? e.value : ''; };
    return {
      id: s.name,
      base: (s.parents || [])[0] || '',
      character_profile: get('character_profile'),
      community: get('community'),
      spec_rank: get('spec_rank'),
      story_id: get('story_id'),
      spawn: get('$spawn'),
      npc: get('npc'),
      logic: get('logic')
    };
  });
}

// === Тайники secrets.ltx (#79) ===
export function parseSecrets(text) {
  const parsed = parseLtx(text);
  const listSec = parsed.sections.find(function (s) { return s.name.toLowerCase() === 'list'; });
  const lists = listSec ? (listSec.parents || []) : [];
  const treasures = parsed.sections.filter(function (s) { return s.name.toLowerCase() !== 'list'; }).map(function (s) {
    return { id: s.name, parents: s.parents, items: s.entries.map(function (e) { return { item: e.key, value: e.value }; }) };
  });
  return { lists: lists, treasures: treasures };
}

// === Разбор game_relations.ltx (сообщества и отношения фракций) (#9) ===

export function parseGameRelations(text) {
  const parsed = parseLtx(text);
  const communities = [];
  const relations = {};
  const monsterCommunities = [];
  parsed.sections.forEach(function (sec) {
    if (sec.name === 'communities') {
      const e = sec.entries.find(function (x) { return x.key === 'communities'; });
      if (e) {
        const parts = e.value.split(',').map(function (x) { return x.trim(); }).filter(Boolean);
        for (let i = 0; i < parts.length; i += 2) communities.push(parts[i]);
      }
    } else if (sec.name === 'communities_relations') {
      sec.entries.forEach(function (e) { relations[e.key] = e.value.split(',').map(function (x) { return x.trim(); }); });
    } else if (sec.name === 'monster_communities') {
      const e = sec.entries.find(function (x) { return x.key === 'communities'; });
      if (e) {
        const parts = e.value.split(',').map(function (x) { return x.trim(); }).filter(Boolean);
        for (let i = 0; i < parts.length; i += 2) monsterCommunities.push(parts[i]);
      }
    }
  });
  return { communities: communities, relations: relations, monsterCommunities: monsterCommunities };
}

// Переименовать секцию с сохранением родителей (#21).
export function renameSection(text, oldName, newName) {
  return splitLines(text).map(function (l) {
    return l.replace(/^\[([^\]]+)\]/, function (full, inner) {
      const ci = inner.indexOf(':');
      const name = (ci >= 0 ? inner.slice(0, ci) : inner).trim();
      if (name.toLowerCase() === String(oldName).toLowerCase()) return '[' + newName + (ci >= 0 ? inner.slice(ci) : '') + ']';
      return full;
    });
  }).join(NL);
}

// Заменить подстроку в значениях конкретного ключа во всех секциях (#21).
export function replaceFieldValues(text, key, from, to) {
  return splitLines(text).map(function (l) {
    const ci = l.indexOf(';');
    const code = (ci !== -1 ? l.slice(0, ci) : l).trim();
    const eq = code.indexOf('=');
    if (eq > 0 && code.slice(0, eq).trim() === key) {
      const comment = ci !== -1 ? ' ' + l.slice(ci) : '';
      return key + ' = ' + code.slice(eq + 1).trim().split(from).join(to) + comment;
    }
    return l;
  }).join(NL);
}



// === Профили полей по категориям предметов (#15-20) ===

export const ITEM_FIELD_PROFILES = {
  'Оружие': ['ammo_class', 'ammo_mag_size', 'ammo_limit', 'ammo_elapsed', 'fire_modes', 'fire_distance', 'bullet_speed', 'rpm', 'rpm_empty_click', 'hit_power', 'hit_impulse', 'hit_type', 'fire_dispersion_base', 'grenade_class', 'launch_speed', 'scope_status', 'scope_name', 'scope_zoom_factor', 'silencer_status', 'silencer_name', 'grenade_launcher_status', 'grenade_launcher_name', 'zoom_enabled', 'weapon_class', 'slot', 'animation_slot', 'hand_dependence', 'single_handed', 'upgrades', 'installed_upgrades', 'upgrade_scheme', 'hud', 'position', 'orientation', 'snd_shoot', 'snd_empty', 'snd_reload', 'snd_draw', 'snd_holster', 'fire_point', 'shell_point', 'flame_particles', 'smoke_particles', 'misfire_probability', 'condition_shot_dec', 'use_aim_bullet', 'time_to_aim', 'kill_msg_x', 'kill_msg_y', 'kill_msg_width', 'kill_msg_height', 'cost', 'inv_weight'],
  'Боеприпасы': ['k_dist', 'k_disp', 'k_impulse', 'k_pierce', 'k_ap', 'k_hit', 'k_air_resistance', 'k_bullet_speed', 'k_cam_dispersion', 'impair', 'buck_shot', 'tracers', 'box_size', 'inv_weight'],
  'Броня': ['burn_protection', 'strike_protection', 'shock_protection', 'wound_protection', 'radiation_protection', 'telepatic_protection', 'chemical_burn_protection', 'explosion_protection', 'fire_wound_protection', 'additional_inventory_weight', 'additional_inventory_weight2', 'ef_equipment_type', 'bones_koeff_protection', 'immunities_sect', 'actor_visual', 'full_icon_name', 'slot', 'power_loss', 'night_vision_sect', 'artefact_count', 'speed_coef', 'upgrades', 'upgrade_scheme', 'cost', 'inv_weight'],
  'Артефакты': ['health_restore_speed', 'radiation_restore_speed', 'satiety_restore_speed', 'power_restore_speed', 'bleeding_restore_speed', 'additional_inventory_weight', 'hit_absorbation', 'cost'],
  'Юзабельные': ['eat_health', 'eat_power', 'eat_satiety', 'eat_radiation', 'eat_alcohol', 'eat_max_power', 'eat_portions_num', 'cost', 'inv_weight'],
  'Квестовое': ['quest_item', 'story_id', 'can_trade', 'cost', 'inv_weight'],
  'Апгрейды': ['cost', 'hit_power', 'bullet_speed', 'rpm', 'hit_impulse', 'ammo_mag_size', 'ammo_limit', 'scheme_index', 'parent', 'condition', 'prerequisites', 'effects', 'icon', 'name'],
  'Детекторы': ['detect_actor', 'detect_artefacts', 'hud', 'af_sound', 'af_rank', 'cost'],
  'Торговля': ['buy_condition', 'sell_condition', 'buy_supplies', 'buy_price', 'sell_price']
};

// Возвращает профиль полей для секции (по категории; trade_* — торговля).
export function itemFieldProfile(section) {
  const name = String((section && section.name) || '').toLowerCase();
  let prof;
  if (name.indexOf('trade') === 0 || name.indexOf('_trade') !== -1) prof = ITEM_FIELD_PROFILES['Торговля'];
  else prof = ITEM_FIELD_PROFILES[classifyItem(section)] || [];
  return ITEM_COMMON_FIELDS.concat(prof);
}

// === Подсветка синтаксиса LTX (#2) ===
export function highlightLtx(text) {
  const esc = function (s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); };
  return String(text || '').split('\n').map(function (line) {
    if (/^\s*;/.test(line)) return '<span style="color:#6b7280">' + esc(line) + '</span>';
    const sec = line.match(/^(\s*)(\[[^\]]*\])(\s*)(.*)$/);
    if (sec) return esc(sec[1]) + '<span style="color:#a78bfa;font-weight:bold">' + esc(sec[2]) + '</span>' + (sec[4] ? '<span style="color:#6b7280">' + esc(sec[3] + sec[4]) + '</span>' : '');
    const inc = line.match(/^(\s*)(#include\s+"[^"]*")(\s*)(.*)$/);
    if (inc) return esc(inc[1]) + '<span style="color:#2dd4bf">' + esc(inc[2]) + '</span>' + (inc[4] ? '<span style="color:#6b7280">' + esc(inc[3] + inc[4]) + '</span>' : '');
    const kv = line.match(/^(\s*)([^=;]+?)(\s*=\s*)([^;]*)(;.*)?$/);
    if (kv) return esc(kv[1]) + '<span style="color:#fbbf24">' + esc(kv[2]) + '</span>' + esc(kv[3]) + '<span style="color:#d1d5db">' + esc(kv[4]) + '</span>' + (kv[5] ? '<span style="color:#6b7280">' + esc(kv[5]) + '</span>' : '');
    return esc(line);
  }).join('\n');
}

// === Дерево наследования секций LTX (#3) ===
export function ltxInheritanceTree(text) {
  const sections = parseLtx(text).sections;
  const children = {};
  const roots = [];
  sections.forEach(function (s) {
    if (!s.name) return;
    const parents = s.parents || [];
    if (!parents.length) { if (roots.indexOf(s.name) === -1) roots.push(s.name); return; }
    parents.forEach(function (p) { (children[p] = children[p] || []); if (children[p].indexOf(s.name) === -1) children[p].push(s.name); });
  });
  return { roots: roots, children: children };
}

// === Словарь описаний полей предметов (#5) ===
export const FIELD_DESCRIPTIONS = {
  cost: 'Цена', inv_weight: 'Вес в инвентаре', inv_name: 'Название в инвентаре (text_id)', inv_name_short: 'Короткое название',
  description: 'Описание (text_id)', visual: 'Модель (OGF)', class: 'Класс предмета (для движка)', '$spawn': 'Секция спавна в редакторе уровней',
  ammo_class: 'Используемые патроны (секции)', ammo_mag_size: 'Размер магазина', ammo_limit: 'Лимит патронов', ammo_elapsed: 'Патроны в магазине',
  fire_modes: 'Режимы огня (1/0, -1 — авто)', fire_distance: 'Дальность стрельбы', bullet_speed: 'Начальная скорость пули', rpm: 'Скорострельность (выстр/мин)',
  hit_power: 'Урон', hit_impulse: 'Импульс (отдача цели)', hit_type: 'Тип ранения', fire_dispersion_base: 'Базовое рассеивание',
  grenade_class: 'Подствольные гранаты', slot: 'Слот инвентаря', animation_slot: 'Тип анимации', hud: 'Секция HUD', position: 'Позиция в руках',
  orientation: 'Ориентация в руках', snd_shoot: 'Звук выстрела', snd_reload: 'Звук перезарядки', snd_draw: 'Звук доставания', snd_holster: 'Звук убирания',
  misfire_probability: 'Вероятность осечки', condition_shot_dec: 'Износ за выстрел', use_aim_bullet: 'Баллистика по прицелу', time_to_aim: 'Время прицеливания',
  burn_protection: 'Защита от ожогов', strike_protection: 'Защита от ударов', shock_protection: 'Защита от электричества', wound_protection: 'Защита от ран',
  radiation_protection: 'Защита от радиации', telepatic_protection: 'Защита от пси-воздействия', chemical_burn_protection: 'Защита от хим. ожогов',
  explosion_protection: 'Защита от взрывов', fire_wound_protection: 'Защита от огнестрела', immunities_sect: 'Секция иммунитетов',
  actor_visual: 'Модель на игроке', full_icon_name: 'Полная иконка NPC', power_loss: 'Потеря энергии', night_vision_sect: 'ПНВ', artefact_count: 'Слотов под артефакты', speed_coef: 'Модификатор скорости',
  eat_health: 'Восстановление здоровья', eat_power: 'Восстановление энергии', eat_satiety: 'Насыщение', eat_radiation: 'Вывод радиации', eat_alcohol: 'Опьянение', eat_max_power: 'Макс. энергия', eat_portions_num: 'Порций',
  k_dist: 'Дистанция (боеприпас)', k_disp: 'Рассеивание (боеприпас)', k_impulse: 'Импульс (боеприпас)', k_pierce: 'Бронепробитие', k_ap: 'Пробитие брони', k_hit: 'Урон (боеприпас)', k_bullet_speed: 'Скорость пули',
  inv_grid_x: 'X иконки в атласе', inv_grid_y: 'Y иконки в атласе', inv_grid_width: 'Ширина иконки (ячейки)', inv_grid_height: 'Высота иконки (ячейки)',
  death_items: 'Предметы, выпадающие при смерти', scope_name: 'Секция прицела', silencer_name: 'Секция глушителя', grenade_launcher_name: 'Секция подствольника',
  upgrades: 'Доступные апгрейды', installed_upgrades: 'Установленные апгрейды', upgrade_scheme: 'Схема апгрейдов', prerequisites: 'Требования апгрейда', effects: 'Эффекты апгрейда',
  quest_item: 'Квестовый предмет', story_id: 'ID для скриптов', can_trade: 'Можно продавать', buy_condition: 'Условие покупки', sell_condition: 'Условие продажи', buy_supplies: 'Условие ассортимента'
};

