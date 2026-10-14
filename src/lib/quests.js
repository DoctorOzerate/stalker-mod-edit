// Работа с квестами: парсинг/экспорт game_tasks и task_manager, валидация, цепочки.
import { DOMParser } from '@xmldom/xmldom';
import { parseLtx } from './ltx.js';

const NL = '\n';
function escapeXml(v) { return String(v == null ? '' : v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;'); }

export const QUEST_CATEGORIES = ['Сюжет', 'Побочный', 'Повторимый', 'Скрытый'];
export const QUEST_TEMPLATES = [
  { label: 'Принести предмет', prefix: 'fetch', prior: '10' },
  { label: 'Убить цель', prefix: 'kill', prior: '15' },
  { label: 'Сходить на локацию', prefix: 'goto', prior: '5' }
];
export function normalizeQuestObjectives(objectives) {
  return (Array.isArray(objectives) ? objectives : []).map(function (o) {
    if (o && typeof o === 'object') return { text: String(o.text || ''), icon: String(o.icon || ''), infoportion: String(o.infoportion || ''), article: String(o.article || '') };
    return { text: String(o || ''), icon: '', infoportion: '', article: '' };
  });
}
export function parseGameTasksXml(text) {
  let src = String(text || '').replace(/\b(set_complete|set_fail)\b(?=\s*[>\/])/g, '$1="true"');
  src = src.replace(/^\s*#include\b[^\n]*$/gm, ''); // убрать #include-строки
  let d = new DOMParser().parseFromString(src, 'application/xml');
  const rootIsFragment = d.documentElement && ['game_task', 'task'].indexOf(String(d.documentElement.tagName)) !== -1;
  if (d.getElementsByTagName('parsererror').length || rootIsFragment) {
    // фрагмент без корневого тега (tasks_*.xml) — обернуть
    d = new DOMParser().parseFromString('<game_tasks>' + src + '</game_tasks>', 'application/xml');
    if (d.getElementsByTagName('parsererror').length) return [];
  }
  const root = d.documentElement;
  if (!root) return [];
  const rn = String(root.tagName).toLowerCase();
  const format = rn === 'task_manager' ? 'task_manager' : (rn === 'game_tasks' || rn === 'game_tasks_data' ? 'game_tasks' : null);
  if (!format) return [];
  const tag = format === 'task_manager' ? 'task' : 'game_task';
  const childText = function (el, name) {
    const c = Array.from(el.childNodes).find(function (x) { return x.nodeType === 1 && x.tagName === name; });
    return c ? String(c.textContent || '').trim() : '';
  };
  return Array.from(root.getElementsByTagName(tag)).map(function (gt) {
    const objectives = Array.from(gt.childNodes).filter(function (x) { return x.nodeType === 1 && x.tagName === 'objective'; }).map(function (obj) {
      const hasElementChildren = Array.from(obj.childNodes).some(function (x) { return x.nodeType === 1; });
      const t = childText(obj, 'text');
      return {
        text: t || (hasElementChildren ? '' : String(obj.textContent || '').trim()),
        icon: childText(obj, 'icon'),
        infoportion: childText(obj, 'infoportion'),
        article: childText(obj, 'article')
      };
    }).filter(function (o) { return o.text || o.icon || o.infoportion || o.article; });
    let complete = '', fail = '';
    Array.from(gt.childNodes).filter(function (x) { return x.nodeType === 1 && x.tagName === 'infoportion'; }).forEach(function (ip) {
      const txt = String(ip.textContent || '').trim();
      const pick = function (av) { return (av && av !== 'true') ? av : txt; };
      if (ip.hasAttribute('set_complete')) complete = pick(ip.getAttribute('set_complete'));
      if (ip.hasAttribute('set_fail')) fail = pick(ip.getAttribute('set_fail'));
    });
    return {
      id: gt.getAttribute('id') || '',
      prior: gt.getAttribute('prior') || gt.getAttribute('prio') || '',
      title: childText(gt, 'title') || childText(gt, 'name'),
      text: childText(gt, 'text') || childText(gt, 'brief'),
      objectives: objectives,
      infoportion_complete: complete,
      infoportion_fail: fail,
      icon: childText(gt, 'icon'),
      location: childText(gt, 'location'),
      article: childText(gt, 'article'),
      category: '',
      requires: [],
      reward: '',
      format: format
    };
  }).filter(function (q) { return q.id; });
}

// Разбор task_manager.ltx (ЗП): секция = квест (#37).
export function parseTaskManagerLtx(text) {
  const parsed = parseLtx(text);
  return parsed.sections.filter(function (s) { return s.name && s.name.toLowerCase() !== 'task_manager'; }).map(function (s) {
    const get = function (key) { const e = s.entries.find(function (x) { return x.key === key; }); return e ? e.value : ''; };
    const condlist = s.entries.filter(function (x) { return /^condlist_/.test(x.key); }).map(function (x) { return x.value; });
    const reward = get('reward_money') ? ('money:' + get('reward_money')) : (get('reward_item') || '');
    return {
      id: s.name,
      prior: get('prior'),
      title: get('title'),
      text: get('descr') || get('description'),
      objectives: [{ text: get('target'), icon: '', infoportion: '', article: '' }],
      infoportion_complete: '',
      infoportion_fail: '',
      icon: get('icon'),
      location: '',
      article: '',
      category: '',
      requires: [],
      reward: reward,
      condlist: condlist,
      format: 'task_manager'
    };
  }).filter(function (q) { return q.id; });
}
export function questsToXml(quests, encoding) {
  const enc = encoding === 'utf-8' ? 'utf-8' : 'windows-1251';
  const L = [];
  L.push('<?xml version="1.0" encoding="' + enc + '"?>');
  const games = (quests || []).filter(function (q) { return q.format !== 'task_manager'; });
  const managers = (quests || []).filter(function (q) { return q.format === 'task_manager'; });
  if (games.length) {
    L.push('<game_tasks>');
    games.forEach(function (q) {
      L.push('\t<game_task id="' + escapeXml(q.id) + '"' + (q.prior ? ' prior="' + escapeXml(q.prior) + '"' : '') + '>');
      if (q.title) L.push('\t\t<title>' + escapeXml(q.title) + '</title>');
      if (q.text) L.push('\t\t<text>' + escapeXml(q.text) + '</text>');
      if (q.icon) L.push('\t\t<icon>' + escapeXml(q.icon) + '</icon>');
      (q.objectives || []).forEach(function (o) {
        const txt = typeof o === 'string' ? o : (o && o.text);
        const ic = (o && o.icon) || '', ip = (o && o.infoportion) || '', art = (o && o.article) || '';
        if (ic || ip || art) {
          L.push('\t\t<objective>');
          if (txt) L.push('\t\t\t<text>' + escapeXml(txt) + '</text>');
          if (ic) L.push('\t\t\t<icon>' + escapeXml(ic) + '</icon>');
          if (ip) L.push('\t\t\t<infoportion>' + escapeXml(ip) + '</infoportion>');
          if (art) L.push('\t\t\t<article>' + escapeXml(art) + '</article>');
          L.push('\t\t</objective>');
        } else if (txt) {
          L.push('\t\t<objective><text>' + escapeXml(txt) + '</text></objective>');
        }
      });
      if (q.infoportion_complete) L.push('\t\t<infoportion set_complete>' + escapeXml(q.infoportion_complete) + '</infoportion>');
      if (q.infoportion_fail) L.push('\t\t<infoportion set_fail>' + escapeXml(q.infoportion_fail) + '</infoportion>');
      if (q.location) L.push('\t\t<location>' + escapeXml(q.location) + '</location>');
      if (q.article) L.push('\t\t<article>' + escapeXml(q.article) + '</article>');
      if (q.reward) L.push('\t\t<reward>' + escapeXml(q.reward) + '</reward>');
      L.push('\t</game_task>');
    });
    L.push('</game_tasks>');
  }
  if (managers.length) {
    L.push('<task_manager>');
    managers.forEach(function (q) {
      L.push('\t<task id="' + escapeXml(q.id) + '"' + (q.prior ? ' prior="' + escapeXml(q.prior) + '"' : '') + '>');
      if (q.title) L.push('\t\t<title>' + escapeXml(q.title) + '</title>');
      if (q.text) L.push('\t\t<brief>' + escapeXml(q.text) + '</brief>');
      if (q.icon) L.push('\t\t<icon>' + escapeXml(q.icon) + '</icon>');
      (q.objectives || []).forEach(function (o) { const txt = typeof o === 'string' ? o : (o && o.text); if (txt) L.push('\t\t<objective><text>' + escapeXml(txt) + '</text></objective>'); });
      if (q.reward) L.push('\t\t<reward>' + escapeXml(q.reward) + '</reward>');
      L.push('\t</task>');
    });
    L.push('</task_manager>');
  }
  return L.join(NL);
}
export function taskManagerLtx(quests) {
  const list = (quests || []).filter(function (q) { return q.format === 'task_manager'; });
  if (!list.length) return '';
  const L = [];
  L.push('[task_manager]');
  L.push('task = ' + list.map(function (q) { return q.id; }).join(', '));
  list.forEach(function (q) {
    L.push('');
    L.push('[' + q.id + ']');
    L.push('type = single');
    if (q.prior) L.push('prior = ' + q.prior);
  });
  return L.join(NL);
}
export function validateQuests(quests, languages) {
  const issues = [];
  const seen = {};
  const langs = Array.isArray(languages) ? languages : [];
  const hasEntry = function (id) {
    if (!id) return false;
    for (let i = 0; i < langs.length; i++) { if ((langs[i].entries || []).some(function (e) { return e.id === id && e.text; })) return true; }
    return false;
  };
  (quests || []).forEach(function (q) {
    if (seen[q.id]) issues.push({ quest: q.id, severity: 'error', msg: 'Дубликат id: ' + q.id });
    seen[q.id] = true;
    if (!String(q.title || '').trim()) issues.push({ quest: q.id, severity: 'warning', msg: 'Пустой заголовок (title)' });
    if (!String(q.text || '').trim()) issues.push({ quest: q.id, severity: 'warning', msg: 'Пустое описание (text)' });
    if (!(q.objectives || []).length) issues.push({ quest: q.id, severity: 'warning', msg: 'Нет целей' });
    if (q.prior && !/^\d+$/.test(String(q.prior).trim())) issues.push({ quest: q.id, severity: 'error', msg: 'Приоритет не число: ' + q.prior });
    if (q.title && !hasEntry(q.title)) issues.push({ quest: q.id, severity: 'warning', msg: 'Нет перевода title: ' + q.title });
    if (q.text && !hasEntry(q.text)) issues.push({ quest: q.id, severity: 'warning', msg: 'Нет перевода text: ' + q.text });
    (q.objectives || []).forEach(function (o) { const t = typeof o === 'string' ? o : (o && o.text); if (t && !hasEntry(t)) issues.push({ quest: q.id, severity: 'warning', msg: 'Нет перевода цели: ' + t }); });
  });
  return issues;
}
export function computeQuestChains(quests) {
  const list = quests || [];
  const completeTo = {};
  list.forEach(function (q) { if (q.infoportion_complete) completeTo[q.infoportion_complete] = q.id; });
  const parentOf = {};
  list.forEach(function (q) { (q.requires || []).forEach(function (r) { const p = completeTo[r]; if (p && p !== q.id) parentOf[q.id] = p; }); });
  const roots = list.filter(function (q) { return !parentOf[q.id]; }).map(function (q) { return q.id; });
  const children = {};
  Object.keys(parentOf).forEach(function (cid) { (children[parentOf[cid]] = children[parentOf[cid]] || []).push(cid); });
  const chains = [];
  const walk = function (id, acc) {
    const path = acc.concat([id]);
    const kids = children[id] || [];
    if (!kids.length) { chains.push(path); return; }
    kids.forEach(function (k) { walk(k, path); });
  };
  roots.forEach(function (r) { walk(r, []); });
  return chains;
}
