// Экспорт диалогов в игровые XML/ZIP: несколько диалогов, dialogs.ltx, string_table (#1, #2, #4, #5).
// Чистый модуль: возвращает строки; кодировку применяет вызывающий код.
import { validateGraph } from './graph.js';

function esc(v) { return String(v == null ? '' : v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;'); }

// Один диалог → строка `<dialog>...</dialog>` с полным round-trip: text, sound, 6 тегов метаданных, next (#4).
export function dialogueToXml(dialog) {
  const name = String((dialog && dialog.id) || (dialog && dialog.name) || 'dialogue');
  const nodes = Array.isArray(dialog.nodes) ? dialog.nodes : [];
  const edges = Array.isArray(dialog.edges) ? dialog.edges : [];
  const errs = validateGraph(nodes, edges).filter(function (x) { return x.severity === 'error'; });
  if (errs.length) throw new Error('Диалог ' + name + ': исправьте ошибки графа (' + errs.length + ')');
  const real = nodes.filter(function (n) { return n.type === 'PHRASE'; });
  const idOf = new Map(), used = new Set();
  real.forEach(function (n, i) {
    let id = String((n.data || {}).phraseId || i);
    const base = id; let k = 1;
    while (used.has(id)) id = base + '_' + (k++);
    used.add(id); idOf.set(n.id, id);
  });
  const out = {};
  edges.filter(function (e) { return !e.broken && idOf.has(e.source) && idOf.has(e.target); }).forEach(function (e) { (out[e.source] = out[e.source] || []).push(idOf.get(e.target)); });
  const tags = function (d) {
    const m = (d.metadata || (d.xray && d.xray.metadata) || d.xray) || {};
    return ['has_info', 'dont_has_info', 'precondition', 'give_info', 'disable_info', 'action'].map(function (k) {
      return (Array.isArray(m[k]) ? m[k] : m[k] ? [m[k]] : []).map(function (v) { return '<' + k + '>' + esc(v) + '</' + k + '>'; }).join('');
    }).join('');
  };
  const phrases = real.map(function (n) {
    const d = n.data || {}, id = idOf.get(n.id), t = d.use_id !== false ? (d.text_id || '') : (d.text_id || '');
    return '<phrase id="' + esc(id) + '"><text>' + esc(t) + '</text>' + (d.voice_id ? '<sound>' + esc(d.voice_id) + '</sound>' : '') + tags(d) + (out[n.id] || []).map(function (x) { return '<next>' + esc(x) + '</next>'; }).join('') + '</phrase>';
  }).join('');
  return '<dialog id="' + esc(name) + '"><phrase_list>' + phrases + '</phrase_list></dialog>';
}

// `dialogs.ltx` со списком всех диалогов (#1).
export function dialogsLtx(ids) {
  return '[dialogs]\ndialogs = ' + (ids || []).join(', ') + '\n';
}

// Отчёт о ссылках text_id без перевода (#5).
export function validateTextIds(dialogues, languages) {
  const issues = [];
  const langIds = new Set();
  (Array.isArray(languages) ? languages : []).forEach(function (l) { (l.entries || []).forEach(function (e) { if (e.id) langIds.add(e.id); }); });
  (Array.isArray(dialogues) ? dialogues : []).forEach(function (d) {
    (d.nodes || []).forEach(function (n) {
      if (n.type !== 'PHRASE') return;
      const t = n.data && n.data.text_id;
      if (t && !langIds.has(t)) issues.push({ dialogue: d.id || d.name, text_id: t, msg: (d.id || d.name) + ': нет перевода для ' + t });
    });
  });
  return issues;
}

// Полная карта файлов (путь → текст) для нескольких групп диалогов (#2).
// groups: [{ fileName, dialogues: [{id, nodes, edges}] }]. cfg = 'config' | 'configs'.
export function gameFilesFromDialogues(groups, cfg, encoding, languages, projectName) {
  const enc = encoding === 'utf-8' ? 'UTF-8' : 'windows-1251';
  const cfgDir = cfg === 'configs' ? 'configs' : 'config';
  const decl = '<?xml version="1.0" encoding="' + enc + '"?>';
  const data = {};
  const allIds = [];
  const allPhrases = [];
  (groups || []).forEach(function (g) {
    const fileName = String(g.fileName || 'dialogue').replace(/[^a-zA-Z0-9_-]/g, '_') || 'dialogue';
    const dialogs = (g.dialogues || []).filter(Boolean);
    dialogs.forEach(function (d) { allIds.push(String(d.id || d.name || '')); (d.nodes || []).forEach(function (n) { if (n.type === 'PHRASE') allPhrases.push(n); }); });
    const xml = decl + '\n<game_dialogs>' + dialogs.map(function (d) { return dialogueToXml(d); }).join('') + '</game_dialogs>\n';
    data['gamedata/' + cfgDir + '/gameplay/dialogs_' + fileName + '.xml'] = xml;
  });
  if (allIds.length) data['gamedata/' + cfgDir + '/gameplay/dialogs.ltx'] = dialogsLtx(allIds);
  const safe = String(projectName || 'dialogue').replace(/[^a-zA-Z0-9_-]/g, '_') || 'dialogue';
  const langs = Array.isArray(languages) && languages.length ? languages : [{ locale: 'rus', name: 'Русский', entries: [] }];
  langs.forEach(function (lang) {
    const byId = new Map((lang.entries || []).map(function (x) { return [x.id, x.text]; }));
    allPhrases.forEach(function (n) { const d = n.data || {}; if (d.text_id && !byId.has(d.text_id) && d.text) byId.set(d.text_id, d.text); });
    const table = decl + '\n<string_table>' + Array.from(byId).map(function (x) { return '<string id="' + esc(x[0]) + '"><text>' + esc(x[1]) + '</text></string>'; }).join('') + '</string_table>\n';
    data['gamedata/' + cfgDir + '/text/' + (lang.locale || 'rus') + '/st_' + safe + '.xml'] = table;
  });
  return data;
}

// === Резолв text_id → перевод (#5) ===
export function resolveTextId(languages, textId) {
  const langs = (languages || []).slice();
  for (let i = 0; i < langs.length; i++) {
    const e = (langs[i].entries || []).find(function (x) { return x.id === textId && String(x.text || '').trim(); });
    if (e) return { locale: langs[i].locale, text: e.text };
  }
  return null;
}

// === Проверка text_id: дубликаты, пустые, без перевода (#77) ===
export function findTextIdIssues(languages, dialogues) {
  const issues = [];
  const used = new Set();
  (dialogues || []).forEach(function (d) { (d.nodes || []).forEach(function (n) { if (n.type === 'PHRASE' && n.data && n.data.text_id) used.add(n.data.text_id); }); });
  (languages || []).forEach(function (lang) {
    const seen = new Set();
    (lang.entries || []).forEach(function (e) {
      if (!e.id) return;
      if (seen.has(e.id)) issues.push({ locale: lang.locale, text_id: e.id, msg: 'Дубликат id: ' + e.id });
      seen.add(e.id);
      if (!String(e.text || '').trim()) issues.push({ locale: lang.locale, text_id: e.id, msg: 'Пустой перевод: ' + e.id });
    });
  });
  used.forEach(function (id) {
    const has = (languages || []).some(function (lang) { return (lang.entries || []).some(function (e) { return e.id === id && String(e.text || '').trim(); }); });
    if (!has) issues.push({ locale: '', text_id: id, msg: 'Нет перевода ни в одной локали: ' + id });
  });
  return issues;
}


// === Покрытие text_id переводами (в %) ===
export function textIdCoverage(dialogues, languages) {
  const ids = new Set();
  (dialogues || []).forEach(function (d) { (d.nodes || []).forEach(function (n) { if (n.type === 'PHRASE' && n.data && n.data.text_id) ids.add(n.data.text_id); }); });
  if (!ids.size) return { total: 0, translated: 0, pct: 100 };
  let translated = 0;
  ids.forEach(function (id) {
    const has = (languages || []).some(function (l) { return (l.entries || []).some(function (e) { return e.id === id && String(e.text || '').trim(); }); });
    if (has) translated++;
  });
  return { total: ids.size, translated: translated, pct: Math.round(translated / ids.size * 100) };
}

