// Утилиты для работы с Lua-скриптами: токенизация, подсветка, форматирование, свёртка.
// Зависимости: только константы NL/Q/BS (определены локально).

const NL = '\n';
const Q = '"';
const BS = '\\';

const LUA_KEYWORDS = new Set(['function', 'end', 'if', 'then', 'else', 'elseif', 'for', 'while', 'do', 'return', 'local', 'nil', 'true', 'false', 'and', 'or', 'not', 'in', 'break', 'repeat', 'until']);
export function longBracketEnd(s, start) {
  // Длинная скобка Lua: [=*[ ... ]=*]. `start` указывает на первую '['.
  if (s[start] !== '[') return -1;
  let level = 0;
  while (s[start + 1 + level] === '=') level++;
  if (s[start + 1 + level] !== '[') return -1;
  const close = ']' + '='.repeat(level) + ']';
  const idx = s.indexOf(close, start + 2 + level);
  return idx === -1 ? -1 : idx + close.length;
}

export function tokenizeLua(code) {
  const tokens = [];
  const s = String(code || '');
  const n = s.length;
  let i = 0;
  const isAlpha = function (c) { return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'); };
  const isDigit = function (c) { return c >= '0' && c <= '9'; };
  const isId = function (c) { return isAlpha(c) || isDigit(c) || c === '_'; };
  const push = function (text, cls) { tokens.push({ text: text, cls: cls }); };
  while (i < n) {
    const c = s[i];
    if (c === '-' && s[i + 1] === '-') {
      if (s[i + 2] === '[') {
        const stop = longBracketEnd(s, i + 2);
        push(s.slice(i, stop === -1 ? n : stop), 'comment'); i = stop === -1 ? n : stop;
      } else {
        let j = i; while (j < n && s[j] !== NL) j++;
        push(s.slice(i, j), 'comment'); i = j;
      }
      continue;
    }
    if (c === '[') {
      const stop = longBracketEnd(s, i);
      if (stop !== -1) { push(s.slice(i, stop), 'string'); i = stop; continue; }
    }
    if (c === Q || c === "'") {
      const q = c; let j = i + 1; let closed = false;
      while (j < n) { if (s[j] === BS) j += 2; else if (s[j] === q) { closed = true; j++; break; } else j++; }
      push(s.slice(i, closed ? j : n), 'string'); i = closed ? j : n;
      continue;
    }
    if (isDigit(c)) {
      let j = i; while (j < n && (isDigit(s[j]) || s[j] === '.')) j++;
      push(s.slice(i, j), 'number'); i = j;
      continue;
    }
    if (isAlpha(c) || c === '_') {
      let j = i; while (j < n && isId(s[j])) j++;
      const word = s.slice(i, j);
      push(word, LUA_KEYWORDS.has(word) ? 'keyword' : 'ident'); i = j;
      continue;
    }
    if (c === '(' || c === ')' || c === '[' || c === ']' || c === '{' || c === '}') { push(c, 'bracket'); i++; continue; }
    push(c, 'plain'); i++;
  }
  return tokens;
}

export function tokenizeLuaLines(code) {
  const s = String(code || '');
  const lines = s.split(NL);
  const result = lines.map(function () { return []; });
  let li = 0;
  tokenizeLua(s).forEach(function (tk) {
    const text = String(tk.text);
    let seg = 0;
    while (seg < text.length) {
      const nl = text.indexOf(NL, seg);
      if (nl === -1) {
        if (seg < text.length && li < result.length) result[li].push({ text: text.slice(seg), cls: tk.cls });
        break;
      }
      if (nl > seg && li < result.length) result[li].push({ text: text.slice(seg, nl), cls: tk.cls });
      li++;
      seg = nl + 1;
    }
  });
  return result;
}

export function countLuaBrackets(code) {
  const s = String(code || '');
  return {
    open: (s.match(/[([{]/g) || []).length,
    close: (s.match(/[)\]}]/g) || []).length
  };
}

export function formatLua(code) {
  const lines = String(code || '').split(/\n/);
  let indent = 0;
  return lines.map(function (raw) {
    const t = raw.trim();
    if (!t) return '';
    if (/^(end|else|elseif|until)\b/.test(t)) indent = Math.max(0, indent - 1);
    const line = new Array(indent + 1).join('  ') + t;
    if (/(^function\b|\bthen\s*$|\bdo\s*$|^repeat\b$|^else\b$|^elseif\b)/.test(t)) indent++;
    return line;
  }).join('\n');
}

export function luaFunctionBlocks(lines) {
  const blocks = [];
  let depth = 0, start = -1;
  for (let i = 0; i < lines.length; i++) {
    const t = lines[i].trim();
    if (/^function\b/.test(t) || /^local\s+function\b/.test(t)) { if (depth === 0) start = i; depth++; }
    else if (/^end\b/.test(t)) { depth = Math.max(0, depth - 1); if (depth === 0 && start !== -1) { blocks.push({ start: start, end: i }); start = -1; } }
  }
  return blocks;
}

// === Автодополнение (#27), граф вызовов (#28), неиспользуемые функции (#29) ===

export function luaCompletions(prefix) {
  const p = String(prefix || '').toLowerCase();
  if (!p) return LUA_API.slice();
  return LUA_API.filter(function (f) { return f.toLowerCase().indexOf(p) !== -1; });
}

export function luaCallGraph(code) {
  const s = String(code || '');
  const lines = s.split(NL);
  const blocks = luaFunctionBlocks(lines);
  const defs = {};
  const names = [];
  blocks.forEach(function (b) {
    const m = lines[b.start].match(/function\s+([A-Za-z_][\w.:]*)/);
    if (!m) return;
    const name = m[1];
    names.push(name);
    defs[name] = lines.slice(b.start, b.end + 1).join(NL);
  });
  const escRe = function (x) { return x.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); };
  const edges = [];
  names.forEach(function (name) {
    names.forEach(function (other) {
      if (other === name) return;
      if (new RegExp('\\b' + escRe(other) + '\\s*\\(').test(defs[name])) edges.push({ from: name, to: other });
    });
  });
  return { functions: names, edges: edges };
}

