// Работа с XML и локализациями: string_table, персонажи, диалоги, определение локали.
// Зависимости: normalizePath и DOM-парсеры из core.js; flattenDialogues/safeNode/layoutDialogue из graph.js.

import { DOMParser } from '@xmldom/xmldom';
import { normalizePath, escapeText, escapeAttribute, parseCharactersXml as domParseCharactersXml, parseDialogueXml as domParseDialogueXml } from './core.js';
import { flattenDialogues, safeNode, layoutDialogue } from './graph.js';

const Q = '"';

export function xmlAttr(attrs, name) {
  const a = String(attrs || '');
  const key = name + '=';
  const i = a.indexOf(key);
  if (i === -1) return '';
  const q1 = a.indexOf(Q, i + key.length);
  if (q1 === -1) return '';
  const q2 = a.indexOf(Q, q1 + 1);
  if (q2 === -1) return '';
  return a.slice(q1 + 1, q2);
}

export function xmlTag(body, tag) {
  const b = String(body || '');
  const open = b.indexOf('<' + tag);
  if (open === -1) return '';
  const gt = b.indexOf('>', open);
  if (gt === -1) return '';
  const close = b.indexOf('</' + tag + '>', gt);
  if (close === -1) return '';
  return b.slice(gt + 1, close).trim();
}

export function parseCharactersXml(text) {
  return domParseCharactersXml(text, DOMParser);
}
export function parseGeneralCharactersXml(text) {
  const d = new DOMParser().parseFromString(String(text), 'application/xml');
  if (d.getElementsByTagName('parsererror').length) return [];
  const root = d.documentElement;
  if (!root || root.tagName !== 'character_desc_general') return [];
  const out = [];
  Array.from(root.getElementsByTagName('community')).forEach(function (c) { const id = c.getAttribute('id'); if (id && out.indexOf(id) === -1) out.push(id); });
  Array.from(root.getElementsByTagName('faction')).forEach(function (c) { const id = c.getAttribute('id'); if (id && out.indexOf(id) === -1) out.push(id); });
  return out;
}
export const LOCALE_NAMES = { rus: 'Русский', eng: 'English', ukr: 'Українська', ger: 'Deutsch', deu: 'Deutsch', fra: 'Français', fre: 'Français', ita: 'Italiano', spa: 'Español', esp: 'Español', pol: 'Polski', cz: 'Čeština', czc: 'Čeština', tur: 'Türkçe', jap: '日本語' };
export function detectLocale(path) {
  const parts = normalizePath(path).split('/');
  const textIndex = parts.findIndex(function (x) { return x.toLowerCase() === 'text'; });
  if (textIndex >= 0 && parts[textIndex + 1]) return parts[textIndex + 1].toLowerCase();
  const base = ((parts.pop() || '').split('.')[0] || '').toLowerCase();
  const m = base.match(/(?:^|[_\-])(rus|eng|ukr|ger|deu|fra|fre|ita|spa|esp|pol|cz|czc|tur|jap)$/);
  return m ? m[1] : 'unknown';
}
export function mergeLanguages(existing, incoming) {
  const out = [];
  const byLocale = new Map();
  (Array.isArray(existing) ? existing : []).forEach(function (l) {
    if (!l || typeof l !== 'object') return;
    const locale = String(l.locale || '').toLowerCase();
    const lang = { locale: locale, name: String(l.name || ''), entries: (Array.isArray(l.entries) ? l.entries : []).filter(function (e) { return e && e.id; }).map(function (e) { return { id: String(e.id), text: String(e.text || '') }; }) };
    out.push(lang);
    if (locale) byLocale.set(locale, lang);
  });
  (Array.isArray(incoming) ? incoming : []).forEach(function (inc) {
    if (!inc || typeof inc !== 'object') return;
    const locale = String(inc.locale || '').toLowerCase();
    if (!locale) return;
    let lang = byLocale.get(locale);
    if (!lang) {
      lang = { locale: locale, name: String(inc.name || LOCALE_NAMES[locale] || locale.toUpperCase()), entries: [] };
      byLocale.set(locale, lang);
      out.push(lang);
    } else if (!lang.name && inc.name) {
      lang.name = String(inc.name);
    }
    const ids = new Set(lang.entries.map(function (e) { return e.id; }));
    (Array.isArray(inc.entries) ? inc.entries : []).forEach(function (e) {
      if (e && e.id && !ids.has(String(e.id))) { lang.entries.push({ id: String(e.id), text: String(e.text || '') }); ids.add(String(e.id)); }
    });
  });
  return out;
}
export function parseStringTableXml(text, path) {
  let src = String(text);
  let d = new DOMParser().parseFromString(src, 'application/xml');
  let root = d.documentElement;
  if (d.getElementsByTagName('parsererror').length || !root || root.tagName !== 'string_table') {
    // фрагмент без <string_table> — обернуть, только если реально есть <string>
    if (!/<string[\s>]/.test(src)) return null;
    d = new DOMParser().parseFromString('<string_table>' + src + '</string_table>', 'application/xml');
    if (d.getElementsByTagName('parsererror').length) return null;
    root = d.documentElement;
  }
  if (!root || root.tagName !== 'string_table') return null;
  const entries=Array.from(root.getElementsByTagName('string')).map(function(s){var t=Array.from(s.childNodes).find(function(x){return x.nodeType===1&&x.tagName==='text';});return {id:s.getAttribute('id')||'',text:t?(t.textContent||''):''};}).filter(function(x){return x.id;});
  const locale=detectLocale(path);
  return {locale:locale,name:LOCALE_NAMES[locale]||locale.toUpperCase(),path:normalizePath(path),entries:entries};
}
export function parseDialogueXml(text) {
  const parsed = domParseDialogueXml(text, DOMParser);
  return flattenDialogues(parsed).map(function (d) {
    d.nodes = d.nodes.map(safeNode);
    d.nodes.forEach(function (n) { if (n.type === 'PHRASE' && !n.data.roleSource) { n.data.role = 'unknown'; n.data.roleSource = 'unknown'; } });
    layoutDialogue(d.nodes, d.edges.filter(function (e) { return !e.broken; }));
    return d;
  });
}

