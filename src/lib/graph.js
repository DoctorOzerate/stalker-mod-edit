// Работа с графом диалога: типы узлов, нормализация, валидация, раскладка, рендеринг SVG.
// Зависимости: только локальная escapeXml.

function escapeXml(v) { return String(v == null ? '' : v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;'); }

export const NODE_TYPES = {
  START: { label: 'Старт', icon: '▶', color: '#16a34a', desc: 'Начало диалога' },
  PHRASE: { label: 'Реплика', icon: '💬', color: '#2563eb', desc: 'Игровая реплика NPC/ГГ/не определено' },
  CONDITION: { label: 'Условие', icon: '❓', color: '#15803d', desc: 'Проверка выражения' },
  QUEST_CHECK: { label: 'Проверка квеста', icon: '📋', color: '#9333ea', desc: 'Проверка info portion' },
  ITEM_CHECK: { label: 'Проверка предмета', icon: '🎒', color: '#22c55e', desc: 'Наличие предмета' },
  RANDOM: { label: 'Случайный выбор', icon: '🎲', color: '#4f46e5', desc: 'Случайная ветка' },
  ACTION: { label: 'Действие', icon: '⚙', color: '#dc2626', desc: 'Выдать предмет, вызвать скрипт' },
  END: { label: 'Конец', icon: '■', color: '#64748b', desc: 'Завершение диалога' }
};
export const PALETTE_HIDDEN = ['CONDITION', 'QUEST_CHECK', 'ITEM_CHECK', 'ACTION'];

export const META_KEYS = ['has_info', 'dont_has_info', 'precondition', 'give_info', 'disable_info', 'action'];
export const FALLBACK_NODE = { label: 'Неизвестный узел', icon: '?', color: '#64748b', desc: 'Неизвестный тип' };
export function safeMetadata(value) {
  const src = value && typeof value === 'object' && !Array.isArray(value) ? value : {};
  const out = {};
  META_KEYS.forEach(function (key) {
    const v = src[key];
    out[key] = Array.isArray(v) ? v.filter(function (x) { return x != null; }).map(String) : (v == null || v === '' ? [] : [String(v)]);
  });
  return out;
}
export function safeData(value) {
  const source = value && typeof value === 'object' && !Array.isArray(value) ? value : {};
  const data = Object.assign({ phraseId: '', role: 'unknown', roleSource: 'migration', text: '', text_id: '', use_id: false, voice_id: '', expression: '', action_type: '', params: '' }, source);
  data.role = ['unknown', 'npc', 'player'].indexOf(data.role) >= 0 ? data.role : 'unknown';
  data.metadata = safeMetadata(source.metadata || (source.xray && source.xray.metadata) || source.xray);
  delete data['spe' + 'aker'];
  return data;
}
// Подсказка text_id для реплики из phraseId/dialogueId (#3).
export function suggestTextId(node, dialogueId) {
  const d = safeData(node && node.data);
  const base = String(dialogueId || 'dialogue').replace(/[^a-zA-Z0-9_]/g, '_') || 'dialogue';
  let pid = String(d.phraseId || (node && node.id) || '').replace(/[^a-zA-Z0-9_]/g, '_');
  if (!pid) pid = 'phrase';
  if (pid.indexOf(base + '_') === 0) return pid;
  return base + '_' + pid;
}

// Проверка баланса скобок/кавычек в precondition/action (#4).
function bracketBalance(s) {
  const str = String(s || '');
  const stack = [];
  const closers = { ')': '(', ']': '[', '}': '{' };
  let i = 0, n = str.length;
  while (i < n) {
    const c = str[i];
    if (c === '"' || c === "'") {
      const q = c; i++;
      while (i < n) { if (str[i] === '\\') i += 2; else if (str[i] === q) { i++; break; } else i++; }
      continue;
    }
    if (c === '-' && str[i + 1] === '-' && str[i + 2] === '[') {
      i += 3;
      while (i < n && !(str[i] === ']' && str[i + 1] === ']')) i++;
      i = Math.min(i + 2, n);
      continue;
    }
    if (c === '(' || c === '[' || c === '{') stack.push(c);
    else if (c === ')' || c === ']' || c === '}') {
      if (stack.pop() !== closers[c]) return { ok: false, depth: stack.length };
    }
    i++;
  }
  return { ok: stack.length === 0, depth: stack.length };
}

// Синтаксическая проверка метаданных реплики (#4): info-порции и условия/действия.
export function validateMetadataSyntax(metadata) {
  const issues = [];
  const m = safeMetadata(metadata);
  ['has_info', 'dont_has_info', 'give_info', 'disable_info'].forEach(function (key) {
    (m[key] || []).forEach(function (raw) {
      const v = String(raw == null ? '' : raw).trim();
      if (!v) return;
      if (/[;{}\[\]<>]/.test(v)) issues.push({ severity: 'warning', code: 'metadata_syntax', key: key, msg: key + ': недопустимые символы в «' + v + '»' });
      if (v.indexOf('(') !== -1 && v.indexOf(')') === -1) issues.push({ severity: 'warning', code: 'metadata_syntax', key: key, msg: key + ': незакрытая скобка в «' + v + '»' });
    });
  });
  ['precondition', 'action'].forEach(function (key) {
    (m[key] || []).forEach(function (raw) {
      const v = String(raw == null ? '' : raw);
      if (!v.trim()) return;
      const b = bracketBalance(v);
      if (!b.ok) issues.push({ severity: 'error', code: 'metadata_balance', key: key, msg: key + ': несбалансированные скобки/кавычки' });
    });
  });
  return issues;
}

export function safeNode(node, index) {
  const raw = node && typeof node === 'object' ? node : {};
  const oldType = raw.type;
  const type = oldType === 'NPC_REPLY' || oldType === 'PLAYER_REPLY' ? 'PHRASE' : String(oldType || 'UNKNOWN');
  const data = safeData(raw.data);
  if (oldType === 'NPC_REPLY') data.role = 'npc';
  if (oldType === 'PLAYER_REPLY') data.role = 'player';
  if (oldType === 'NPC_REPLY' || oldType === 'PLAYER_REPLY') data.roleSource = 'migration';
  if (type === 'PHRASE' && !data.phraseId) data.phraseId = String(raw.id || index);
  return Object.assign({}, raw, { id: String(raw.id || ('node_' + index)), type: type, x: Number.isFinite(+raw.x) ? +raw.x : 0, y: Number.isFinite(+raw.y) ? +raw.y : 0, data: data });
}
export function flattenDialogues(value) {
  const source = Array.isArray(value) ? value : [];
  const flat = [];
  source.forEach(function (item) {
    if (item && Array.isArray(item.dialogues)) item.dialogues.forEach(function (d) { if (d && typeof d === 'object') flat.push(d); });
    else if (item && typeof item === 'object') flat.push(item);
  });
  return flat.map(function (d, i) {
    return Object.assign({}, d, { id: String(d.id || ('dialogue_' + i)), name: String(d.name || d.id || ('dialogue_' + i)), nodes: Array.isArray(d.nodes) ? d.nodes.map(safeNode) : [], edges: Array.isArray(d.edges) ? d.edges.filter(function (e) { return e && typeof e === 'object'; }) : [] });
  });
}

export function validateGraph(nodes, edges) {
  const issues = [], known = new Set(Object.keys(NODE_TYPES)), ids = new Map(), out = new Map();
  (Array.isArray(nodes) ? nodes : []).forEach(function (node, index) {
    if (!node || typeof node !== 'object' || !known.has(node.type)) {
      issues.push({ severity: 'error', code: 'type', msg: 'Неизвестный тип узла #' + (index + 1) });
    } else if (!node.id || ids.has(node.id)) {
      issues.push({ severity: 'error', code: 'dup_id', msg: 'Дублирующийся или пустой ID узла: ' + (node.id || '(пусто)') });
    } else ids.set(node.id, node);
  });
  (Array.isArray(edges) ? edges : []).forEach(function (edge) {
    if (!edge || edge.broken || !ids.has(edge.source) || !ids.has(edge.target)) {
      issues.push({ severity: 'error', code: 'broken_edge', msg: 'Битая связь ' + ((edge && edge.source) || '?') + ' → ' + ((edge && edge.target) || '?') });
    } else {
      if (!out.has(edge.source)) out.set(edge.source, []);
      out.get(edge.source).push(edge.target);
    }
  });
  const starts = Array.from(ids.values()).filter(function (n) { return n.type === 'START'; });
  const ends = Array.from(ids.values()).filter(function (n) { return n.type === 'END'; });
  if (starts.length) {
    const reachable = new Set(), queue = starts.map(function (n) { return n.id; });
    while (queue.length) { const id = queue.shift(); if (reachable.has(id)) continue; reachable.add(id); (out.get(id) || []).forEach(function (target) { queue.push(target); }); }
    ids.forEach(function (_, id) { if (!reachable.has(id)) issues.push({ severity: 'warning', code: 'unreachable', msg: 'Недостижимый узел: ' + id }); });
  }
  if (!starts.length) issues.push({ severity: 'error', code: 'missing_start', msg: 'Отсутствует START' });
  if (!ends.length) issues.push({ severity: 'error', code: 'missing_end', msg: 'Отсутствует END' });
  Array.from(ids.values()).forEach(function (n) {
    if (n.type !== 'PHRASE') return;
    const d = safeData(n.data);
    const m = d.metadata;
    validateMetadataSyntax(m).forEach(function (iss) { issues.push({ severity: iss.severity, code: iss.code, msg: n.id + ': ' + iss.msg }); });
    const hasMeta = Object.keys(m).some(function (k) { return Array.isArray(m[k]) && m[k].some(function (v) { return String(v || '').trim(); }); });
    const hasNext = (out.get(n.id) || []).length > 0;
    if (!String(d.text || '').trim() && !String(d.text_id || '').trim() && !hasMeta && !hasNext) {
      issues.push({ severity: 'error', code: 'empty_text', msg: 'Пустой текст реплики: ' + n.id });
    }
    if (!hasNext) issues.push({ severity: 'warning', code: 'dead_end', msg: 'Реплика без продолжения (нет next/END): ' + n.id });
  });
  const color = new Map();
  function visit(id) {
    color.set(id, 1);
    (out.get(id) || []).forEach(function (target) {
      if (color.get(target) === 1) issues.push({ severity: 'warning', code: 'imported_cycle', msg: 'Цикл: ' + id + ' → ' + target });
      else if (color.get(target) !== 2) visit(target);
    });
    color.set(id, 2);
  }
  ids.forEach(function (_, id) { if (!color.get(id)) visit(id); });
  return issues;
}

export function layoutDialogue(nodes, edges) {
  const adj = {};
  edges.forEach(function (e) { (adj[e.source] = adj[e.source] || []).push(e.target); });
  const level = {};
  const start = nodes.find(function (n) { return n.type === 'START'; });
  const queue = start ? [start.id] : [];
  if (start) level[start.id] = 0;
  const visited = new Set();
  let qIndex = 0;
  while (qIndex < queue.length) {
    const cur = queue[qIndex++];
    if (visited.has(cur)) continue;
    visited.add(cur);
    (adj[cur] || []).forEach(function (t) {
      if (level[t] == null) level[t] = (level[cur] || 0) + 1;
      queue.push(t);
    });
  }
  nodes.forEach(function (n) { if (level[n.id] == null) level[n.id] = 0; });
  const groups = {};
  nodes.forEach(function (n) { (groups[level[n.id]] = groups[level[n.id]] || []).push(n.id); });
  const lx = 60, ly = 80, xs = 320, ys = 110;
  Object.keys(groups).forEach(function (l) {
    groups[l].forEach(function (id, i) {
      const n = nodes.find(function (x) { return x.id === id; });
      if (n) { n.x = lx + (+l) * xs; n.y = ly + i * ys; }
    });
  });
}

export function nodeLabel(n, languages, showText) {
  const d = n.data || {};
  if (d.use_id && d.text_id) {
    if (showText === false) return '#' + d.text_id;
    const langs = Array.isArray(languages) ? languages : [];
    for (let i = 0; i < langs.length; i++) {
      const e = (langs[i].entries || []).find(function (x) { return x.id === d.text_id; });
      if (e && e.text) return e.text;
    }
    return '#' + d.text_id;
  }
  return (d.text || d.expression || d.action_type) || 'Нет содержимого';
}
export function roleColor(n) {
  if (!n || n.type !== 'PHRASE') return null;
  const role = n.data && n.data.role;
  return role === 'npc' ? '#2563eb' : role === 'player' ? '#16a34a' : '#64748b';
}
export function dialogueStats(nodes, edges) {
  const list = nodes || [];
  const phrases = list.filter(function (n) { return n.type === 'PHRASE'; });
  const adj = {};
  (edges || []).forEach(function (e) { (adj[e.source] = adj[e.source] || []).push(e.target); });
  const depth = {};
  const starts = list.filter(function (n) { return n.type === 'START'; });
  const q = starts.map(function (n) { return n.id; });
  starts.forEach(function (n) { depth[n.id] = 0; });
  let qi = 0;
  while (qi < q.length) { const id = q[qi++]; (adj[id] || []).forEach(function (t) { if (depth[t] == null) { depth[t] = depth[id] + 1; q.push(t); } }); }
  let maxDepth = 0;
  Object.keys(depth).forEach(function (k) { maxDepth = Math.max(maxDepth, depth[k]); });
  const totalText = phrases.reduce(function (sum, n) { return sum + String((n.data && n.data.text) || '').length; }, 0);
  return {
    nodes: list.length,
    edges: (edges || []).length,
    phrases: phrases.length,
    branches: phrases.filter(function (n) { return (adj[n.id] || []).length > 1; }).length,
    maxDepth: maxDepth,
    avgText: phrases.length ? Math.round(totalText / phrases.length) : 0
  };
}
export function graphToSvg(nodes, edges) {
  const list = nodes || [];
  if (!list.length) return '';
  const minX = Math.min.apply(null, list.map(function (n) { return n.x; })) - 20;
  const minY = Math.min.apply(null, list.map(function (n) { return n.y; })) - 20;
  const maxX = Math.max.apply(null, list.map(function (n) { return n.x + 210; })) + 20;
  const maxY = Math.max.apply(null, list.map(function (n) { return n.y + 100; })) + 20;
  const w = maxX - minX, h = maxY - minY;
  const byId = {};
  list.forEach(function (n) { byId[n.id] = n; });
  const L = [];
  L.push('<svg xmlns="http://www.w3.org/2000/svg" width="' + w + '" height="' + h + '" viewBox="' + minX + ' ' + minY + ' ' + w + ' ' + h + '">');
  L.push('<rect x="' + minX + '" y="' + minY + '" width="' + w + '" height="' + h + '" fill="#18181b"/>');
  (edges || []).forEach(function (e) {
    const a = byId[e.source], b = byId[e.target];
    if (!a || !b) return;
    const x1 = a.x + 210, y1 = a.y + 45, x2 = b.x, y2 = b.y + 45, mx = (x1 + x2) / 2;
    L.push('<path d="M ' + x1 + ' ' + y1 + ' C ' + mx + ' ' + y1 + ', ' + mx + ' ' + y2 + ', ' + x2 + ' ' + y2 + '" fill="none" stroke="#71717a" stroke-width="2"/>');
  });
  list.forEach(function (n) {
    const cfg = NODE_TYPES[n.type] || FALLBACK_NODE;
    const txt = escapeXml((nodeLabel(n, [], true) || n.id).slice(0, 60));
    L.push('<rect x="' + n.x + '" y="' + n.y + '" width="210" height="90" rx="8" fill="' + cfg.color + '"/>');
    L.push('<text x="' + (n.x + 12) + '" y="' + (n.y + 24) + '" fill="#fff" font-size="12" font-weight="bold">' + escapeXml(cfg.icon + ' ' + cfg.label) + '</text>');
    L.push('<text x="' + (n.x + 12) + '" y="' + (n.y + 50) + '" fill="#fff" font-size="11">' + txt + '</text>');
  });
  L.push('</svg>');
  return L.join('\n');
}

// === Группировка диалогов по локации/персонажу/типу (по префиксу id) ===
export const DIALOGUE_GROUPS = {
  esc: 'Кордон', escape: 'Кордон', agr: 'Агропром', gar: 'Свалка', dar: 'Тёмная долина',
  bar: 'Бар', mil: 'Военные склады', yantar: 'Янтарь', rad: 'Радар', pri: 'Припять', cit: 'Цитадель',
  dm: 'Служебные', tm: 'Торговля', quest: 'Квесты', tutorial: 'Обучение', optional: 'Дополнительные',
  general: 'Общие', hello: 'Приветствия', raid: 'Рейд', napad: 'Нападение',
  actor: 'ГГ (actor)', barman: 'Бармен', volk: 'Волк', oso: 'Осознание', dolg: 'Долг',
  trader: 'Торговец', brodyaga: 'Бродяга', vagonchik: 'Вагончик', val: 'Вал', hlam: 'Хлам', stalker: 'Сталкер'
};
export function dialogueGroup(id) {
  const prefix = String(id || '').split('_')[0].toLowerCase();
  return DIALOGUE_GROUPS[prefix] || (prefix ? prefix.charAt(0).toUpperCase() + prefix.slice(1) : 'Прочее');
}