export function luaUnused(code) {
  const s = String(code || '');
  const names = (s.match(/function\s+([A-Za-z_][\w.:]*)/g) || []).map(function (f) { return f.replace(/^function\s+/, ''); });
  const escRe = function (x) { return x.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); };
  const unused = [];
  names.forEach(function (name) {
    const count = (s.match(new RegExp('\\b' + escRe(name) + '\\b', 'g')) || []).length;
    if (count <= 1) unused.push(name);
  });
  return unused;
}


// === Расширения для моддинга STALKER (#28, #29, #31, #32) ===

// Известные глобальные имена движка X-Ray (не считаем ошибкой).
export const LUA_ENGINE_GLOBALS = [
  '_G', 'self', 'db', 'alife', 'level', 'game', 'device', 'profiler', 'fs',
  'math', 'string', 'table', 'os', 'io', 'coroutine', 'tonumber', 'tostring',
  'pairs', 'ipairs', 'type', 'pcall', 'xpcall', 'print', 'require', 'unpack',
  'select', 'setmetatable', 'getmetatable', 'rawget', 'rawset', 'next',
  'assert', 'error', 'loadstring', 'loadfile', 'dofile', 'collectgarbage',
  'news_manager', 'xr_effects', 'xr_sound', 'dialogs', 'actor_menu_inventory',
  'utils', 'bit', 'ini_file', 'time_global', 'class_registrator', 'game_registrator',
  'ui_registrator', 'state_mgr', 'xr_logic', 'xr_conditions', 'xr_evaluators_id',
  'xr_actions_id', 'treasure_manager', 'trade_manager', 'death_manager', 'task_manager'
];

// Частые функции API движка для автодополнения (#28, #49).
export const LUA_API = [
  'db.actor', 'db.actor:give_item', 'db.actor:take_item', 'db.actor:give_money',
  'db.actor:give_info_portion', 'db.actor:disable_info_portion', 'db.actor:has_info',
  'db.actor:alive', 'db.actor:position', 'db.actor:set_character_rank', 'db.actor:change_reputation',
  'db.actor:level_vertex_id', 'db.actor:game_vertex_id', 'db.actor:iterate_inventory', 'db.actor:active_item',
  'db.actor:object', 'db.actor:is_actor_outdoors', 'db.storage', 'db.server',
  'alife():create', 'alife():release', 'alife():object', 'alife():level_name', 'alife():switch_distance',
  'level.add_cam_effector', 'level.remove_cam_effector', 'level.get_game_time',
  'level.vertex_position', 'level.game_vertex_id', 'level.set_weather', 'level.set_time_factor',
  'level.spawn_item', 'level.spawn_phantom', 'level.add_pp_effector',
  'game.translate_string', 'game.get_game_time', 'game.start_tutorial', 'game.get_env_value',
  'news_manager.send_tip', 'news_manager.send_tip_nosound',
  'xr_effects.set_sound_volume', 'xr_sound.set_sound_volume',
  'dialogs.relocate_wounded', 'dialogs.start', 'dialogs.break_dialog',
  'actor_menu_inventory.show', 'utils.cfg', 'utils.xr_ini', 'device().set_internal_event',
  'level.patrol', 'level.add_death_callback', 'level.add_call'
];