// Полный парсинг character_desc_general.xml: сообщества с отношениями (#9).
export function parseCharacterDescGeneral(text) {
  const d = new DOMParser().parseFromString(String(text), 'application/xml');
  if (d.getElementsByTagName('parsererror').length) return null;
  const root = d.documentElement;
  if (!root || root.tagName !== 'character_desc_general') return null;
  const communities = [];
  Array.from(root.getElementsByTagName('community')).forEach(function (c) {
    const id = c.getAttribute('id');
    if (!id) return;
    const childText = function (tag) { const el = Array.from(c.childNodes).find(function (x) { return x.nodeType === 1 && x.tagName === tag; }); return el ? (el.textContent || '').trim() : ''; };
    const relations = [];
    Array.from(c.childNodes).filter(function (x) { return x.nodeType === 1 && x.tagName === 'relation'; }).forEach(function (r) {
      relations.push({ community: r.getAttribute('community') || '', value: (r.textContent || '').trim() });
    });
    communities.push({ id: id, name: childText('name') || c.getAttribute('name') || '', icon: childText('icon') || c.getAttribute('icon') || '', relations: relations });
  });
  return { communities: communities };
}

// Сериализация обратно в XML (#9).
export function characterDescGeneralToXml(data) {
  const L = [];
  L.push('<?xml version="1.0" encoding="windows-1251"?>');
  L.push('<character_desc_general>');
  L.push('\t<communities>');
  (data.communities || []).forEach(function (c) {
    L.push('\t\t<community id="' + escapeAttribute(c.id) + '">');
    if (c.name) L.push('\t\t\t<name>' + escapeText(c.name) + '</name>');
    if (c.icon) L.push('\t\t\t<icon>' + escapeText(c.icon) + '</icon>');
    (c.relations || []).forEach(function (r) { L.push('\t\t\t<relation community="' + escapeAttribute(r.community) + '">' + escapeText(r.value) + '</relation>'); });
    L.push('\t\t</community>');
  });
  L.push('\t</communities>');
  L.push('</character_desc_general>');
  return L.join('\n');
}

// === info_portions.xml (#60, #61) ===
export function parseInfoPortions(text) {
  const d = new DOMParser().parseFromString(String(text), 'application/xml');
  if (d.getElementsByTagName('parsererror').length) return [];
  const root = d.documentElement;
  if (!root || root.tagName !== 'game_information_portions') return [];
  const kids = function (el, tag) { return Array.from(el.childNodes).filter(function (x) { return x.nodeType === 1 && x.tagName === tag; }).map(function (x) { return (x.textContent || '').trim(); }); };
  return Array.from(root.getElementsByTagName('info_portion')).map(function (ip) {
    return {
      id: ip.getAttribute('id') || '',
      tasks: kids(ip, 'task'),
      dialogs: kids(ip, 'dialog'),
      disables: kids(ip, 'disable'),
      articles: kids(ip, 'article')
    };
  }).filter(function (x) { return x.id; });
}

// === encyclopedia_*.xml (#63) ===
export function parseEncyclopedia(text) {
  const d = new DOMParser().parseFromString(String(text), 'application/xml');
  if (d.getElementsByTagName('parsererror').length) return [];
  const root = d.documentElement;
  if (!root || root.tagName !== 'encyclopedia') return [];
  const childText = function (el, tag) { const c = Array.from(el.childNodes).find(function (x) { return x.nodeType === 1 && x.tagName === tag; }); return c ? (c.textContent || '').trim() : ''; };
  return Array.from(root.getElementsByTagName('article')).map(function (a) {
    return { id: a.getAttribute('id') || '', name: a.getAttribute('name') || '', group: a.getAttribute('group') || '', ltx: childText(a, 'ltx'), text: childText(a, 'text') };
  }).filter(function (x) { return x.id; });
}