// Сниппеты, специфичные для моддинга STALKER (#32, #50).
export const LUA_SNIPPETS = [
  { label: 'has_info', text: 'if db.actor:has_info("%+quest_done%") then\n\t\nend' },
  { label: 'give_item', text: 'db.actor:give_item("medkit")' },
  { label: 'give_money', text: 'db.actor:give_money(5000)' },
  { label: 'info', text: 'db.actor:give_info_portion("quest_given")' },
  { label: 'news tip', text: 'news_manager.send_tip(db.actor, "tip_id", nil, nil, 5000)' },
  { label: 'alife', text: 'alife():create("object", db.actor:position(), db.actor:level_vertex_id(), db.actor:game_vertex_id())' },
  { label: 'death cb', text: 'level.add_death_callback(character_id, function(victim, who)\n\t\nend)' },
  { label: 'logic', text: 'xr_logic.pstor_retrieve(obj, section)' },
  { label: 'translate', text: 'game.translate_string("text_id")' },
  { label: 'weather', text: 'level.set_weather("weather_id", true)' }
];

// Проверка баланса блоков (function/if/for/while/repeat vs end/until) (#31).
export function luaCheck(code) {
  const tokens = tokenizeLua(String(code || ''));
  const issues = [];
  let depth = 0;
  const openers = { 'function': 1, 'if': 1, 'for': 1, 'while': 1, 'repeat': 1 };
  for (let i = 0; i < tokens.length; i++) {
    const tk = tokens[i];
    if (tk.cls !== 'keyword') continue;
    if (openers[tk.text]) depth++;
    else if (tk.text === 'end' || tk.text === 'until') depth--;
    if (depth < 0) { issues.push({ severity: 'error', code: 'lua_extra_end', msg: 'Лишний end/until' }); depth = 0; }
  }
  if (depth > 0) issues.push({ severity: 'error', code: 'lua_unclosed', msg: 'Незакрытые блоки: ' + depth });
  return issues;
}

// Эвристика: глобальные присваивания без local (#29).
export function luaUndeclaredGlobals(code) {
  const lines = String(code || '').split(NL);
  const issues = [];
  const locals = new Set();
  const allow = new Set(LUA_ENGINE_GLOBALS);
  lines.forEach(function (raw) {
    const t = raw.replace(/--.*$/, '');
    let m = t.match(/\blocal\s+function\s+([A-Za-z_]\w*)/);
    if (m) { locals.add(m[1]); return; }
    m = t.match(/\blocal\s+([A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*)/);
    if (m) m[1].split(',').forEach(function (n) { const name = n.trim(); if (/^[A-Za-z_]\w*$/.test(name)) locals.add(name); });
  });
  lines.forEach(function (raw, i) {
    const t = raw.replace(/--.*$/, '').trim();
    const m = t.match(/^([A-Za-z_]\w*)\s*(?:,\s*[A-Za-z_]\w*)*\s*=/);
    if (m && !locals.has(m[1]) && !allow.has(m[1]) && m[1] !== 'end') {
      issues.push({ severity: 'warning', code: 'lua_global', line: i + 1, msg: 'Возможное глобальное присваивание (нет local): ' + m[1] });
    }
  });
  return issues;
}


// === Проверка привязок script_binding (#56) ===
export function validateScriptBindings(ltxText, scripts) {
  const issues = [];
  const scriptMap = {};
  (scripts || []).forEach(function (s) {
    const base = String(s.name || '').toLowerCase().replace(/\.script$/, '');
    scriptMap[base] = String(s.text || '');
  });
  const re = /script_binding\s*=\s*([A-Za-z_][\w.]*)/g;
  let m;
  while ((m = re.exec(String(ltxText || ''))) !== null) {
    const b = m[1];
    const dot = b.indexOf('.');
    const file = (dot >= 0 ? b.slice(0, dot) : b).toLowerCase();
    const fn = dot >= 0 ? b.slice(dot + 1) : '';
    const body = scriptMap[file];
    if (body == null) issues.push({ code: 'binding_no_file', binding: b, msg: 'script_binding: файл не найден — ' + file + '.script (' + b + ')' });
    else if (fn && !new RegExp('function\\s+' + fn.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\b').test(body)) issues.push({ code: 'binding_no_fn', binding: b, msg: 'script_binding: функция не найдена — ' + b });
  }
  return issues;
}

