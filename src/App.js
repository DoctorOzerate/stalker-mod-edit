import React, { useEffect, useMemo, useRef, useState } from 'react';
import { zipSync, strToU8 } from 'fflate';
import { normalizePath as coreNormalizePath, cp1251Encode, decodeGameBytes } from './lib/core.js';
import { QUEST_CATEGORIES, QUEST_TEMPLATES, normalizeQuestObjectives, parseGameTasksXml, parseTaskManagerLtx, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, questChainSvg, parseCondlistEntry, condlistEntryToStr } from './lib/quests.js';
import { tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, luaCheck, luaUndeclaredGlobals, LUA_API, LUA_SNIPPETS, LUA_ENGINE_GLOBALS, luaCallGraph, luaUnused, luaCompletions, validateScriptBindings } from './lib/lua.js';
import { decodeDDS, pixelsToDataURL, encodeDds } from './lib/dds.js';
import { splitLines, parseLtx, resolveIncludes, updateLtxLine, removeSection, addSection, addFieldToSection, removeFieldByLine, isValidGridValue, isValidGridWH, iconCoords, ATLAS_PRESETS, isFileLoaded, findIncludeSuggestion, itemFileCategory, isItemFile, mergedItemEntries, includeTreeRows, includeGraphSvg, ITEM_CATEGORIES, COMMUNITY_ICONS, classifyItem, validateLtxRefs, findDuplicateSections, findInheritanceCycles, renameSection, replaceFieldValues, ITEM_FIELD_PROFILES, itemFieldProfile, parseGameRelations, parseRatingScales, parseSquadDescr, parseSecrets, parseSpawnSections, parseMonsters, parseActor, parseSurgeManager, parseTrade, highlightLtx, ltxInheritanceTree, FIELD_DESCRIPTIONS } from './lib/ltx.js';
import { NODE_TYPES, PALETTE_HIDDEN, META_KEYS, FALLBACK_NODE, safeNode, flattenDialogues, validateGraph, layoutDialogue, nodeLabel, roleColor, dialogueStats, suggestTextId, validateMetadataSyntax, dialogueGroup, DIALOGUE_GROUPS, graphToSvg } from './lib/graph.js';
import { gameFilesFromDialogues, validateTextIds, dialogueToXml, findTextIdIssues, resolveTextId } from './lib/dialogs.js';
import { analyzeSpawn } from './lib/spawn.js';
import { xmlAttr, xmlTag, parseCharactersXml, parseGeneralCharactersXml, parseCharacterDescGeneral, characterDescGeneralToXml, LOCALE_NAMES, detectLocale, mergeLanguages, parseStringTableXml, parseDialogueXml, parseInfoPortions, parseEncyclopedia, parseNpcProfile, parseTextureDescriptor } from './lib/xml.js';
import { createHistory } from './lib/history.js';
import { tr } from './lib/i18n.js';
import { loadPlugins, savePlugins, runPluginExporter } from './lib/plugins.js';
import { renderDialoguesTab } from './tabs/DialoguesTab.js';
import { renderCharactersTab } from './tabs/CharactersTab.js';
import { renderItemsTab } from './tabs/ItemsTab.js';
import { renderIconsTab } from './tabs/IconsTab.js';
import { renderScriptsTab } from './tabs/ScriptsTab.js';
import { renderSystemTab } from './tabs/SystemTab.js';
import { renderLanguagesTab } from './tabs/LanguagesTab.js';
import { renderQuestsTab } from './tabs/QuestsTab.js';

const NL = String.fromCharCode(10);
const Q = String.fromCharCode(34);
const BS = String.fromCharCode(92);
const BOM = String.fromCharCode(65279);

const SCHEMA_VERSION = 3;
const STORAGE_KEY = 'stalker_editor_current_v3';
const PROJECTS_KEY = 'stalker_editor_projects_v1';
const GAMEDATA_KEY = 'xray_forge_gamedata_v1';
const PROGRAM_SETTINGS_KEY = 'xray_forge_program_settings_v1';
const BACKUPS_KEY = 'xray_forge_backups_v1';

// Общие истории отмены/повтора для разных доменов (чистые функции, лимит снапшотов).
const graphHistory = createHistory(50);
const iconsHistory = createHistory(100);
const itemsHistory = createHistory(50);
const questsHistory = createHistory(50);

// Пресеты акцентного цвета интерфейса: [основной, hover].
const ACCENT_PRESETS = {
  green: ['#16a34a', '#15803d'],
  blue: ['#2563eb', '#1d4ed8'],
  orange: ['#f97316', '#ea580c'],
  purple: ['#7c3aed', '#6d28d9'],
  red: ['#dc2626', '#b91c1c'],
};

// Реестр горячих клавиш: id, подпись и комбинация по умолчанию.
// Формат: 'mod+s', 'shift+z', 'F5', 'Delete' (mod = Ctrl/Cmd).
const HOTKEY_ACTIONS = [
  { id: 'save', label: 'Сохранить', def: 'mod+s' },
  { id: 'exportXml', label: 'XML редактора', def: 'mod+e' },
  { id: 'globalSearch', label: 'Глобальный поиск', def: 'mod+k' },
  { id: 'undo', label: 'Отменить', def: 'mod+z' },
  { id: 'redo', label: 'Повторить', def: 'mod+y' },
  { id: 'addStart', label: 'Добавить Старт', def: '1' },
  { id: 'addPhrase', label: 'Добавить Реплику', def: '2' },
  { id: 'addRandom', label: 'Добавить Случайный', def: '3' },
  { id: 'addEnd', label: 'Добавить Конец', def: '4' },
  { id: 'deleteSelected', label: 'Удалить выбранное', def: 'Delete' },
  { id: 'escape', label: 'Отмена/закрыть', def: 'Escape' },
];
function defaultHotkeys() {
  const o = {}; HOTKEY_ACTIONS.forEach(function (a) { o[a.id] = a.def; }); return o;
}
function comboMatches(e, combo) {
  const parts = String(combo || '').toLowerCase().split('+').map(function (s) { return s.trim(); });
  const key = parts[parts.length - 1];
  if (!key) return false;
  const wantMod = parts.indexOf('mod') !== -1 || parts.indexOf('ctrl') !== -1 || parts.indexOf('cmd') !== -1;
  const wantShift = parts.indexOf('shift') !== -1;
  const wantAlt = parts.indexOf('alt') !== -1;
  if (wantMod !== (e.ctrlKey || e.metaKey)) return false;
  if (wantShift !== !!e.shiftKey) return false;
  if (wantAlt !== !!e.altKey) return false;
  return e.key.toLowerCase() === key.toLowerCase();
}


const SERIES = [
  { value: 'soc', label: 'SoC · Тень Чернобыля', cfg: 'config' },
  { value: 'cs', label: 'CS · Чистое Небо', cfg: 'config' },
  { value: 'cop', label: 'CoP · Зов Припяти', cfg: 'configs' }
];

function emptyProject() {
  return {
    version: SCHEMA_VERSION, projectName: 'new_dialogue', dialogueId: 'new_dialogue', series: 'soc', theme: 'dark',
    gamedataPath: '', resourcesPath: '', nodes: [], edges: [], characters: [], dialogues: [], quests: [], generalCommunities: [],
    itemsLtx: '', scripts: [], systemLtx: '', languages: [], atlas: { columns: 5, cellSize: 50, cellHeight: 50 },
    ltxFiles: {}, textures: [], itemCategories: {}, settings: { importEncoding: 'auto', exportEncoding: 'windows-1251', showNodeText: true, autosaveInterval: 60 },
    itemFiles: {}, itemOrigins: {}, equipSlots: {}, pinnedIcons: [], pinCollections: { default: [] }
  };
}

function escapeXml(v) { return String(v == null ? '' : v).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g,'&quot;').replace(/'/g,'&apos;'); }

function csvCell(v) {
  const s = String(v == null ? '' : v);
  if (new RegExp('[' + Q + BS + 'n,;]').test(s)) return Q + s.replace(new RegExp(Q, 'g'), Q + Q) + Q;
  return s;
}

function download(text, file, mime, bom) {
  const content = (bom ? BOM : '') + text;
  const blob = new Blob([content], { type: (mime || 'text/plain') + ';charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url; a.download = file;
  document.body.appendChild(a); a.click(); a.remove();
  setTimeout(function () { URL.revokeObjectURL(url); }, 1200);
}

function downloadDataUrl(dataUrl, file) {
  const a = document.createElement('a');
  a.href = dataUrl; a.download = file;
  document.body.appendChild(a); a.click(); a.remove();
}

function normalizePath(p) { return coreNormalizePath(p); }
function decodeTextSafely(buffer, encoding) {
  const bytes = new Uint8Array(buffer || new ArrayBuffer(0));
  const requested = String(encoding || 'auto').toLowerCase();
  const cp = function () { return new TextDecoder('windows-1251').decode(bytes); };
  if (requested === 'windows-1251' || requested === 'cp1251') return cp();
  if (requested === 'utf-16le') return new TextDecoder('utf-16le').decode(bytes);
  if (bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf) return new TextDecoder('utf-8').decode(bytes);
  if (bytes[0] === 0xff && bytes[1] === 0xfe) return new TextDecoder('utf-16le').decode(bytes);
  if (requested === 'utf-8') return new TextDecoder('utf-8').decode(bytes);
  if (requested === 'auto') {
    const head = new TextDecoder('latin1').decode(bytes.slice(0, 300));
    const m = head.match(/<\?xml[^>]*encoding\s*=\s*["']([^"']+)/i);
    if (m) {
      const enc = m[1].toLowerCase();
      if (enc === 'utf-8') return new TextDecoder('utf-8').decode(bytes);
      if (enc === 'utf-16' || enc === 'utf-16le') return new TextDecoder('utf-16le').decode(bytes);
      return cp();
    }
  }
  try { return new TextDecoder('utf-8', { fatal: true }).decode(bytes); }
  catch (e) { return cp(); }
}
async function readGameText(file, options) {
  const opt = options || {};
  return decodeTextSafely(await file.arrayBuffer(), opt.encoding || 'auto');
}
function textBytes(text, encoding) {
  return encoding === 'windows-1251' ? cp1251Encode(String(text || '')) : strToU8(String(text || ''));
}
function downloadBytes(bytes, file, mime) {
  const blob = new Blob([bytes], { type: mime || 'application/octet-stream' });
  const url = URL.createObjectURL(blob), a = document.createElement('a'); a.href=url; a.download=file;
  document.body.appendChild(a); a.click(); a.remove(); setTimeout(function(){URL.revokeObjectURL(url);},1200);
}

function makeNode(type, x, y) {
  return {
    id: type.toLowerCase() + '_' + Math.random().toString(36).slice(2, 8),
    type, x, y,
    data: { phraseId: '', role: 'unknown', roleSource: 'manual', text: '', text_id: '', use_id: false, voice_id: '', expression: '', action_type: '', params: '', metadata: { has_info: [], dont_has_info: [], precondition: [], give_info: [], disable_info: [], action: [] } }
  };
}

function seedDialogue() {
  const nodes = [
    Object.assign(makeNode('START', 60, 180), { id: 'start' }),
    Object.assign(makeNode('PHRASE', 340, 150), { id: 'npc_hi', data: { text: 'Ну что, сталкер, принёс артефакт?', voice_id: '' } }),
    Object.assign(makeNode('PHRASE', 650, 80), { id: 'player_yes', data: { text: 'Да, вот он.' } }),
    Object.assign(makeNode('PHRASE', 650, 240), { id: 'player_no', data: { text: 'Нет, я ещё вернусь.' } }),
    Object.assign(makeNode('ITEM_CHECK', 960, 80), { id: 'check_artifact', data: { expression: 'af_medusa' } }),
    Object.assign(makeNode('ACTION', 1260, 80), { id: 'reward', data: { action_type: 'give_item', params: 'medkit' } }),
    Object.assign(makeNode('PHRASE', 960, 250), { id: 'bye', data: { text: 'Возвращайся, когда будет что показать.', voice_id: '' } }),
    Object.assign(makeNode('END', 1560, 150), { id: 'end' })
  ];
  const edges = [
    ['start', 'npc_hi'], ['npc_hi', 'player_yes'], ['npc_hi', 'player_no'], ['player_yes', 'check_artifact'],
    ['check_artifact', 'reward'], ['reward', 'end'], ['player_no', 'bye'], ['bye', 'end']
  ].map(function (p) { return { id: 'e_' + p[0] + '_' + p[1], source: p[0], target: p[1] }; });
  return { nodes: nodes, edges: edges };
}

function toXml(projectName, series, nodes, edges) {
  const payload = JSON.stringify({ schema:'x-ray-forge/internal-xml-v1', projectName:projectName, series:series, nodes:nodes, edges:edges });
  return '<?xml version="1.0" encoding="UTF-8"?>' + NL + '<dialogue_editor schema="internal-xml-v1"><payload>' + escapeXml(payload) + '</payload><nodes count="' + nodes.length + '"/></dialogue_editor>' + NL;
}
function buildGameFiles(projectName, series, nodes, edges, encoding, languages) {
  const errs=validateGraph(nodes,edges).filter(function(x){return x.severity==='error';});
  if(errs.length) throw new Error('Игровой экспорт: исправьте битые связи');
  const safe=String(projectName||'dialogue').replace(/[^a-zA-Z0-9_-]/g,'_')||'dialogue';
  const real=nodes.filter(function(n){return n.type==='PHRASE';});
  const idOf=new Map(), used=new Set();
  real.forEach(function(n,i){let id=String((n.data||{}).phraseId||i);let base=id,k=1;while(used.has(id))id=base+'_'+k++;used.add(id);idOf.set(n.id,id)});
  const out={}; edges.filter(function(e){return !e.broken&&idOf.has(e.source)&&idOf.has(e.target)}).forEach(function(e){(out[e.source]=out[e.source]||[]).push(idOf.get(e.target))});
  const tags=function(d){const m=d.metadata||d.xray?.metadata||d.xray||{};return ['has_info','dont_has_info','precondition','give_info','disable_info','action'].map(function(k){return (Array.isArray(m[k])?m[k]:m[k]?[m[k]]:[]).map(function(v){return '<'+k+'>'+escapeXml(v)+'</'+k+'>'}).join('')}).join('')};
  const phrases=real.map(function(n){const d=n.data||{}, id=idOf.get(n.id), t=d.use_id!==false?(d.text_id||''):d.text_id||'';return '<phrase id="'+escapeXml(id)+'"><text>'+escapeXml(t)+'</text>'+(d.voice_id?'<sound>'+escapeXml(d.voice_id)+'</sound>':'')+tags(d)+(out[n.id]||[]).map(x=>'<next>'+escapeXml(x)+'</next>').join('')+'</phrase>'}).join('');
  const declaration=encoding==='windows-1251'?'windows-1251':'UTF-8';
  const xml='<?xml version="1.0" encoding="'+declaration+'"?>\n<game_dialogs><dialog id="'+escapeXml(safe)+'"><phrase_list>'+phrases+'</phrase_list></dialog></game_dialogs>\n';
  const cfg=(SERIES.find(function(s){return s.value===series;})||SERIES[0]).cfg, data={};
  const bytes=function(s){return textBytes(s, encoding);};
  data['gamedata/'+cfg+'/gameplay/dialogs_'+safe+'.xml']=bytes(xml);
  const langs=Array.isArray(languages)&&languages.length?languages:[{locale:'rus',name:'Русский',entries:[]}];
  langs.forEach(function(lang){const entries=Array.isArray(lang.entries)?lang.entries:[];const byId=new Map(entries.map(x=>[x.id,x.text]));real.forEach(function(n){const d=n.data||{};if(d.text_id&&!byId.has(d.text_id)&&d.text)byId.set(d.text_id,d.text)});const table='<?xml version="1.0" encoding="'+declaration+'"?>\n<string_table>'+Array.from(byId).map(function(x){return '<string id="'+escapeXml(x[0])+'"><text>'+escapeXml(x[1])+'</text></string>'}).join('')+'</string_table>\n';data['gamedata/'+cfg+'/text/'+(lang.locale||'rus')+'/st_'+safe+'.xml']=bytes(table)});
  return data;
}
function gamePackage(projectName, series, nodes, edges, encoding, languages) {
  return zipSync(buildGameFiles(projectName, series, nodes, edges, encoding, languages));
}
function exportSpritesheet(pins, tex, cellW, cellH) {
  if (!pins || !pins.length || !tex) return;
  const cw = cellW || 50, ch = cellH || 50, gap = 2;
  const w = pins.reduce(function (a, p) { return a + (p.w || 1) * cw + gap; }, gap);
  const h = Math.max.apply(null, pins.map(function (p) { return (p.h || 1) * ch; }));
  const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
  const ctx = cv.getContext('2d'); const img = new Image();
  img.onload = function () {
    let x = gap;
    pins.forEach(function (p) { const pw = (p.w || 1) * cw, ph = (p.h || 1) * ch; ctx.drawImage(img, p.x * cw, p.y * ch, pw, ph, x, 0, pw, ph); x += pw + gap; });
    downloadDataUrl(cv.toDataURL('image/png'), 'icons_sheet.png');
  };
  img.src = tex.dataURL;
}

function runSelfCheck() {
  const out = [];
  const t = function (name, ok, detail) { out.push({ name: name, ok: !!ok, detail: detail || '' }); };
  const sample = '[medkit]' + NL + 'inv_name = аптечка' + NL + 'inv_weight = 0.25' + NL + '; comment' + NL + 'inv_grid_x = 3' + NL;
  const p = parseLtx(sample);
  t('Парсинг LTX (секции)', p.sections.length === 1 && p.sections[0].name === 'medkit', 'Секций: ' + p.sections.length);
  t('Парсинг LTX (ключ=значение)', p.sections[0].entries.length === 3, 'Записей: ' + p.sections[0].entries.length);
  t('Парсинг LTX (дубликаты)', (function () { const d = parseLtx('[a]' + NL + 'x = 1' + NL + 'x = 2'); return d.sections[0].entries.length === 2; })(), 'Дубликаты сохранены');
  const inc = parseLtx('#include ' + Q + 'a.ltx' + Q + NL + '#include ' + Q + 'b.ltx' + Q);
  t('Парсинг include', inc.includes.length === 2 && inc.includes[0].path === 'a.ltx', 'Найдено: ' + inc.includes.length);
  const files = { 'a.ltx': '#include ' + Q + 'b.ltx' + Q, 'b.ltx': '#include ' + Q + 'a.ltx' + Q };
  const res = resolveIncludes('#include ' + Q + 'a.ltx' + Q, files);
  t('Рекурсивный include (без зависания)', res.length >= 2 && res.length <= 10, 'Шагов: ' + res.length);
  t('Нормализация пути', normalizePath('a' + BS + 'b' + BS + 'c.ltx') === 'a/b/c.ltx', normalizePath('a' + BS + 'b' + BS + 'c.ltx'));
  const gn = [
    { id: 's', type: 'START' }, { id: 'a', type: 'PHRASE', data: { text: 'x' } },
    { id: 'b', type: 'PHRASE', data: { text: '' } }, { id: 'd', type: 'END' },
    { id: 'dup', type: 'END' }, { id: 'dup', type: 'END' }
  ];
  const ge = [{ id: 'e1', source: 's', target: 'a' }, { id: 'e2', source: 'a', target: 'missing' }, { id: 'e3', source: 'b', target: 'a' }];
  const verr = validateGraph(gn, ge);
  t('Валидация: дубликат ID', verr.some(function (e) { return e.code === 'dup_id'; }), '');
  t('Валидация: битая связь', verr.some(function (e) { return e.code === 'broken_edge'; }), '');
  t('Валидация: недостижимая нода', verr.some(function (e) { return e.code === 'unreachable'; }), '');
  t('Валидация: пустой текст', verr.some(function (e) { return e.code === 'empty_text'; }), '');
  const cycNodes = [{ id: 's', type: 'START' }, { id: 'a', type: 'PHRASE', data: { text: 'x' } }, { id: 'b', type: 'END' }];
  const cycEdges = [{ id: 'e1', source: 's', target: 'a' }, { id: 'e2', source: 'a', target: 'a' }, { id: 'e3', source: 'a', target: 'b' }];
  t('Валидация: импортированный цикл — предупреждение', validateGraph(cycNodes, cycEdges).some(function (e) { return e.code === 'imported_cycle' && e.severity === 'warning'; }), '');
  try { var ds=parseDialogueXml('<game_dialogs><dialog id="d"><phrase_list><phrase id="0"><text/><has_info>a</has_info><action>b</action><next>1</next></phrase><phrase id="1"><text>x</text></phrase></phrase_list></dialog><dialog id="d2"><phrase_list><phrase id="0"><text>z</text></phrase></phrase_list></dialog></game_dialogs>'); t('DOM game_dialogs / phrase_list, пустой text и metadata',ds.length===2&&ds[0].nodes.filter(function(n){return n.type==='PHRASE';}).length===2&&ds[0].nodes[1].data.metadata.has_info[0]==='a',''); } catch(e){t('DOM game_dialogs / phrase_list',false,e.message);}
  var branch=[{id:'s',type:'START'},{id:'a',type:'PHRASE'},{id:'b',type:'PHRASE'},{id:'c',type:'END'}], be=[{source:'s',target:'a'},{source:'s',target:'b'},{source:'a',target:'c'},{source:'b',target:'c'}]; t('Ветвление и слияние не являются циклом',!validateGraph(branch,be).some(function(e){return e.code==='imported_cycle';}),'');
  try { t('Windows-1251 декодирование',decodeGameBytes(Uint8Array.from([0xcf,0xf0,0xe8,0xe2,0xe5,0xf2]).buffer)==='Привет',''); } catch(e){t('Windows-1251 декодирование',false,e.message);}
  try { var lang=parseStringTableXml('<string_table><string id="a"><text>Текст</text></string></string_table>','gamedata/config/text/rus/a.xml');t('Локализация string_table',lang.locale==='rus'&&lang.entries[0].text==='Текст','');}catch(e){t('Локализация string_table',false,e.message);}
  t('Предмет: валидная сетка', isValidGridValue('3') && isValidGridWH('2'), '');
  t('Предмет: невалидная сетка', !isValidGridValue('-1') && !isValidGridValue('abc') && !isValidGridWH('0'), '');
  const c = iconCoords(3, 10, 50);
  t('Координаты иконки', c.x === 150 && c.y === 500, 'x=' + c.x + ', y=' + c.y);
  t('Экранирование XML', escapeXml('<a&>') === '&lt;a&amp;&gt;', '');
  t('Экранирование CSV', csvCell('a,b') === Q + 'a,b' + Q, '');
  t('Экранирование JSON', (function () { try { JSON.parse(JSON.stringify({ a: '<&>' + Q })); return true; } catch (e) { return false; } })(), '');
  const luaToks = tokenizeLua('local x = 1 -- note');
  t('Lua-токенизация: keyword', luaToks.some(function (tk) { return tk.cls === 'keyword' && tk.text === 'local'; }), '');
  t('Lua-токенизация: comment', luaToks.some(function (tk) { return tk.cls === 'comment' && tk.text === '-- note'; }), '');
  const br = countLuaBrackets('function foo() {} []');
  t('Lua-баланс скобок', br.open === 3 && br.close === 3, 'open=' + br.open + ', close=' + br.close);
  const ml = tokenizeLuaLines('--[[' + NL + 'заголовок' + NL + ']]' + NL + 'local x = 1');
  t('Lua-многострочный комментарий', (ml[1] && ml[1].length === 1 && ml[1][0].cls === 'comment') && (ml[3] && ml[3].some(function (tk) { return tk.cls === 'keyword' && tk.text === 'local'; })), 'строк: ' + ml.length);
  const qq = parseGameTasksXml('<game_tasks><game_task id="q1" prior="5"><title>t</title><text>x</text><objective><text>o1</text></objective><objective>o2</objective><infoportion set_complete>c</infoportion><infoportion set_fail>f</infoportion></game_task></game_tasks>');
  t('Квесты: парсинг game_tasks', qq.length === 1 && qq[0].id === 'q1' && qq[0].prior === '5' && qq[0].objectives.length === 2 && qq[0].objectives[0].text === 'o1' && qq[0].infoportion_complete === 'c' && qq[0].infoportion_fail === 'f' && qq[0].format === 'game_tasks', '');
  const qc = computeQuestChains([{ id: 'a', infoportion_complete: 'a_done', requires: [] }, { id: 'b', infoportion_complete: 'b_done', requires: ['a_done'] }, { id: 'c', infoportion_complete: 'c_done', requires: ['a_done'] }]);
  t('Квесты: цепочки', qc.length === 2 && qc[0].join(',') === 'a,b' && qc[1].join(',') === 'a,c', JSON.stringify(qc));
  t('Lua: форматирование', formatLua('function a()\nprint(1)\nend') === 'function a()\n  print(1)\nend', '');
  t('Include: подсказка', findIncludeSuggestion('misc/items.ltx', { 'config/misc/items.ltx': '' }) === 'config/misc/items.ltx', '');
  const qv = validateQuests([{ id: 'x', prior: 'abc', title: 't', text: '', objectives: [] }], [{ locale: 'rus', entries: [{ id: 't', text: 'ok' }] }]);
  t('Квесты: валидация', qv.some(function (i) { return i.msg.indexOf('Приоритет') !== -1; }) && qv.some(function (i) { return i.msg.indexOf('Пустое описание') !== -1; }), '');
  const gen = parseGeneralCharactersXml('<character_desc_general><communities><community id="stalker"/><community id="bandit"/></communities><faction id="army"/></character_desc_general>');
  t('Персонажи: character_desc_general', gen.length === 3 && gen[0] === 'stalker' && gen.indexOf('army') !== -1, gen.join(','));
  return out;
}

function loadProjects() {
  try {
    const value = JSON.parse(localStorage.getItem(PROJECTS_KEY));
    return Array.isArray(value) ? value.filter(function (x) { return x && typeof x === 'object'; }).map(function (x, i) { return { name: String(x.name || ('project_' + i)), updatedAt: Number(x.updatedAt) || 0, data: x.data && typeof x.data === 'object' ? x.data : emptyProject() }; }) : [];
  } catch (e) { return []; }
}

function migrate(p) {
  const raw = p && typeof p === 'object' && !Array.isArray(p) ? p : {};
  const base = emptyProject();
  const q = Object.assign({}, base, raw);
  q.version = SCHEMA_VERSION;
  q.dialogueId = String(raw.dialogueId || raw.projectName || 'new_dialogue');
  q.nodes = Array.isArray(raw.nodes) ? raw.nodes.map(safeNode) : [];
  q.edges = Array.isArray(raw.edges) ? raw.edges.filter(function (e) { return e && typeof e === 'object'; }).map(function (e, i) { return Object.assign({ id: 'edge_' + i }, e); }) : [];
  q.characters = Array.isArray(raw.characters) ? raw.characters.filter(function (x) { return x && typeof x === 'object'; }) : [];
  q.dialogues = flattenDialogues(raw.dialogues);
  q.languages = (Array.isArray(raw.languages) ? raw.languages : []).filter(function (x) { return x && typeof x === 'object'; }).map(function (lang, i) {
    return Object.assign({}, lang, { locale: String(lang.locale || lang.code || ('lang' + i)).toLowerCase(), name: String(lang.name || lang.locale || ('Language ' + (i + 1))), entries: (Array.isArray(lang.entries) ? lang.entries : []).filter(function (e) { return e && typeof e === 'object'; }).map(function (e) { return { id: String(e.id || ''), text: String(e.text || '') }; }) });
  });
  q.scripts = (Array.isArray(raw.scripts) ? raw.scripts : []).filter(function (x) { return x && typeof x === 'object'; }).map(function (x, i) { return { name: String(x.name || ('script_' + i + '.script')), path: String(x.path || x.name || ('script_' + i + '.script')), text: String(x.text || ''), original: String(x.original || '') }; });
  q.quests = (Array.isArray(raw.quests) ? raw.quests : []).filter(function (x) { return x && typeof x === 'object'; }).map(function (x) { return { id: String(x.id || ''), prior: String(x.prior || ''), title: String(x.title || ''), text: String(x.text || ''), objectives: normalizeQuestObjectives(x.objectives), infoportion_complete: String(x.infoportion_complete || ''), infoportion_fail: String(x.infoportion_fail || ''), icon: String(x.icon || ''), location: String(x.location || ''), article: String(x.article || ''), category: String(x.category || ''), requires: Array.isArray(x.requires) ? x.requires.map(String) : [], reward: String(x.reward || ''), format: String(x.format || 'game_tasks'), sourcePath: String(x.sourcePath || ''), original: (x.original && typeof x.original === 'object') ? x.original : null }; }).filter(function (x) { return x.id; });
  q.atlas = Object.assign({ columns: 5, cellSize: 50, cellHeight: 50 }, raw.atlas && typeof raw.atlas === 'object' ? raw.atlas : {});
  q.atlas.columns = Math.max(1, +q.atlas.columns || 5); q.atlas.cellSize = Math.max(1, +q.atlas.cellSize || 50); q.atlas.cellHeight = Math.max(1, +q.atlas.cellHeight || 50);
  q.settings = Object.assign({ importEncoding: 'auto', exportEncoding: 'windows-1251', showNodeText: true, autosaveInterval: 60 }, raw.settings && typeof raw.settings === 'object' ? raw.settings : {});
  if (['auto', 'utf-8', 'windows-1251'].indexOf(q.settings.importEncoding) < 0) q.settings.importEncoding = 'auto';
  if (['utf-8', 'windows-1251'].indexOf(q.settings.exportEncoding) < 0) q.settings.exportEncoding = 'windows-1251';
  q.ltxFiles = q.ltxFiles && typeof q.ltxFiles === 'object' && !Array.isArray(q.ltxFiles) ? q.ltxFiles : {};
  q.textures = Array.isArray(q.textures) ? q.textures : [];
  q.itemCategories = q.itemCategories && typeof q.itemCategories === 'object' && !Array.isArray(q.itemCategories) ? q.itemCategories : {};
  q.itemFiles = q.itemFiles && typeof q.itemFiles === 'object' && !Array.isArray(q.itemFiles) ? q.itemFiles : {};
  q.itemOrigins = q.itemOrigins && typeof q.itemOrigins === 'object' && !Array.isArray(q.itemOrigins) ? q.itemOrigins : {};
  q.equipSlots = q.equipSlots && typeof q.equipSlots === 'object' && !Array.isArray(q.equipSlots) ? q.equipSlots : {};
  q.pinnedIcons = Array.isArray(q.pinnedIcons) ? q.pinnedIcons : [];
  q.generalCommunities = Array.isArray(q.generalCommunities) ? q.generalCommunities.map(String) : [];
  q.pinCollections = (q.pinCollections && typeof q.pinCollections === 'object' && !Array.isArray(q.pinCollections)) ? q.pinCollections : { default: q.pinnedIcons };
  return q;
}

function loadProject() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return migrate(JSON.parse(raw));
  } catch (e) { return null; }
}

function loadProgramSettings() {
  const fallback = { theme: 'dark', autosaveInterval: 60, showNodeText: true, accent: 'green', density: 'normal', locale: 'ru', syncUrl: '', editorFontSize: 13, hotkeys: defaultHotkeys() };
  try {
    const raw = JSON.parse(localStorage.getItem(PROGRAM_SETTINGS_KEY) || '{}');
    const hk = Object.assign(defaultHotkeys(), raw.hotkeys && typeof raw.hotkeys === 'object' ? raw.hotkeys : {});
    return { theme: raw.theme || 'dark', autosaveInterval: +(raw.autosaveInterval != null ? raw.autosaveInterval : 60), showNodeText: raw.showNodeText !== false, accent: ACCENT_PRESETS[raw.accent] ? raw.accent : 'green', density: ['compact', 'normal', 'roomy'].indexOf(raw.density) >= 0 ? raw.density : 'normal', locale: raw.locale === 'en' ? 'en' : 'ru', syncUrl: String(raw.syncUrl || ''), editorFontSize: Math.min(24, Math.max(10, +raw.editorFontSize || 13)), hotkeys: hk };
  } catch (e) { return fallback; }
}
function loadBackups() {
  try { const v = JSON.parse(localStorage.getItem(BACKUPS_KEY)); return Array.isArray(v) ? v : []; } catch (e) { return []; }
}
function saveBackups(list) {
  try { localStorage.setItem(BACKUPS_KEY, JSON.stringify((list || []).slice(0, 10))); } catch (e) {}
}
// Проверка целостности проекта: битые ссылки между вкладками.
function validateProjectIntegrity(p) {
  const issues = [];
  const langIds = new Set();
  (p.languages || []).forEach(function (l) { (l.entries || []).forEach(function (e) { if (e.id) langIds.add(e.id); }); });
  const dlgIds = new Set();
  (p.dialogues || []).forEach(function (d) { if (d.id) dlgIds.add(d.id); if (d.name) dlgIds.add(d.name); });
  (p.nodes || []).forEach(function (n) { if (n.type === 'PHRASE' && n.data && n.data.text_id && !langIds.has(n.data.text_id)) issues.push({ where: 'Диалоги', msg: 'text_id без перевода: ' + n.data.text_id }); });
  (p.characters || []).forEach(function (c) {
    if (c.start_dialog && !dlgIds.has(c.start_dialog)) issues.push({ where: 'Персонажи', msg: (c.id || '?') + ': start_dialog не найден — ' + c.start_dialog });
    if (c.actor_dialog && !dlgIds.has(c.actor_dialog)) issues.push({ where: 'Персонажи', msg: (c.id || '?') + ': actor_dialog не найден — ' + c.actor_dialog });
  });
  (p.quests || []).forEach(function (q) {
    [q.title, q.text].concat((q.objectives || []).map(function (o) { return typeof o === 'string' ? o : (o && o.text); })).forEach(function (id) { if (id && !langIds.has(id)) issues.push({ where: 'Квесты', msg: (q.id || '?') + ': text_id без перевода — ' + id }); });
  });
  parseLtx(p.systemLtx || '').includes.forEach(function (x) { if (!isFileLoaded(x.path, p.ltxFiles)) issues.push({ where: 'system.ltx', msg: 'битый include: ' + x.path }); });
  return issues;
}

function gamedataOf(p) {
  return { ltxFiles: p.ltxFiles || {}, textures: p.textures || [], scripts: p.scripts || [], itemFiles: p.itemFiles || {}, itemOrigins: p.itemOrigins || {} };
}
function saveGamedataSnapshot(name, data) {
  try {
    const all = JSON.parse(localStorage.getItem(GAMEDATA_KEY) || '{}');
    if (!all || typeof all !== 'object') return;
    all[String(name)] = data;
    localStorage.setItem(GAMEDATA_KEY, JSON.stringify(all));
  } catch (e) {}
}
function loadGamedataSnapshot(name) {
  try {
    const all = JSON.parse(localStorage.getItem(GAMEDATA_KEY) || '{}');
    if (all && typeof all === 'object' && all[String(name)]) return all[String(name)];
  } catch (e) {}
  return null;
}
function removeGamedataSnapshot(name) {
  try {
    const all = JSON.parse(localStorage.getItem(GAMEDATA_KEY) || '{}');
    if (all && typeof all === 'object') { delete all[String(name)]; localStorage.setItem(GAMEDATA_KEY, JSON.stringify(all)); }
  } catch (e) {}
}
function toPersistable(p) {
  const copy = Object.assign({}, p);
  delete copy.ltxFiles;
  delete copy.textures;
  delete copy.scripts;
  delete copy.itemFiles;
  delete copy.itemOrigins;
  return copy;
}




function AtlasViewer(props) {
  const ref = useRef(null);
  const dragRef = useRef(null);
  const texture = props.texture, overlay = props.overlay, overlayOpacity = props.overlayOpacity, columns = props.columns, hover = props.hover, sel = props.sel;
  const cw = props.cellW || 50, ch = props.cellH || 50;
  const selW = props.selW || 1, selH = props.selH || 1, zoom = props.zoom || 1;
  useEffect(function () {
    const cv = ref.current; if (!cv || !texture) return;
    const ctx = cv.getContext('2d');
    let cancelled = false;
    const load = function (src) { return new Promise(function (res, rej) { const im = new Image(); im.onload = function () { res(im); }; im.onerror = rej; im.src = src; }); };
    (async function () {
      try {
        const img = await load(texture.dataURL);
        if (cancelled) return;
        cv.width = Math.round(img.width * zoom); cv.height = Math.round(img.height * zoom);
        ctx.setTransform(zoom, 0, 0, zoom, 0, 0);
        ctx.clearRect(0, 0, img.width, img.height);
        ctx.drawImage(img, 0, 0);
        if (overlay && overlay.dataURL) {
          const ov = await load(overlay.dataURL);
          if (cancelled) return;
          ctx.globalAlpha = Math.max(0, Math.min(1, +overlayOpacity || 0.5));
          ctx.drawImage(ov, 0, 0);
          ctx.globalAlpha = 1;
        }
        ctx.strokeStyle = 'rgba(255,255,255,0.5)'; ctx.lineWidth = 1;
        const rows = Math.ceil(img.height / ch);
        for (let c = 0; c <= columns; c++) { ctx.beginPath(); ctx.moveTo(c * cw + 0.5, 0); ctx.lineTo(c * cw + 0.5, img.height); ctx.stroke(); }
        for (let r = 0; r <= rows; r++) { ctx.beginPath(); ctx.moveTo(0, r * ch + 0.5); ctx.lineTo(img.width, r * ch + 0.5); ctx.stroke(); }
        if (hover) { ctx.strokeStyle = 'rgba(34,197,94,0.9)'; ctx.lineWidth = 1; ctx.strokeRect(hover.x * cw, hover.y * ch, cw, ch); }
        if (sel) { ctx.strokeStyle = '#22c55e'; ctx.lineWidth = 2; ctx.strokeRect(sel.x * cw, sel.y * ch, selW * cw, selH * ch); }
      } catch (e) {}
    })();
    return function () { cancelled = true; };
  }, [texture, overlay, overlayOpacity, columns, cw, ch, sel, selW, selH, zoom, hover]);
  const cellAt = function (e) {
    const r = ref.current.getBoundingClientRect();
    const scale = ref.current.width / r.width;
    return { x: Math.floor((e.clientX - r.left) * scale / cw), y: Math.floor((e.clientY - r.top) * scale / ch) };
  };
  return React.createElement('canvas', {
    ref: ref, className: 'max-w-full border rounded-xl cursor-crosshair',
    style: { imageRendering: 'pixelated' },
    onWheel: function (e) { if (e.ctrlKey) { e.preventDefault(); if (props.onZoom) props.onZoom(e.deltaY < 0 ? 0.25 : -0.25); } },
    onMouseDown: function (e) {
      if (e.button === 1) {
        e.preventDefault();
        const container = ref.current && ref.current.parentElement;
        dragRef.current = { kind: 'pan', cx: e.clientX, cy: e.clientY, sx: container ? container.scrollLeft : 0, sy: container ? container.scrollTop : 0 };
      } else if (e.button === 0) {
        const c = cellAt(e);
        if (c.x >= 0 && c.y >= 0 && c.x < columns) {
          dragRef.current = { kind: 'select', start: c };
          if (props.onSelect) props.onSelect(c);
          if (props.onSelectRegion) props.onSelectRegion({ x: c.x, y: c.y, w: 1, h: 1 });
        }
      }
    },
    onMouseMove: function (e) {
      const c = cellAt(e);
      const valid = c.x >= 0 && c.y >= 0 && c.x < columns;
      if (props.onHover) props.onHover(valid ? c : null);
      if (dragRef.current && dragRef.current.kind === 'pan') {
        const container = ref.current && ref.current.parentElement;
        if (container) { container.scrollLeft = dragRef.current.sx - (e.clientX - dragRef.current.cx); container.scrollTop = dragRef.current.sy - (e.clientY - dragRef.current.cy); }
      } else if (dragRef.current && dragRef.current.kind === 'select' && valid) {
        const s = dragRef.current.start;
        if (props.onSelectRegion) props.onSelectRegion({ x: Math.min(s.x, c.x), y: Math.min(s.y, c.y), w: Math.abs(s.x - c.x) + 1, h: Math.abs(s.y - c.y) + 1 });
      }
    },
    onMouseUp: function () { if (dragRef.current && dragRef.current.kind === 'select' && props.onSelectEnd) props.onSelectEnd(); dragRef.current = null; },
    onMouseLeave: function () { dragRef.current = null; if (props.onHover) props.onHover(null); }
  });
}

function extractCharacterXml(id, ltxFiles) {
  let found = null;
  Object.keys(ltxFiles || {}).forEach(function (p) {
    if (found) return;
    const txt = ltxFiles[p];
    let i = txt.indexOf('id="' + id + '"');
    if (i === -1) i = txt.indexOf("id='" + id + "'");
    if (i !== -1) {
      const start = txt.lastIndexOf('<specific_character', i);
      const end = txt.indexOf('</specific_character>', i);
      found = start !== -1 && end !== -1 ? txt.slice(start, end + '</specific_character>'.length) : txt;
    }
  });
  return found;
}

export default function App() {
  const initialProject = useMemo(function () {
    const p = loadProject() || migrate(emptyProject());
    const snap = loadGamedataSnapshot(p.projectName);
    return snap ? Object.assign({}, p, snap) : p;
  }, []);
  const [project, setProject] = useState(initialProject);
  const [projects, setProjects] = useState(loadProjects);
  const [selected, setSelected] = useState(null);
  const [tab, setTab] = useState('dialogues');
  const [welcomeOpen, setWelcomeOpen] = useState(true);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [programSettingsOpen, setProgramSettingsOpen] = useState(false);
  const [programSettings, setProgramSettings] = useState(loadProgramSettings);
  const [newName, setNewName] = useState('new_dialogue');
  const [newSeries, setNewSeries] = useState('soc');
  const [newTheme, setNewTheme] = useState('dark');
  const [loadProgress, setLoadProgress] = useState(null);
  const [metaOpen, setMetaOpen] = useState(false);
  const [lastAutosave, setLastAutosave] = useState(null);
  const [charSearch, setCharSearch] = useState('');
  const [itemSearch, setItemSearch] = useState('');
  const [scriptSearch, setScriptSearch] = useState('');
  const [scriptReplace, setScriptReplace] = useState('');
  const [scriptDiff, setScriptDiff] = useState(false);
  const [helpOpen, setHelpOpen] = useState(false);
  const [graphHist, setGraphHist] = useState({ hist: [], pos: -1 });
  const scriptTextRef = useRef(null);
  const [includePreview, setIncludePreview] = useState(null);
  const [sectionSearch, setSectionSearch] = useState('');
  const [expandedSys, setExpandedSys] = useState({});
  const [itemsHist, setItemsHist] = useState({ hist: [], pos: -1 });
  const [duplicateCells, setDuplicateCells] = useState([]);
  const [iconClipboard, setIconClipboard] = useState(null);
  const [pinColName, setPinColName] = useState('default');
  const [charXml, setCharXml] = useState(null);
  const [paletteCollapsed, setPaletteCollapsed] = useState(false);
  const [propsCollapsed, setPropsCollapsed] = useState(false);
  const [canvasFull, setCanvasFull] = useState(false);
  const [langTableMode, setLangTableMode] = useState(false);
  const [multiSelect, setMultiSelect] = useState([]);
  const [marquee, setMarquee] = useState(null);
  const marqueeRef = useRef(null);
  marqueeRef.current = marquee;
  const nodesRef = useRef(null);
  const [scriptViewMode, setScriptViewMode] = useState(true);
  const [ltxViewMode, setLtxViewMode] = useState(false);
  const [collapsedBlocks, setCollapsedBlocks] = useState({});
  const [paletteFilter, setPaletteFilter] = useState('');
  const [graphSearch, setGraphSearch] = useState('');
  const [colorByRole, setColorByRole] = useState(false);
  const [toast, setToast] = useState('');
  const [errors, setErrors] = useState([]);
  const [appErrors, setAppErrors] = useState([]);
  const [opLog, setOpLog] = useState([]);
  const [backups, setBackups] = useState(loadBackups);
  const [integrityResults, setIntegrityResults] = useState([]);
  const [systemDark, setSystemDark] = useState(function () { return typeof window !== 'undefined' && window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches; });
  const [selfResults, setSelfResults] = useState([]);
  const [pan, setPan] = useState({ x: 20, y: 20 });
  const [zoom, setZoom] = useState(1);
  const [connectFrom, setConnectFrom] = useState(null);
  const [selItem, setSelItem] = useState(null);
  const [itemCategory, setItemCategory] = useState('Все');
  const [selCell, setSelCell] = useState({ x: 0, y: 0 });
  const [selSize, setSelSize] = useState({ w: 1, h: 1 });
  const [atlasZoom, setAtlasZoom] = useState(1);
  const [hoverCell, setHoverCell] = useState(null);
  const [histState, setHistState] = useState({ hist: [], pos: -1 });
  const [compareOpacity, setCompareOpacity] = useState(0.5);
  const [selScript, setSelScript] = useState(0);
  const [selChar, setSelChar] = useState(null);
  const [selQuest, setSelQuest] = useState(null);
  const [questSearch, setQuestSearch] = useState('');
  const [questCategory, setQuestCategory] = useState('Все');
  const [questView, setQuestView] = useState('edit');
  const [questPreviewLocale, setQuestPreviewLocale] = useState('');
  const [questsHist, setQuestsHist] = useState({ hist: [], pos: -1 });
  const [activePhraseLocale, setActivePhraseLocale] = useState('');
  const canvasRef = useRef(null);
  const dragRef = useRef(null);
  const importRef = useRef(null);
  const gamedataRef = useRef(null);
  const scriptRef = useRef(null);
  const ddsRef = useRef(null);
  const compareRef = useRef(null);
  const lastGamedataRef = useRef(null);
  const autosaveRef = useRef(null);
  const [dirty, setDirty] = useState(false);
  const [globalSearchOpen, setGlobalSearchOpen] = useState(false);
  const [globalSearchQuery, setGlobalSearchQuery] = useState('');
  const [ctxMenu, setCtxMenu] = useState(null);
  const dirtyRef = useRef(false);
  dirtyRef.current = dirty;
  const firstProjectRender = useRef(true);
  const [listSort, setListSort] = useState('name');
  const [playthrough, setPlaythrough] = useState(null);
  const [toastAction, setToastAction] = useState(null);
  const [plugins, setPlugins] = useState(loadPlugins);
  const [shortcutsOpen, setShortcutsOpen] = useState(false);
  const [hoverNode, setHoverNode] = useState(null);
  const [dlgExportFiles, setDlgExportFiles] = useState({});
  const [selLtxPath, setSelLtxPath] = useState(null);
  const [spawnData, setSpawnData] = useState(null);
  const spawnRef = useRef(null);
  const stringsRef = useRef(null);
  const [unlockedRoots, setUnlockedRoots] = useState({});

  useEffect(function () {
    if (!window.matchMedia) return;
    const mq = window.matchMedia('(prefers-color-scheme: dark)');
    const fn = function (e) { setSystemDark(e.matches); };
    mq.addEventListener('change', fn);
    return function () { mq.removeEventListener('change', fn); };
  }, []);
  useEffect(function () {
    const onErr = function (e) {
      const msg = (e && e.message) ? e.message : (e && e.reason && e.reason.message) ? e.reason.message : String(e);
      console.error('App error:', e);
      setAppErrors(function (prev) { return prev.concat([{ t: Date.now(), msg: msg }]).slice(-50); });
    };
    window.addEventListener('error', onErr);
    window.addEventListener('unhandledrejection', function (e) { onErr(e && e.reason ? e.reason : e); });
    return function () { window.removeEventListener('error', onErr); window.removeEventListener('unhandledrejection', onErr); };
  }, []);
  // Плотность интерфейса: масштабируем корневой rem (не влияет на координаты холста).
  useEffect(function () {
    const d = programSettings.density || 'normal';
    document.documentElement.style.fontSize = d === 'compact' ? '14px' : d === 'roomy' ? '18px' : '';
    return function () { document.documentElement.style.fontSize = ''; };
  }, [programSettings.density]);
  // Флаг несохранённых изменений: любое изменение проекта после первой отрисовки.
  useEffect(function () {
    if (firstProjectRender.current) { firstProjectRender.current = false; return; }
    setDirty(true);
  }, [project]);
  // Подтверждение при закрытии вкладки/окна, если есть несохранённые изменения.
  useEffect(function () {
    const fn = function (e) { if (dirtyRef.current) { e.preventDefault(); e.returnValue = ''; } };
    window.addEventListener('beforeunload', fn);
    return function () { window.removeEventListener('beforeunload', fn); };
  }, []);

  const dark = programSettings.theme === 'auto' ? systemDark : programSettings.theme !== 'light';
  const t = function (key, fb) { return tr(programSettings.locale, key, fb); };
  const nodes = project.nodes || [];
  nodesRef.current = nodes;
  const edges = project.edges || [];
  const connectedSet = useMemo(function () {
    if (!selected) return null;
    const s = new Set([selected]);
    edges.forEach(function (e) { if (e.source === selected) s.add(e.target); if (e.target === selected) s.add(e.source); });
    return s;
  }, [selected, edges]);
  const selectedNode = nodes.find(function (n) { return n.id === selected; });
  const includes = useMemo(function () { return parseLtx(project.systemLtx).includes; }, [project.systemLtx]);
  const parsedItems = useMemo(function () { return parseLtx(project.itemsLtx); }, [project.itemsLtx]);
  const textures = project.textures || [];
  const atlasTex = textures[0] || null;
  const compareTex = textures[1] || null;
  const selSection = useMemo(function () { return parsedItems.sections.find(function (s) { return s.name === selItem; }) || null; }, [parsedItems, selItem]);
  const itemsByName = useMemo(function () { const m = {}; parsedItems.sections.forEach(function (s) { m[s.name.toLowerCase()] = s; }); return m; }, [parsedItems]);
  const ltxSectionsMap = useMemo(function () { const names = {}; Object.keys(project.ltxFiles || {}).forEach(function (p) { parseLtx(project.ltxFiles[p]).sections.forEach(function (sec) { if (sec.name) { (names[sec.name] = names[sec.name] || []); if (names[sec.name].indexOf(p) === -1) names[sec.name].push(p); } }); }); return names; }, [project.ltxFiles]);
  const ltxSectionNames = useMemo(function () { return new Set(Object.keys(ltxSectionsMap).map(function (k) { return k.toLowerCase(); })); }, [ltxSectionsMap]);
  const includeRows = useMemo(function () { const rows = []; includeTreeRows(project.systemLtx || '', project.ltxFiles || {}, 0, new Set(), rows); return rows; }, [project.systemLtx, project.ltxFiles]);
  const includeSvg = useMemo(function () { return includeGraphSvg(project.systemLtx || '', project.ltxFiles || {}); }, [project.systemLtx, project.ltxFiles]);

  const ui = useMemo(function () {
    return dark ? {
      app: 'bg-zinc-950 text-zinc-100', panel: 'bg-zinc-900/80 border-zinc-800', soft: 'bg-zinc-900/55 border-zinc-800',
      field: 'bg-zinc-800 border-zinc-700 text-zinc-100 placeholder-zinc-500', muted: 'text-zinc-500', hover: 'hover:bg-zinc-800',
      canvas: '#09090b', grid: '#27272a', card: 'bg-zinc-900 border-zinc-800 text-zinc-100', line: '#71717a'
    } : {
      app: 'bg-slate-100 text-slate-900', panel: 'bg-white/95 border-slate-200 shadow-sm', soft: 'bg-slate-50 border-slate-200',
      field: 'bg-white border-slate-300 text-slate-900 placeholder-slate-400', muted: 'text-slate-500', hover: 'hover:bg-slate-100',
      canvas: '#f8fafc', grid: '#cbd5e1', card: 'bg-white border-slate-300 text-slate-900 shadow-md', line: '#64748b'
    };
  }, [dark]);

  const patch = function (p) { setProject(function (prev) { return Object.assign({}, prev, p); }); };
  const patchNode = function (id, data) {
    setProject(function (prev) { return Object.assign({}, prev, { nodes: prev.nodes.map(function (n) { return n.id === id ? Object.assign({}, n, { data: Object.assign({}, n.data, data) }) : n; }) }); });
  };
  const say = function (msg, action) { setToast(msg); setToastAction(action || null); setOpLog(function (prev) { return prev.concat([{ t: Date.now(), msg: msg }]).slice(-100); }); setTimeout(function () { setToast(''); setToastAction(null); }, action ? 6000 : 2400); };
  const openCtxMenu = function (e, items) { e.preventDefault(); e.stopPropagation(); setCtxMenu({ x: e.clientX, y: e.clientY, items: items }); };
  const duplicateNode = function (id) {
    const src = nodes.find(function (n) { return n.id === id; }); if (!src) return;
    const copy = Object.assign({}, src, { id: 'node_' + Math.random().toString(36).slice(2, 8), x: src.x + 40, y: src.y + 40, data: Object.assign({}, src.data, { phraseId: '' }) });
    pushGraphHist(); patch({ nodes: nodes.concat([copy]) }); setSelected(copy.id); say('Нода продублирована');
  };
  const startPlaythrough = function () {
    const start = nodes.find(function (n) { return n.type === 'START'; }) || nodes[0];
    if (!start) { say('Нет стартовой ноды'); return; }
    setSelected(start.id);
    setPlaythrough({ current: start.id, path: [start.id] });
    setTab('dialogues');
  };
  const stepPlaythrough = function (nextId) {
    if (!playthrough) return;
    setSelected(nextId);
    setPlaythrough({ current: nextId, path: playthrough.path.concat([nextId]) });
  };
  const exitPlaythrough = function () { setPlaythrough(null); };

  useEffect(function () {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(toPersistable(project))); } catch (e) { }
    const cur = { name: project.projectName, ltxFiles: project.ltxFiles, textures: project.textures, scripts: project.scripts };
    const last = lastGamedataRef.current;
    if (!last || last.name !== cur.name || last.ltxFiles !== cur.ltxFiles || last.textures !== cur.textures || last.scripts !== cur.scripts) {
      lastGamedataRef.current = cur;
      try { saveGamedataSnapshot(project.projectName, gamedataOf(project)); } catch (e) {}
    }
  }, [project]);

  useEffect(function () { try { localStorage.setItem(PROGRAM_SETTINGS_KEY, JSON.stringify(programSettings)); } catch (e) {} }, [programSettings]);

  useEffect(function () { autosaveRef.current = { project: project, projects: projects }; }, [project, projects]);
  useEffect(function () {
    const secs = +((programSettings && programSettings.autosaveInterval) || 0);
    if (!(secs > 0)) return;
    const t = setInterval(function () {
      const cur = autosaveRef.current;
      if (!cur) return;
      try {
        const persist = toPersistable(cur.project);
        localStorage.setItem(STORAGE_KEY, JSON.stringify(persist));
        const list = (cur.projects || []).filter(function (p) { return p.name !== cur.project.projectName; });
        localStorage.setItem(PROJECTS_KEY, JSON.stringify([{ name: cur.project.projectName, updatedAt: Date.now(), data: persist }].concat(list)));
        if (window.xrayForge && window.xrayForge.saveProjectFile) window.xrayForge.saveProjectFile({ name: cur.project.projectName, data: persist });
        setLastAutosave(new Date());
        setDirty(false);
      } catch (e) {}
    }, secs * 1000);
    return function () { clearInterval(t); };
  }, [programSettings && programSettings.autosaveInterval]);

  useEffect(function () {
    const move = function (e) {
      if (!dragRef.current) return;
      const d = dragRef.current;
      const dx = (e.clientX - d.cx) / zoom;
      const dy = (e.clientY - d.cy) / zoom;
      if (d.kind === 'node') setProject(function (prev) { return Object.assign({}, prev, { nodes: prev.nodes.map(function (n) { return n.id === d.id ? Object.assign({}, n, { x: d.x + dx, y: d.y + dy }) : n; }) }); });
      if (d.kind === 'group') setProject(function (prev) { return Object.assign({}, prev, { nodes: prev.nodes.map(function (n) { const o = d.origins.find(function (o) { return o.id === n.id; }); return o ? Object.assign({}, n, { x: o.x + dx, y: o.y + dy }) : n; }) }); });
      if (d.kind === 'pan') setPan({ x: d.x + e.clientX - d.cx, y: d.y + e.clientY - d.cy });
      if (d.kind === 'marquee') {
        const gx1 = (d.cx - d.rectLeft - d.panX) / d.zoom;
        const gy1 = (d.cy - d.rectTop - d.panY) / d.zoom;
        const gx2 = (e.clientX - d.rectLeft - d.panX) / d.zoom;
        const gy2 = (e.clientY - d.rectTop - d.panY) / d.zoom;
        setMarquee({ x1: Math.min(gx1, gx2), y1: Math.min(gy1, gy2), x2: Math.max(gx1, gx2), y2: Math.max(gy1, gy2) });
      }
    };
    const up = function () {
      if (dragRef.current && dragRef.current.kind === 'marquee') {
        const m = marqueeRef.current;
        if (m) {
          const ids = nodesRef.current.filter(function (n) { return n.x + 210 >= m.x1 && n.x <= m.x2 && n.y + 100 >= m.y1 && n.y <= m.y2; }).map(function (n) { return n.id; });
          setMultiSelect(ids);
        }
        setMarquee(null);
      }
      dragRef.current = null;
    };
    window.addEventListener('mousemove', move);
    window.addEventListener('mouseup', up);
    return function () { window.removeEventListener('mousemove', move); window.removeEventListener('mouseup', up); };
  }, [zoom]);

  const addNode = function (type) {
    const n = makeNode(type, 120 + nodes.length * 24, 120 + nodes.length * 16);
    if (type === 'PHRASE') n.data.phraseId = nextPhraseId();
    pushGraphHist(); patch({ nodes: nodes.concat([n]) });
    setSelected(n.id);
  };
  const nextPhraseId = function () {
    const base = ((project.dialogueId || project.projectName) || 'dialogue').replace(/[^a-zA-Z0-9_]/g, '_');
    let n = 1;
    while (nodes.some(function (x) { return x.data && x.data.phraseId === base + '_' + n; })) n++;
    return base + '_' + n;
  };
  const createDialogue = function () {
    const id = window.prompt('ID диалога (реплики будут создаваться как <id>_1, <id>_2 …):', project.dialogueId || project.projectName || 'dialogue');
    if (!id || !id.trim()) return;
    const clean = String(id).trim().replace(/[^a-zA-Z0-9_]/g, '_') || 'dialogue';
    const start = Object.assign(makeNode('START', 60, 180), { id: 'start' });
    patch({ dialogueId: clean, nodes: [start], edges: [] });
    setSelected(null); setErrors([]);
    say('Диалог «' + clean + '» создан. Добавьте реплики из палитры.');
  };
  const deleteNode = function (id) {
    pushGraphHist(); patch({ nodes: nodes.filter(function (n) { return n.id !== id; }), edges: edges.filter(function (e) { return e.source !== id && e.target !== id; }) });
    setSelected(null);
    say('Нода удалена', { label: 'Отменить', action: undoGraph });
  };
  const deleteEdge = function (id) { pushGraphHist(); patch({ edges: edges.filter(function (e) { return e.id !== id; }) }); say('Связь удалена', { label: 'Отменить', action: undoGraph }); };
  const pushGraphHist = function () {
    setGraphHist(function (s) { return graphHistory.push(s, { nodes: project.nodes, edges: project.edges }); });
  };
  const undoGraph = function () {
    setGraphHist(graphHistory.move(graphHist, -1, function (t) { if (t) patch({ nodes: t.nodes, edges: t.edges }); }));
  };
  const redoGraph = function () {
    setGraphHist(graphHistory.move(graphHist, +1, function (t) { if (t) patch({ nodes: t.nodes, edges: t.edges }); }));
  };
  const portClick = function (id, kind) {
    if (!connectFrom) { setConnectFrom({ id: id, kind: kind }); return; }
    if (connectFrom.id === id || connectFrom.kind === kind) { setConnectFrom(null); return; }
    const source = connectFrom.kind === 'out' ? connectFrom.id : id;
    const target = connectFrom.kind === 'out' ? id : connectFrom.id;
    if (!edges.some(function (e) { return e.source === source && e.target === target; })) {
      pushGraphHist(); patch({ edges: edges.concat([{ id: 'e_' + Date.now(), source: source, target: target }]) });
    }
    setConnectFrom(null);
  };

  const validate = function () {
    const errs = validateGraph(nodes, edges);
    const langIds = new Set();
    (project.languages || []).forEach(function (l) { (l.entries || []).forEach(function (e) { if (e.id) langIds.add(e.id); }); });
    nodes.filter(function (n) { return n.type === 'PHRASE' && n.data && n.data.text_id; }).forEach(function (n) {
      if (!langIds.has(n.data.text_id)) errs.push({ severity: 'warning', code: 'missing_text_id', msg: 'text_id без перевода: ' + n.id + ' → ' + n.data.text_id });
    });
    validateTextIds(project.dialogues || [], project.languages || []).forEach(function (iss) { errs.push({ severity: 'warning', code: 'missing_text_id', msg: iss.msg }); });
    setErrors(errs);
    const blocking = errs.filter(function (x) { return x.severity === 'error'; }).length;
    const warnings = errs.filter(function (x) { return x.severity === 'warning'; }).length;
    say(blocking ? 'Ошибок: ' + blocking + (warnings ? ', предупреждений: ' + warnings : '') : (warnings ? 'Предупреждений: ' + warnings : 'Ошибок нет'));
  };

  const saveProject = function () {
    const persist = toPersistable(project);
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(persist)); } catch (e) { say('Ошибка сохранения: ' + e.message); return; }
    setBackups(function (prev) { const next = [{ t: Date.now(), data: persist }].concat(prev).slice(0, 10); saveBackups(next); return next; });
    setProjects(function (prev) {
      const list = prev.filter(function (p) { return p.name !== project.projectName; });
      const updated = [{ name: project.projectName, updatedAt: Date.now(), data: persist }].concat(list);
      try { localStorage.setItem(PROJECTS_KEY, JSON.stringify(updated)); } catch (e) {}
      return updated;
    });
    say('Проект сохранён');
    setDirty(false);
  };
  const switchProject = function (name) {
    if (!name || name === project.projectName) return;
    const persist = toPersistable(project);
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify(persist)); } catch (e) {}
    setProjects(function (prev) {
      const list = prev.filter(function (p) { return p.name !== project.projectName; });
      const updated = [{ name: project.projectName, updatedAt: Date.now(), data: persist }].concat(list);
      try { localStorage.setItem(PROJECTS_KEY, JSON.stringify(updated)); } catch (e) {}
      return updated;
    });
    const target = projects.find(function (p) { return p.name === name; });
    if (target) { setProject(migrate(target.data)); setDirty(false); setWelcomeOpen(false); say('Проект «' + name + '» открыт'); }
  };
  const syncProject = async function () {
    const url = String(programSettings.syncUrl || '').trim();
    if (!url) { say('Укажите URL сервера синхронизации в настройках программы'); return; }
    try {
      const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(toPersistable(project)) });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      say('Синхронизировано с сервером');
    } catch (err) { say('Ошибка синхронизации: ' + err.message); }
  };
  const loadFromSync = async function () {
    const url = String(programSettings.syncUrl || '').trim();
    if (!url) { say('Укажите URL сервера синхронизации в настройках программы'); return; }
    try {
      const res = await fetch(url);
      if (!res.ok) throw new Error('HTTP ' + res.status);
      const data = await res.json();
      setProject(migrate(data)); setDirty(false); say('Проект загружен с сервера');
    } catch (err) { say('Ошибка загрузки: ' + err.message); }
  };
  const addPlugin = function () {
    const p = { id: 'plg_' + Date.now().toString(36), name: 'Экспортёр ' + (plugins.length + 1), code: 'function(project){ return [{ name: "export.json", text: JSON.stringify(project, null, 2) }]; }' };
    const next = plugins.concat([p]); setPlugins(next); savePlugins(next); say('Плагин добавлен');
  };
  const updatePlugin = function (id, key, value) {
    const next = plugins.map(function (p) { return p.id === id ? Object.assign({}, p, (function () { const o = {}; o[key] = value; return o; })()) : p; });
    setPlugins(next); savePlugins(next);
  };
  const removePlugin = function (id) {
    const next = plugins.filter(function (p) { return p.id !== id; });
    setPlugins(next); savePlugins(next);
  };
  const runPlugin = function (p) {
    try {
      const files = runPluginExporter(p, project);
      files.forEach(function (f) { download(f.text, f.name, 'text/plain', false); });
      say('Плагин выполнен: ' + files.length + ' файл(ов)');
    } catch (err) { say(err.message); }
  };
  const exportSettings = function () { download(JSON.stringify(programSettings, null, 2), 'xray-forge-settings.json', 'application/json', false); say('Настройки экспортированы'); };
  const importSettings = function (e) {
    const f = e.target.files && e.target.files[0]; if (!f) return;
    const r = new FileReader();
    r.onload = function () { try { const s = JSON.parse(r.result); setProgramSettings(Object.assign({}, programSettings, s)); say('Настройки импортированы'); } catch (err) { say('Ошибка импорта настроек'); } };
    r.readAsText(f); e.target.value = '';
  };
  const restoreBackup = function (b) {
    if (window.confirm('Восстановить проект из резервной копии от ' + new Date(b.t).toLocaleString() + '?')) { setProject(migrate(b.data)); say('Проект восстановлен'); }
  };
  const runIntegrity = function () { const r = validateProjectIntegrity(project); setIntegrityResults(r); say(r.length ? 'Проблем: ' + r.length : 'Проблем не найдено'); };
  const exportFullPackage = function () {
    try {
      const encoding = (project.settings && project.settings.exportEncoding) || 'windows-1251';
      const data = buildGameFiles(project.dialogueId || project.projectName, project.series, nodes, edges, encoding, project.languages);
      const cfg = (SERIES.find(function (s) { return s.value === project.series; }) || SERIES[0]).cfg;
      const bytes = function (s) { return textBytes(s, encoding); };
      const qxml = questsToXml(project.quests || [], encoding);
      if (qxml.trim()) data['gamedata/' + cfg + '/misc/game_tasks.xml'] = bytes(qxml);
      if (project.itemsLtx) data['gamedata/' + cfg + '/misc/items.ltx'] = bytes(project.itemsLtx);
      if (project.systemLtx) data['gamedata/' + cfg + '/system.ltx'] = bytes(project.systemLtx);
      (project.scripts || []).forEach(function (s) { if (s.name) data['gamedata/' + cfg + '/scripts/' + s.name] = bytes(s.text || ''); });
      Object.keys(project.ltxFiles || {}).forEach(function (p) { data['gamedata/' + p] = bytes(project.ltxFiles[p]); });
      downloadBytes(zipSync(data), (project.projectName || 'project') + '_full.zip', 'application/zip');
      say('Полный пакет создан (' + Object.keys(data).length + ' файлов)');
    } catch (err) { say(err.message); }
  };
  const exportAllDialogues = function () {
    try {
      const encoding = (project.settings && project.settings.exportEncoding) || 'windows-1251';
      const cfg = (SERIES.find(function (s) { return s.value === project.series; }) || SERIES[0]).cfg;
      const list = [{ id: project.dialogueId || project.projectName, name: project.projectName, nodes: nodes, edges: edges }];
      (project.dialogues || []).forEach(function (d) {
        const id = d.id || d.name;
        if (!list.some(function (x) { return x.id === id; })) list.push({ id: id, name: d.name, nodes: d.nodes, edges: d.edges });
      });
      const groups = {}, order = [];
      list.forEach(function (d) {
        const f = String((dlgExportFiles || {})[d.id] || d.id).replace(/[^a-zA-Z0-9_-]/g, '_') || d.id;
        if (!groups[f]) { groups[f] = []; order.push(f); }
        groups[f].push(d);
      });
      const text = gameFilesFromDialogues(order.map(function (f) { return { fileName: f, dialogues: groups[f] }; }), cfg, encoding, project.languages, project.projectName);
      const bytes = function (s) { return textBytes(s, encoding); };
      const zip = {};
      Object.keys(text).forEach(function (p) { zip[p] = bytes(text[p]); });
      downloadBytes(zipSync(zip), (project.projectName || 'project') + '_dialogues.zip', 'application/zip');
      say('Экспортировано диалогов: ' + list.length + ' в ' + order.length + ' XML');
    } catch (err) { say(err.message); }
  };

  const exportProject = function () { download(JSON.stringify(toPersistable(project), null, 2), (project.projectName || 'project') + '.project.json', 'application/json', false); };
  const importProject = function (e) {
    const file = e.target.files && e.target.files[0]; if (!file) return;
    const r = new FileReader();
    r.onload = function (ev) {
      try { setProject(migrate(JSON.parse(ev.target.result))); say('Проект загружен'); setSettingsOpen(false); setWelcomeOpen(false); } catch (err) { say('Не удалось прочитать проект'); }
    };
    r.readAsText(file); e.target.value = '';
  };
  const exportXml = function () { const d = project.dialogueId || project.projectName; download(toXml(d, project.series, nodes, edges), (d || 'dialogue') + '.editor.xml', 'application/xml', false); };
  const exportGameZip = function () { const d = project.dialogueId || project.projectName; try { downloadBytes(gamePackage(d, project.series, nodes, edges, (project.settings && project.settings.exportEncoding) || 'windows-1251', project.languages), (d || 'dialogue') + '_game.zip', 'application/zip'); say('Игровой пакет создан'); } catch (err) { say(err.message); } };
  const openProjectItem = function (p) {
    const proj = migrate(p.data);
    const snap = loadGamedataSnapshot(p.name);
    if (snap) {
      setProject(Object.assign({}, proj, snap));
    } else {
      setProject(proj);
      if (p.data && p.data.gamedataPath) {
        setSelected(null); setErrors([]); setTab('dialogues'); setWelcomeOpen(false);
        setSettingsOpen(true);
        say('gamedata «' + p.data.gamedataPath + '» не найдена — перевыберите папку в настройках проекта');
        return;
      }
    }
    setSelected(null); setErrors([]); setTab('dialogues'); setWelcomeOpen(false); say('Проект загружен');
  };
  const deleteProjectItem = function (name) {
    const list = projects.filter(function (x) { return x.name !== name; });
    setProjects(list);
    try { localStorage.setItem(PROJECTS_KEY, JSON.stringify(list)); } catch (e) {}
    removeGamedataSnapshot(name);
    say('Проект удалён');
  };
  const renameProjectItem = function (p) {
    const next = window.prompt('Новое имя проекта', p.name);
    if (!next || !next.trim()) return;
    const name = next.trim();
    const list = projects.map(function (x) { return x.name === p.name ? Object.assign({}, x, { name: name }) : x; });
    setProjects(list);
    try { localStorage.setItem(PROJECTS_KEY, JSON.stringify(list)); } catch (e) {}
    say('Проект переименован');
  };
  const createProject = function () {
    const p = migrate(emptyProject());
    p.projectName = String(newName || '').trim() || 'new_dialogue';
    p.series = newSeries;
    p.theme = newTheme;
    setProgramSettings(Object.assign({}, programSettings, { theme: newTheme }));
    setProject(p); setSelected(null); setErrors([]); setTab('dialogues'); setWelcomeOpen(false);
    say('Проект создан');
  };
  const loadDemo = function () { const s = seedDialogue(); patch({ nodes: s.nodes.map(safeNode), edges: s.edges, dialogueId: 'trader_demo' }); setSelected(null); say('Демо-граф создан'); };
  const centerView = function () {
    if (!nodes.length) { setPan({ x: 20, y: 20 }); setZoom(1); return; }
    const minX = Math.min.apply(null, nodes.map(function (n) { return n.x; }));
    const minY = Math.min.apply(null, nodes.map(function (n) { return n.y; }));
    const maxX = Math.max.apply(null, nodes.map(function (n) { return n.x + 210; }));
    const maxY = Math.max.apply(null, nodes.map(function (n) { return n.y + 100; }));
    const cv = canvasRef.current;
    const vw = cv ? cv.clientWidth : 800, vh = cv ? cv.clientHeight : 600;
    setZoom(1);
    setPan({ x: vw / 2 - (minX + maxX) / 2, y: vh / 2 - (minY + maxY) / 2 });
  };
  const autoLayout = function () {
    if (!nodes.length) return;
    const ns = nodes.map(function (n) { return Object.assign({}, n); });
    layoutDialogue(ns, edges);
    patch({ nodes: ns });
    say('Граф разложен автоматически');
  };
  const loadDialogueIntoGraph = function (dlg) {
    patch({ nodes: (Array.isArray(dlg.nodes) ? dlg.nodes : []).map(safeNode), edges: (Array.isArray(dlg.edges) ? dlg.edges : []).map(function (e) { return Object.assign({}, e); }), dialogueId: dlg.id || project.dialogueId || project.projectName });
    setSelected(null);
    say('Диалог загружен: ' + dlg.id);
  };
  const resetStorage = function () {
    if (!window.confirm('Сбросить все данные редактора в браузере?')) return;
    try { localStorage.removeItem(STORAGE_KEY); localStorage.removeItem(PROJECTS_KEY); localStorage.removeItem(GAMEDATA_KEY); } catch (e) {}
    lastGamedataRef.current = null;
    setProject(emptyProject()); setProjects([]); setSelected(null); setErrors([]); say('Данные сброшены');
  };

  const readDirectoryFiles = async function (handle) {
    const fileMap = {};
    async function walk(dir, prefix) {
      for await (const entry of dir.values()) {
        if (entry.kind === 'directory') await walk(entry, prefix + entry.name + '/');
        else {
          const file = await entry.getFile();
          fileMap[normalizePath(prefix + entry.name)] = file;
        }
      }
    }
    await walk(handle, '');
    return fileMap;
  };

  const processGamedataFiles = async function (fileMap, topName) {
    const ltxFiles = {};
    const itemFiles = {};
    const foundScripts = [];
    const newTextures = [];
    const foundCharacters = [];
    const foundDialogues = [];
    const foundLanguages = [];
    const foundQuests = [];
    let foundGeneral = [];
    let foundGeneralDesc = null;
    let foundInfoPortions = [];
    let foundEncyclopedia = [];
    let foundSquads = [];
    let foundSecrets = null;
    let foundSpawnSections = [];
    let foundMonsters = [];
    let foundActor = null;
    let foundSurgeManager = null;
    let foundNpcProfiles = [];
    let foundTrade = null;
    let foundTextureDescriptors = [];
    let sysText = null, itemsText = null;
    const skipped = [];
    const importEncoding = (project.settings && project.settings.importEncoding) || 'auto';
    const total = Object.keys(fileMap).length;
    let done = 0;
    setLoadProgress({ done: 0, total: total });

    for (const path in fileMap) {
      const file = fileMap[path];
      const lower = path.toLowerCase();
      try {
        if (lower.endsWith('.dds') && (lower.indexOf('ui_icon_equipment') !== -1 || lower.indexOf('ui_npc') !== -1 || lower.indexOf('ui_actor') !== -1)) {
          try {
            const buf = await file.arrayBuffer();
            const dec = decodeDDS(buf);
            newTextures.push({ name: file.name, dataURL: pixelsToDataURL(dec.pixels, dec.width, dec.height), width: dec.width, height: dec.height });
          } catch (e) { newTextures.push({ name: file.name, error: e.message }); }
          continue;
        }

        if (lower.endsWith('.ltx') || lower.endsWith('.script') || lower.endsWith('.lua')) {
          const text = await readGameText(file, { encoding: importEncoding });
          ltxFiles[normalizePath(path)] = text;
          if (lower.endsWith('.script') || lower.endsWith('.lua')) foundScripts.push({ name: file.name, path: normalizePath(path), text: text, original: text });
          else if (isItemFile(file.name)) itemFiles[file.name] = text;
          continue;
        }

        if (lower.endsWith('.xml')) {
          let text = await readGameText(file, { encoding: importEncoding });
          text = text.replace(/^\s*#include\b[^\n]*$/gm, ''); // убрать #include (особенность X-Ray XML)
          const normPath = normalizePath(path);
          const fname = String(file.name || '').toLowerCase();
          // переводы — по папке text/<locale>/
          if (normPath.indexOf('/text/') !== -1) {
            const language = parseStringTableXml(text, path);
            if (language) { foundLanguages.push(language); continue; }
          }
          // персонажи — по имени файла character_desc*
          if (fname.indexOf('character_desc') === 0) {
            const chars = parseCharactersXml(text);
            if (chars.length) { foundCharacters.push({ file: file.name, path: normPath, characters: chars }); continue; }
          }
          // диалоги — по имени файла dialog*
          if (fname.indexOf('dialog') !== -1) {
            const dialogs = parseDialogueXml(text);
            if (dialogs.length) { foundDialogues.push({ file: file.name, path: normPath, dialogues: dialogs }); continue; }
          }
          // квесты — по имени файла task*
          if (fname.indexOf('task') !== -1) {
            const tasks = parseGameTasksXml(text);
            if (tasks.length) { foundQuests.push.apply(foundQuests, tasks.map(function (t) { return Object.assign({}, t, { sourcePath: normPath, original: JSON.parse(JSON.stringify(t)) }); })); continue; }
          }
          // fallback — контентная детекция
          const language = parseStringTableXml(text, path);
          if (language) { foundLanguages.push(language); continue; }
          const chars = parseCharactersXml(text);
          if (chars.length) { foundCharacters.push({ file: file.name, path: normPath, characters: chars }); continue; }
          const dialogs = parseDialogueXml(text);
          if (dialogs.length) { foundDialogues.push({ file: file.name, path: normPath, dialogues: dialogs }); }
          const tasks = parseGameTasksXml(text);
          if (tasks.length) { foundQuests.push.apply(foundQuests, tasks.map(function (t) { return Object.assign({}, t, { sourcePath: normPath, original: JSON.parse(JSON.stringify(t)) }); })); }
          const general = parseGeneralCharactersXml(text);
          if (general.length) foundGeneral = foundGeneral.concat(general);
          const generalDesc = parseCharacterDescGeneral(text);
          if (generalDesc) foundGeneralDesc = { text: text, communities: generalDesc.communities };
          const infoPortions = parseInfoPortions(text);
          if (infoPortions.length) foundInfoPortions = foundInfoPortions.concat(infoPortions);
          const encyclopedia = parseEncyclopedia(text);
          if (encyclopedia.length) foundEncyclopedia = foundEncyclopedia.concat(encyclopedia);
          const npcProfile = parseNpcProfile(text);
          if (npcProfile.length) foundNpcProfiles = foundNpcProfiles.concat(npcProfile);
          const texDesc = parseTextureDescriptor(text);
          if (texDesc && texDesc.textures.length) foundTextureDescriptors.push(texDesc);
          continue;
        }
      } catch (err) {
        skipped.push({ path: path, error: (err && err.message) ? err.message : 'ошибка' });
      }
      done++;
      if (done % 5 === 0 || done === total) setLoadProgress({ done: done, total: total });
    }

    const sysPath = Object.keys(ltxFiles).find(function (p) { return p.toLowerCase().endsWith('system.ltx'); });
    if (sysPath) sysText = ltxFiles[sysPath];
    Object.keys(ltxFiles).forEach(function (p) {
      if (p.toLowerCase().endsWith('task_manager.ltx')) {
        parseTaskManagerLtx(ltxFiles[p]).forEach(function (q) { if (!foundQuests.some(function (x) { return x.id === q.id; })) foundQuests.push(Object.assign({}, q, { sourcePath: normalizePath(p), original: JSON.parse(JSON.stringify(q)) })); });
      }
    });
    let mergedItems = '';
    const itemOrigins = {};
    Object.keys(itemFiles).sort().forEach(function (fn) {
      const text = itemFiles[fn] || '';
      mergedItems += (mergedItems ? NL : '') + '; === ' + fn + ' ===' + NL + text;
      const cat = itemFileCategory(fn);
      parseLtx(text).sections.forEach(function (sec) { if (sec.name) itemOrigins[sec.name] = cat || classifyItem({ name: sec.name, parents: sec.parents, entries: sec.entries }); });
    });
    if (mergedItems) itemsText = mergedItems;

    const mergedChars = (project.characters || []).slice();
    foundCharacters.forEach(function (fc) {
      fc.characters.forEach(function (c) {
        if (!mergedChars.some(function (x) { return x.id === c.id; })) mergedChars.push(Object.assign({}, c, { original: JSON.parse(JSON.stringify(c)) }));
      });
    });

    const groupedLanguages = {};
    foundLanguages.forEach(function (lang) {
      const key = lang.locale || 'unknown';
      if (!groupedLanguages[key]) groupedLanguages[key] = { locale: key, name: lang.name || '', entries: [] };
      (Array.isArray(lang.entries) ? lang.entries : []).forEach(function (e) { if (e && e.id) groupedLanguages[key].entries.push({ id: e.id, text: e.text || '' }); });
    });
    const mergedLanguages = mergeLanguages(project.languages, Object.keys(groupedLanguages).map(function (k) { return groupedLanguages[k]; }));

    const mergedQuests = (project.quests || []).slice();
    foundQuests.forEach(function (q) { if (!mergedQuests.some(function (x) { return x.id === q.id; })) mergedQuests.push(q); });

    const next = {
      gamedataPath: topName || project.gamedataPath,
      ltxFiles: ltxFiles,
      characters: mergedChars,
      languages: mergedLanguages,
      quests: mergedQuests,
      itemFiles: itemFiles,
      itemOrigins: itemOrigins
    };
    if (sysText != null) next.systemLtx = sysText;
    if (itemsText != null) next.itemsLtx = itemsText;
    if (foundScripts.length) next.scripts = foundScripts;
    if (newTextures.length) {
      newTextures.sort(function (a, b) {
        const ai = String(a.name || '').toLowerCase().indexOf('ui_icon_equipment') !== -1 ? 0 : 1;
        const bi = String(b.name || '').toLowerCase().indexOf('ui_icon_equipment') !== -1 ? 0 : 1;
        return ai - bi;
      });
      next.textures = newTextures;
    }
    if (foundDialogues.length) next.dialogues = foundDialogues.flatMap(function (group) {
      return group.dialogues.map(function (d) { return Object.assign({}, d, { sourcePath: group.path, sourceFile: group.file }); });
    });
    if (foundGeneral.length) next.generalCommunities = Array.from(new Set(foundGeneral));
    if (foundGeneralDesc) next.generalDesc = foundGeneralDesc;
    let gameRelations = null;
    Object.keys(ltxFiles).forEach(function (p) { if (p.toLowerCase().endsWith('game_relations.ltx') && !gameRelations) { gameRelations = parseGameRelations(ltxFiles[p]); gameRelations.scales = parseRatingScales(ltxFiles[p]); } });
    if (gameRelations) { next.gameRelations = gameRelations; next.generalCommunities = gameRelations.communities; }
    let squads = [];
    Object.keys(ltxFiles).forEach(function (p) { if (p.toLowerCase().indexOf('squad_descr') !== -1 && p.toLowerCase().endsWith('.ltx')) squads = squads.concat(parseSquadDescr(ltxFiles[p])); });
    if (squads.length) next.squads = squads;
    Object.keys(ltxFiles).forEach(function (p) { if (p.toLowerCase().endsWith('secrets.ltx') && !foundSecrets) foundSecrets = parseSecrets(ltxFiles[p]); });
    if (foundSecrets) next.secrets = foundSecrets;
    Object.keys(ltxFiles).forEach(function (p) { if (p.toLowerCase().indexOf('spawn_sections') !== -1 && p.toLowerCase().endsWith('.ltx')) foundSpawnSections = foundSpawnSections.concat(parseSpawnSections(ltxFiles[p])); });
    if (foundSpawnSections.length) next.spawnSections = foundSpawnSections;
    Object.keys(ltxFiles).forEach(function (p) { if (p.toLowerCase().endsWith('actor.ltx') && !foundActor) foundActor = parseActor(ltxFiles[p]); });
    if (foundActor) next.actor = foundActor;
    Object.keys(ltxFiles).forEach(function (p) { const lower = p.toLowerCase(); if ((lower.endsWith('monsters.ltx') || (lower.indexOf('/m_') !== -1 || lower.indexOf('\\m_') !== -1)) && lower.endsWith('.ltx')) foundMonsters = foundMonsters.concat(parseMonsters(ltxFiles[p])); });
    if (foundMonsters.length) next.monsters = foundMonsters;
    Object.keys(ltxFiles).forEach(function (p) { if (p.toLowerCase().endsWith('surge_manager.ltx') && !foundSurgeManager) foundSurgeManager = parseSurgeManager(ltxFiles[p]); });
    if (foundSurgeManager) next.surgeManager = foundSurgeManager;
    Object.keys(ltxFiles).forEach(function (p) { if (p.toLowerCase().indexOf('trade_') !== -1 && p.toLowerCase().endsWith('.ltx')) { const t = parseTrade(ltxFiles[p]); if (t.traders.length || t.supplies.length) foundTrade = { traders: (foundTrade ? foundTrade.traders : []).concat(t.traders), supplies: (foundTrade ? foundTrade.supplies : []).concat(t.supplies) }; } });
    if (foundTrade) next.trade = foundTrade;
    if (foundNpcProfiles.length) next.npcProfiles = foundNpcProfiles;
    if (foundTextureDescriptors.length) {
      const textureMap = {};
      foundTextureDescriptors.forEach(function (td) { td.textures.forEach(function (t) { textureMap[t.id] = { file: td.file, x: t.x, y: t.y, width: t.width, height: t.height }; }); });
      next.textureMap = textureMap;
    }
    if (foundInfoPortions.length) next.infoPortions = foundInfoPortions;
    if (foundEncyclopedia.length) next.encyclopedia = foundEncyclopedia;

    patch(next);
    setLoadProgress(null);
    return {
      files: Object.keys(fileMap).length,
      scripts: foundScripts.length,
      textures: newTextures.length,
      characters: mergedChars.length,
      dialogues: foundDialogues.reduce(function (a, d) { return a + d.dialogues.length; }, 0),
      languages: mergedLanguages.length,
      quests: mergedQuests.length,
      items: Object.keys(itemFiles).length,
      skipped: skipped.length
    };
  };

  const processGamedataMap = async function (fileMap, top) {
    const res = await processGamedataFiles(fileMap, top);
    say('Загружено: файлов ' + res.files + ', скриптов ' + res.scripts + ', персонажей ' + res.characters + ', диалогов ' + res.dialogues + ', предметов ' + res.items + ', текстур ' + res.textures + ', языков ' + res.languages + ', квестов ' + res.quests + (res.skipped ? ' · пропущено ' + res.skipped : ''));
    return res;
  };
  const pickGamedataFolder = async function () {
    try {
      if (window.showDirectoryPicker) {
        const handle = await window.showDirectoryPicker();
        await processGamedataMap(await readDirectoryFiles(handle), handle && handle.name ? handle.name : '');
      } else {
        gamedataRef.current.value = '';
        gamedataRef.current.click();
      }
    } catch (err) { if (err && err.name === 'AbortError') return; say('Выбор gamedata: ' + (err && err.message ? err.message : 'ошибка')); }
  };
  const scanGamedataFiles = async function (e) {
    const files = Array.from(e.target.files || []);
    const fileMap = {};
    files.forEach(function (f) { fileMap[normalizePath(f.webkitRelativePath || f.name)] = f; });
    const top = files[0] ? (files[0].webkitRelativePath || files[0].name).split('/')[0] : '';
    await processGamedataMap(fileMap, top);
    e.target.value = '';
  };
  const pickResourcesFolder = async function () {
    try {
      if (!window.showDirectoryPicker) { say('Выбор папки недоступен в этом браузере'); return; }
      const handle = await window.showDirectoryPicker();
      patch({ resourcesPath: handle.name });
      say('Папка ресурсов: ' + handle.name);
    } catch (err) { if (err && err.name === 'AbortError') return; say('Выбор ресурсов: ' + (err && err.message ? err.message : 'ошибка')); }
  };

  const scanScripts = async function (e) {
    const files = Array.from(e.target.files || []);
    const found = [];
    for (const f of files) { const t = await readGameText(f, { encoding: (project.settings && project.settings.importEncoding) || 'auto' }); found.push({ name: f.name, path: f.webkitRelativePath || f.name, text: t, original: t }); }
    patch({ scripts: found }); say('Скриптов: ' + found.length); e.target.value = '';
  };
  const importStringTables = async function (e) {
    const files = Array.from(e.target.files || []);
    if (!files.length) return;
    const incoming = [];
    for (const f of files) {
      const text = await readGameText(f, { encoding: (project.settings && project.settings.importEncoding) || 'auto' });
      const lang = parseStringTableXml(text, f.name);
      if (lang) incoming.push(lang);
    }
    if (incoming.length) { patch({ languages: mergeLanguages(project.languages, incoming) }); say('Импортировано таблиц строк: ' + incoming.length); }
    else say('В выбранных файлах не найдено string_table');
    e.target.value = '';
  };
  const updateScript = function (i, text) { patch({ scripts: (project.scripts || []).map(function (s, idx) { return idx === i ? Object.assign({}, s, { text: text }) : s; }) }); };
  const insertSnippet = function (i, sn) {
    const snippets = { 'function': 'function name()\n\t\nend', 'if/then': 'if condition then\n\t\nend', 'for': 'for i = 1, 10 do\n\t\nend', 'while': 'while condition do\n\t\nend', 'local': 'local name = value' };
    const cur = (project.scripts || [])[i] ? ((project.scripts || [])[i].text || '') : '';
    updateScript(i, cur + (cur ? NL : '') + snippets[sn]);
  };
  const insertSnippetText = function (i, text) {
    const cur = (project.scripts || [])[i] ? ((project.scripts || [])[i].text || '') : '';
    updateScript(i, cur + (cur ? NL : '') + text);
  };
  const insertLuaAtCursor = function (i, text) {
    const s = (project.scripts || [])[i]; if (!s) return;
    const el = scriptTextRef.current;
    const start = el && el.selectionStart != null ? el.selectionStart : (s.text || '').length;
    const end = el && el.selectionEnd != null ? el.selectionEnd : start;
    const next = (s.text || '').slice(0, start) + text + (s.text || '').slice(end);
    updateScript(i, next);
    setTimeout(function () { if (el) { el.focus(); el.setSelectionRange(start + text.length, start + text.length); } }, 0);
  };
  const toggleBlock = function (key) { setCollapsedBlocks(function (prev) { const n = Object.assign({}, prev); n[key] = !n[key]; return n; }); };
  const deleteScript = function (i) { patch({ scripts: (project.scripts || []).filter(function (_, idx) { return idx !== i; }) }); setSelScript(0); say('Файл удалён'); };
  const renameScript = function (i) { const n = window.prompt('Новое имя файла', (project.scripts || [])[i] ? (project.scripts || [])[i].name : ''); if (!n || !n.trim()) return; patch({ scripts: (project.scripts || []).map(function (s, idx) { return idx === i ? Object.assign({}, s, { name: n.trim(), path: n.trim() }) : s; }) }); };
  const formatCurrentScript = function () {
    const s = (project.scripts || [])[selScript]; if (!s) return;
    updateScript(selScript, formatLua(s.text));
    say('Скрипт отформатирован');
  };
  const replaceAllInScripts = function () {
    if (!scriptSearch) return;
    const from = scriptSearch, to = scriptReplace;
    let count = 0;
    const next = (project.scripts || []).map(function (s) {
      const t = String(s.text || '');
      const n = t.split(from).length - 1;
      count += n;
      return n ? Object.assign({}, s, { text: t.split(from).join(to) }) : s;
    });
    patch({ scripts: next });
    say('Заменено вхождений: ' + count);
  };
  const jumpToDefinition = function (name) {
    let s = (project.scripts || [])[selScript];
    let targetIdx = selScript;
    let idx = -1, lines = [];
    if (s) { lines = String(s.text || '').split(/\n/); idx = lines.findIndex(function (l) { return /function\b/.test(l) && l.indexOf(name) !== -1; }); }
    if (idx === -1) {
      (project.scripts || []).forEach(function (sc, i) {
        if (idx !== -1) return;
        const ls = String(sc.text || '').split(/\n/);
        const j = ls.findIndex(function (l) { return /function\b/.test(l) && l.indexOf(name) !== -1; });
        if (j !== -1) { s = sc; lines = ls; idx = j; targetIdx = i; }
      });
    }
    if (idx === -1) return;
    setSelScript(targetIdx);
    setScriptViewMode(false);
    setTimeout(function () {
      const el = scriptTextRef.current; if (!el) return;
      const offset = lines.slice(0, idx).reduce(function (a, l) { return a + l.length + 1; }, 0);
      el.focus(); el.setSelectionRange(offset, offset);
    }, 0);
  };

  const loadDds = async function (e) {
    const file = e.target.files && e.target.files[0]; if (!file) return;
    try {
      const buf = await file.arrayBuffer();
      const dec = decodeDDS(buf);
      const dataURL = pixelsToDataURL(dec.pixels, dec.width, dec.height);
      patch({ textures: [{ name: file.name, dataURL: dataURL, width: dec.width, height: dec.height }] });
      say('Загружен ' + file.name + ' (' + dec.width + 'x' + dec.height + ')');
    } catch (err) { say('Ошибка DDS: ' + err.message); }
    e.target.value = '';
  };
  const loadSpawn = async function (e) {
    const file = e.target.files && e.target.files[0]; if (!file) return;
    try {
      const buf = new Uint8Array(await file.arrayBuffer());
      const a = analyzeSpawn(buf);
      setSpawnData({ name: file.name, size: buf.byteLength, header: a.header, sections: a.sections, stringCount: a.strings.length });
      say('Спавн прочитан: ' + file.name);
    } catch (err) { say('Ошибка all.spawn: ' + err.message); }
    e.target.value = '';
  };
  const loadCompareDds = async function (e) {
    const file = e.target.files && e.target.files[0]; if (!file) return;
    try {
      const buf = await file.arrayBuffer();
      const dec = decodeDDS(buf);
      const dataURL = pixelsToDataURL(dec.pixels, dec.width, dec.height);
      patch({ textures: (project.textures || []).concat([{ name: file.name, dataURL: dataURL, width: dec.width, height: dec.height }]) });
      say('Загружен второй атлас: ' + file.name);
    } catch (err) { say('Ошибка DDS: ' + err.message); }
    e.target.value = '';
  };
  const removeCompare = function () { patch({ textures: (project.textures || []).slice(0, 1) }); };
  const pushHist = function () {
    const snap = { sel: selCell, size: selSize };
    setHistState(function (s) { return iconsHistory.push(s, snap); });
  };
  const undoHist = function () {
    setHistState(iconsHistory.move(histState, -1, function (t) { if (t) { setSelCell(t.sel); setSelSize(t.size); } }));
  };
  const redoHist = function () {
    setHistState(iconsHistory.move(histState, +1, function (t) { if (t) { setSelCell(t.sel); setSelSize(t.size); } }));
  };
  const pinIcon = function () {
    const p = { x: selCell.x, y: selCell.y, w: selSize.w, h: selSize.h, name: 'icon_' + selCell.x + '_' + selCell.y };
    const cols = Object.assign({}, project.pinCollections || { default: [] });
    cols[pinColName] = (cols[pinColName] || []).concat([p]);
    patch({ pinCollections: cols });
    say('Иконка закреплена в «' + pinColName + '»');
  };
  const unpinIcon = function (i) {
    const cols = Object.assign({}, project.pinCollections || { default: [] });
    cols[pinColName] = (cols[pinColName] || []).filter(function (_, idx) { return idx !== i; });
    patch({ pinCollections: cols });
  };
  const addPinCollection = function () {
    const n = window.prompt('Название коллекции', 'collection_' + (Object.keys(project.pinCollections || { default: [] }).length + 1));
    if (!n || !n.trim()) return;
    const cols = Object.assign({}, project.pinCollections || { default: [] });
    cols[n.trim()] = [];
    patch({ pinCollections: cols });
    setPinColName(n.trim());
    say('Коллекция «' + n.trim() + '» создана');
  };
  const findDuplicateIcons = function () {
    if (!atlasTex) return;
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      const fp = {};
      const colsN = Math.floor(img.width / cw), rowsN = Math.floor(img.height / ch);
      for (let y = 0; y < rowsN; y++) for (let x = 0; x < colsN; x++) {
        const sc = document.createElement('canvas'); sc.width = 8; sc.height = 8;
        const sctx = sc.getContext('2d'); sctx.drawImage(cv, x * cw, y * ch, cw, ch, 0, 0, 8, 8);
        const key = Array.from(sctx.getImageData(0, 0, 8, 8).data).join(',');
        (fp[key] = fp[key] || []).push({ x: x, y: y });
      }
      const dups = [];
      Object.keys(fp).forEach(function (k) { if (fp[k].length > 1) dups.push.apply(dups, fp[k]); });
      setDuplicateCells(dups);
      say(dups.length ? 'Найдено ячеек-дубликатов: ' + dups.length : 'Дубликатов не найдено');
    };
    img.src = atlasTex.dataURL;
  };
  const autofillCharDialogs = function () {
    const chars = (project.characters || []).map(function (c) {
      const related = (project.dialogues || []).filter(function (d) { return d.id.indexOf(c.id) !== -1 || c.id.indexOf(d.id) !== -1; });
      if (related.length && !c.start_dialog) return Object.assign({}, c, { start_dialog: related[0].id });
      return c;
    });
    patch({ characters: chars });
    say('Ссылки на диалоги обновлены');
  };
  const exportDds = function (fmt, mip) {
    if (!atlasTex) return;
    const format = fmt || 'DXT1';
    const withMip = !!mip;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      downloadBytes(encodeDds(ctx.getImageData(0, 0, cv.width, cv.height).data, cv.width, cv.height, format, withMip), 'atlas_' + format.toLowerCase() + (withMip ? '_mip' : '') + '.dds', 'application/octet-stream');
      say('DDS экспортирован (' + format + (withMip ? ', mip' : '') + ')');
    };
    img.src = atlasTex.dataURL;
  };
  const copyIcon = function () {
    if (!atlasTex) return;
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      const w = (selSize.w || 1) * cw, h = (selSize.h || 1) * ch;
      setIconClipboard(ctx.getImageData(selCell.x * cw, selCell.y * ch, w, h));
      say('Иконка скопирована (' + selSize.w + '×' + selSize.h + ')');
    };
    img.src = atlasTex.dataURL;
  };
  const pasteIcon = function () {
    if (!atlasTex || !iconClipboard) return;
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      ctx.putImageData(iconClipboard, selCell.x * cw, selCell.y * ch);
      patch({ textures: (project.textures || []).map(function (t, i) { return i === 0 ? Object.assign({}, t, { dataURL: cv.toDataURL('image/png') }) : t; }) });
      say('Иконка вставлена в ' + selCell.x + '×' + selCell.y);
    };
    img.src = atlasTex.dataURL;
  };
  const newAtlas = function () {
    const cols = Math.max(1, +(window.prompt('Колонок', '5') || 5));
    const rows = Math.max(1, +(window.prompt('Рядов', '5') || 5));
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const cv = document.createElement('canvas'); cv.width = cols * cw; cv.height = rows * ch;
    const ctx = cv.getContext('2d'); ctx.clearRect(0, 0, cv.width, cv.height);
    patch({ textures: [{ name: 'new_atlas.png', dataURL: cv.toDataURL('image/png'), width: cv.width, height: cv.height }] });
    say('Пустой атлас создан: ' + cols + '×' + rows + ' ячеек');
  };
  const cropAtlas = function () {
    if (!atlasTex) return;
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const x = selCell.x * cw, y = selCell.y * ch, w = (selSize.w || 1) * cw, h = (selSize.h || 1) * ch;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, x, y, w, h, 0, 0, w, h);
      patch({ textures: [{ name: 'crop_' + w + 'x' + h + '.png', dataURL: cv.toDataURL('image/png'), width: w, height: h }] });
      say('Атлас обрезан до выделения (' + w + '×' + h + ')');
    };
    img.src = atlasTex.dataURL;
  };
  const fillSelection = function () {
    if (!atlasTex) return;
    const color = window.prompt('Цвет заливки (hex, напр. #ff0000)', '#000000');
    if (!color) return;
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const x = selCell.x * cw, y = selCell.y * ch, w = (selSize.w || 1) * cw, h = (selSize.h || 1) * ch;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      ctx.fillStyle = color; ctx.fillRect(x, y, w, h);
      patch({ textures: (project.textures || []).map(function (t, i) { return i === 0 ? Object.assign({}, t, { dataURL: cv.toDataURL('image/png'), width: cv.width, height: cv.height }) : t; }) });
      say('Выделение залито: ' + color);
    };
    img.src = atlasTex.dataURL;
  };
  const alphaSelection = function () {
    if (!atlasTex) return;
    const val = Math.max(0, Math.min(255, +(window.prompt('Прозрачность 0–255 (0 — полностью прозрачно)', '128') || 128)));
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const x = selCell.x * cw, y = selCell.y * ch, w = (selSize.w || 1) * cw, h = (selSize.h || 1) * ch;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      const id = ctx.getImageData(x, y, w, h);
      for (let i = 3; i < id.data.length; i += 4) id.data[i] = val;
      ctx.putImageData(id, x, y);
      patch({ textures: (project.textures || []).map(function (t, i) { return i === 0 ? Object.assign({}, t, { dataURL: cv.toDataURL('image/png'), width: cv.width, height: cv.height }) : t; }) });
      say('Прозрачность выделения: ' + val);
    };
    img.src = atlasTex.dataURL;
  };
  // Отражение выделения по горизонтали/вертикали (#45)
  const flipSelection = function (horizontal) {
    if (!atlasTex) return;
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const x = selCell.x * cw, y = selCell.y * ch, w = (selSize.w || 1) * cw, h = (selSize.h || 1) * ch;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      const id = ctx.getImageData(x, y, w, h);
      const data = id.data, copy = new Uint8ClampedArray(data);
      for (let yy = 0; yy < h; yy++) for (let xx = 0; xx < w; xx++) {
        const sx = horizontal ? (w - 1 - xx) : xx;
        const sy = horizontal ? yy : (h - 1 - yy);
        const di = (yy * w + xx) * 4, si = (sy * w + sx) * 4;
        data[di] = copy[si]; data[di + 1] = copy[si + 1]; data[di + 2] = copy[si + 2]; data[di + 3] = copy[si + 3];
      }
      ctx.putImageData(id, x, y);
      patch({ textures: (project.textures || []).map(function (t, i) { return i === 0 ? Object.assign({}, t, { dataURL: cv.toDataURL('image/png'), width: cv.width, height: cv.height }) : t; }) });
      say(horizontal ? 'Отражение по горизонтали' : 'Отражение по вертикали');
    };
    img.src = atlasTex.dataURL;
  };
  // Поворот выделения на 90° (#45)
  const rotateSelection = function (clockwise) {
    if (!atlasTex) return;
    const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
    const w = (selSize.w || 1) * cw, h = (selSize.h || 1) * ch;
    if (w !== h) { say('Поворот требует квадратное выделение (' + (selSize.w || 1) + '×' + (selSize.h || 1) + '). Сначала выделите квадратную область.'); return; }
    const x = selCell.x * cw, y = selCell.y * ch, n = w;
    const img = new Image();
    img.onload = function () {
      const cv = document.createElement('canvas'); cv.width = img.width; cv.height = img.height;
      const ctx = cv.getContext('2d'); ctx.drawImage(img, 0, 0);
      const id = ctx.getImageData(x, y, n, n);
      const data = id.data, copy = new Uint8ClampedArray(data);
      for (let yy = 0; yy < n; yy++) for (let xx = 0; xx < n; xx++) {
        const sx = clockwise ? (n - 1 - yy) : yy;
        const sy = clockwise ? xx : (n - 1 - xx);
        const di = (yy * n + xx) * 4, si = (sy * n + sx) * 4;
        data[di] = copy[si]; data[di + 1] = copy[si + 1]; data[di + 2] = copy[si + 2]; data[di + 3] = copy[si + 3];
      }
      ctx.putImageData(id, x, y);
      patch({ textures: (project.textures || []).map(function (t, i) { return i === 0 ? Object.assign({}, t, { dataURL: cv.toDataURL('image/png'), width: cv.width, height: cv.height }) : t; }) });
      say(clockwise ? 'Поворот на 90° по часовой' : 'Поворот на 90° против часовой');
    };
    img.src = atlasTex.dataURL;
  };
  const iconsKeyRef = useRef(null);
  iconsKeyRef.current = { pin: pinIcon, undo: undoHist, redo: redoHist };
  useEffect(function () {
    const h = function (e) {
      if (tab !== 'icons') return;
      const t = document.activeElement && document.activeElement.tagName;
      if (t === 'INPUT' || t === 'TEXTAREA' || t === 'SELECT') return;
      if (e.key === 'ArrowLeft') { e.preventDefault(); setSelCell(function (c) { return { x: Math.max(0, c.x - 1), y: c.y }; }); }
      else if (e.key === 'ArrowRight') { e.preventDefault(); setSelCell(function (c) { return { x: c.x + 1, y: c.y }; }); }
      else if (e.key === 'ArrowUp') { e.preventDefault(); setSelCell(function (c) { return { x: c.x, y: Math.max(0, c.y - 1) }; }); }
      else if (e.key === 'ArrowDown') { e.preventDefault(); setSelCell(function (c) { return { x: c.x, y: c.y + 1 }; }); }
      else if (e.key === 'Enter') { e.preventDefault(); if (iconsKeyRef.current) iconsKeyRef.current.pin(); }
      else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') { e.preventDefault(); if (iconsKeyRef.current) iconsKeyRef.current.undo(); }
      else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') { e.preventDefault(); if (iconsKeyRef.current) iconsKeyRef.current.redo(); }
    };
    window.addEventListener('keydown', h);
    return function () { window.removeEventListener('keydown', h); };
  }, [tab]);

  const exportItemsCsv = function () {
    const rows = [['section', 'key', 'value']];
    parsedItems.sections.forEach(function (s) { s.entries.forEach(function (en) { rows.push([s.name, en.key, en.value]); }); });
    const csv = rows.map(function (r) { return r.map(csvCell).join(';'); }).join(NL);
    download(csv, 'items.csv', 'text/csv', true);
  };

  const exportItemsLtx = function () { downloadBytes(textBytes(project.itemsLtx || '', (project.settings && project.settings.exportEncoding) || 'windows-1251'), 'items.ltx', 'text/plain'); };

  const updateLtxFile = function (path, text) {
    patch({ ltxFiles: Object.assign({}, project.ltxFiles || {}, (function () { const o = {}; o[path] = text; return o; })()) });
  };
  const deleteLtxFile = function (path) {
    const next = Object.assign({}, project.ltxFiles || {});
    delete next[path];
    patch({ ltxFiles: next });
    if (selLtxPath === path) setSelLtxPath(null);
  };

  const setItemField = function (lineNo, key, value) {
    const lines = splitLines(project.itemsLtx);
    const orig = lines[lineNo - 1] || '';
    const ci = orig.indexOf(';');
    const comment = ci !== -1 ? ' ' + orig.slice(ci) : '';
    patch({ itemsLtx: updateLtxLine(project.itemsLtx, lineNo, key + ' = ' + value + comment) });
  };
  const setItemGrid = function (x, y) {
    if (!selSection) return;
    let text = project.itemsLtx;
    const gx = selSection.entries.find(function (e) { return e.key === 'inv_grid_x'; });
    const gy = selSection.entries.find(function (e) { return e.key === 'inv_grid_y'; });
    if (gx) text = updateLtxLine(text, gx.line, 'inv_grid_x = ' + x);
    else text = addFieldToSection(text, selSection.name, 'inv_grid_x', String(x));
    if (gy) text = updateLtxLine(text, gy.line, 'inv_grid_y = ' + y);
    else text = addFieldToSection(text, selSection.name, 'inv_grid_y', String(y));
    mutateItems(text);
  };
  const setSysEntry = function (line, key, value) {
    const lines = splitLines(project.systemLtx);
    const orig = lines[line - 1] || '';
    const ci = orig.indexOf(';');
    const comment = ci !== -1 ? ' ' + orig.slice(ci) : '';
    patch({ systemLtx: updateLtxLine(project.systemLtx, line, key + ' = ' + value + comment) });
  };
  const removeSysEntry = function (line) {
    const lines = splitLines(project.systemLtx);
    if (line >= 1 && line <= lines.length) patch({ systemLtx: lines.filter(function (_, i) { return i !== line - 1; }).join(NL) });
  };
  const addSysEntry = function (sectionName) {
    patch({ systemLtx: addFieldToSection(project.systemLtx, sectionName, 'new_key', 'value') });
  };
  const addSysSection = function () {
    const name = window.prompt('Имя секции', 'new_section');
    if (!name || !name.trim()) return;
    patch({ systemLtx: addSection(project.systemLtx, name.trim()) });
  };

  const mutateItems = function (newText) {
    setItemsHist(function (s) { return itemsHistory.push(s, project.itemsLtx); });
    patch({ itemsLtx: newText });
  };
  const undoItems = function () {
    setItemsHist(itemsHistory.move(itemsHist, -1, function (t) { if (t != null) patch({ itemsLtx: t }); }));
  };
  const redoItems = function () {
    setItemsHist(itemsHistory.move(itemsHist, +1, function (t) { if (t != null) patch({ itemsLtx: t }); }));
  };

  const addItem = function () {
    const name = 'new_item_' + Math.random().toString(36).slice(2, 6);
    mutateItems(addSection(project.itemsLtx, name));
    setSelItem(name); say('Предмет создан');
  };

  const deleteItem = function (name) {
    mutateItems(removeSection(project.itemsLtx, name));
    setSelItem(null); say('Предмет удалён', { label: 'Отменить', action: undoItems });
  };
  const unlockRootSection = function (name) {
    if (window.confirm('Разблокировать базовую секцию «' + name + '»? Изменения затронут все дочерние секции.')) {
      setUnlockedRoots(Object.assign({}, unlockedRoots, (function () { const o = {}; o[name] = true; return o; })()));
      say('Секция «' + name + '» разблокирована');
    }
  };
  const renameItemSection = function () {
    if (!selItem) { say('Выберите секцию для переименования'); return; }
    const nn = window.prompt('Новое имя секции', selItem);
    if (!nn || !nn.trim()) return;
    const nn2 = nn.trim();
    mutateItems(renameSection(project.itemsLtx, selItem, nn2));
    setSelItem(nn2); say('Секция переименована: ' + selItem + ' → ' + nn2);
  };
  const replaceItemField = function () {
    const key = window.prompt('Имя поля (key), во всех секциях', 'cost');
    if (!key || !key.trim()) return;
    const from = window.prompt('Заменить подстроку (from)', '');
    if (from == null) return;
    const to = window.prompt('На подстроку (to)', '');
    if (to == null) return;
    mutateItems(replaceFieldValues(project.itemsLtx, key.trim(), from, to));
    say('Массовая замена в поле «' + key.trim() + '» выполнена');
  };

  const treeLines = useMemo(function () {
    const out = [];
    const seen = new Set();
    function walk(id, pref) {
      const n = nodes.find(function (x) { return x.id === id; }); if (!n) return;
      if (seen.has(id)) { out.push(pref + '↩ ' + id); return; }
      seen.add(id);
      out.push(pref + (NODE_TYPES[n.type] ? NODE_TYPES[n.type].icon : '•') + ' ' + (NODE_TYPES[n.type] ? NODE_TYPES[n.type].label : n.type) + ' · ' + id);
      edges.filter(function (e) { return e.source === id; }).forEach(function (e) { walk(e.target, pref + '  '); });
    }
    const start = nodes.find(function (n) { return n.type === 'START'; }) || nodes[0];
    if (start) walk(start.id, '');
    nodes.filter(function (n) { return !seen.has(n.id); }).forEach(function (n) { out.push('Недостижимо: ' + n.id); });
    return out;
  }, [nodes, edges]);

  const mutateQuests = function (newQuests) {
    setQuestsHist(function (s) { return questsHistory.push(s, project.quests); });
    patch({ quests: newQuests });
  };
  const undoQuests = function () {
    setQuestsHist(questsHistory.move(questsHist, -1, function (t) { if (t != null) patch({ quests: t }); }));
  };
  const redoQuests = function () {
    setQuestsHist(questsHistory.move(questsHist, +1, function (t) { if (t != null) patch({ quests: t }); }));
  };
  const setQuestField = function (id, key, value) {
    patch({ quests: (project.quests || []).map(function (q) { return q.id === id ? Object.assign({}, q, (function () { const o = {}; o[key] = value; return o; })()) : q; }) });
  };
  const setQuestObjectiveField = function (id, idx, key, value) {
    patch({ quests: (project.quests || []).map(function (q) { return q.id === id ? Object.assign({}, q, { objectives: (q.objectives || []).map(function (o, i) { return i === idx ? Object.assign({}, o, (function () { const oo = {}; oo[key] = value; return oo; })()) : o; }) }) : q; }) });
  };
  const setQuestRequires = function (id, value) {
    const arr = String(value || '').split(/[,\s]+/).map(function (s) { return s.trim(); }).filter(Boolean);
    setQuestField(id, 'requires', arr);
  };
  const addQuestObjective = function (id) {
    mutateQuests((project.quests || []).map(function (q) { return q.id === id ? Object.assign({}, q, { objectives: (q.objectives || []).concat([{ text: '', icon: '', infoportion: '', article: '' }]) }) : q; }));
  };
  const removeQuestObjective = function (id, idx) {
    mutateQuests((project.quests || []).map(function (q) { return q.id === id ? Object.assign({}, q, { objectives: (q.objectives || []).filter(function (_, i) { return i !== idx; }) }) : q; }));
  };
  const addQuest = function () {
    const id = 'quest_' + Math.random().toString(36).slice(2, 6);
    const q = { id: id, prior: '10', title: id + '_title', text: id + '_text', objectives: [{ text: id + '_objective', icon: '', infoportion: '', article: '' }], infoportion_complete: id + '_complete', infoportion_fail: '', icon: '', location: '', article: '', category: '', requires: [], reward: '', format: 'game_tasks', sourcePath: '', original: null };
    mutateQuests((project.quests || []).concat([q]));
    setSelQuest(id);
    say('Квест создан');
  };
  const addQuestFromTemplate = function (tpl) {
    const id = 'quest_' + (tpl.prefix || 'quest') + '_' + Math.random().toString(36).slice(2, 6);
    const q = { id: id, prior: tpl.prior || '10', title: id + '_title', text: id + '_text', objectives: [{ text: id + '_objective', icon: '', infoportion: id + '_complete', article: '' }], infoportion_complete: id + '_complete', infoportion_fail: '', icon: '', location: '', article: '', category: '', requires: [], reward: '', format: 'game_tasks', sourcePath: '', original: null };
    mutateQuests((project.quests || []).concat([q]));
    setSelQuest(id);
    say('Квест «' + tpl.label + '» создан');
  };
  const deleteQuest = function (id) {
    mutateQuests((project.quests || []).filter(function (q) { return q.id !== id; }));
    setSelQuest(null);
    say('Квест удалён', { label: 'Отменить', action: undoQuests });
  };
  const bindQuestToLocales = function (q) {
    const ids = [q.title, q.text].concat((q.objectives || []).map(function (o) { return typeof o === 'string' ? o : o.text; })).filter(Boolean);
    let langs = Array.isArray(project.languages) ? project.languages.slice() : [];
    if (!langs.length) langs = [{ locale: 'rus', name: 'Русский', entries: [] }];
    const nextLangs = langs.map(function (l) {
      const entries = (l.entries || []).slice();
      ids.forEach(function (id) { if (!entries.some(function (e) { return e.id === id; })) entries.push({ id: id, text: '' }); });
      return Object.assign({}, l, { entries: entries });
    });
    patch({ languages: nextLangs });
    say('Квест привязан к локалям (' + ids.length + ' id)');
  };
  const relatedDialoguesForQuest = function (q) {
    const refs = [q.infoportion_complete, q.infoportion_fail, q.id].filter(Boolean);
    const out = [];
    (project.dialogues || []).forEach(function (d) {
      const hits = (d.nodes || []).filter(function (n) {
        const m = (n.data && n.data.metadata) || {};
        const all = [].concat(m.give_info || [], m.disable_info || [], m.has_info || []);
        return all.some(function (v) { return refs.some(function (r) { return String(v).indexOf(r) !== -1 || r.indexOf(String(v)) !== -1; }); });
      });
      if (hits.length) out.push({ dlg: d, hits: hits.length });
    });
    return out;
  };
  const exportQuests = function () {
    const xml = questsToXml(project.quests || [], (project.settings && project.settings.exportEncoding) || 'windows-1251');
    const file = (project.quests || []).some(function (q) { return q.format === 'task_manager'; }) ? 'task_manager.xml' : 'game_tasks.xml';
    downloadBytes(textBytes(xml, (project.settings && project.settings.exportEncoding) || 'windows-1251'), file, 'application/xml');
    say('Квесты экспортированы (' + (project.quests || []).length + ')');
  };

  const filteredTypes = Object.entries(NODE_TYPES).filter(function (kv) { return PALETTE_HIDDEN.indexOf(kv[0]) === -1 && (kv[0] + kv[1].label + kv[1].desc).toLowerCase().indexOf(paletteFilter.toLowerCase()) !== -1; });
  const btn = 'px-3 py-2 rounded-lg text-xs font-semibold border transition ' + (dark ? 'border-zinc-700 bg-zinc-800 hover:bg-zinc-700 text-zinc-100' : 'border-slate-300 bg-white hover:bg-slate-100 text-slate-800');
  const accentColors = ACCENT_PRESETS[programSettings.accent] || ACCENT_PRESETS.green;
  const accentBtn = 'px-3 py-2 rounded-lg text-xs font-semibold text-white transition accent';
  const field = 'rounded-lg border px-3 py-2 text-xs outline-none ' + ui.field;

  const luaColor = { comment: '#94a3b8', string: '#16a34a', keyword: '#2563eb', number: '#22c55e', bracket: '#4ade80', ident: 'inherit', plain: 'inherit' };

  const TABS = [
    ['dialogues', '💬', t('tabDialogues')], ['characters', '🧍', t('tabCharacters')], ['items', '🎒', t('tabItems')], ['icons', '🖼', t('tabIcons')],
    ['scripts', '📜', t('tabScripts')], ['system', '🧾', t('tabSystem')], ['languages', '🌐', t('tabLanguages')], ['quests', '🎯', t('tabQuests')],
    ['overview', '📊', t('tabOverview')]
  ];
  const tabCounts = useMemo(function () {
    return {
      dialogues: (project.dialogues || []).length,
      characters: (project.characters || []).length,
      items: parsedItems.sections.length,
      icons: (project.pinnedIcons || []).length,
      scripts: (project.scripts || []).length,
      system: Object.keys(project.ltxFiles || {}).length,
      languages: (project.languages || []).length,
      quests: (project.quests || []).length,
    };
  }, [project, parsedItems]);
  const brokenEdges = useMemo(function () { const ids = new Set(nodes.map(function (n) { return n.id; })); return edges.filter(function (e) { return !ids.has(e.source) || !ids.has(e.target) || e.broken; }).length; }, [nodes, edges]);
  const emptyTranslations = useMemo(function () { return (project.languages || []).reduce(function (sum, l) { return sum + (l.entries || []).filter(function (e) { return !String(e.text || '').trim(); }).length; }, 0); }, [project.languages]);
  const healthIssues = brokenEdges + emptyTranslations;

  useEffect(function () {
    const key = function (e) {
      const tag = (document.activeElement && document.activeElement.tagName) || '';
      const typing = tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT';
      const hk = (programSettings && programSettings.hotkeys) || {};
      const h = function (id) { return hk[id] || defaultHotkeys()[id]; };
      const m = function (id) { return comboMatches(e, h(id)); };
      const undoActive = function () {
        if (tab === 'dialogues') undoGraph();
        else if (tab === 'items') undoItems();
        else if (tab === 'quests') undoQuests();
        else if (tab === 'icons') undoHist();
      };
      const redoActive = function () {
        if (tab === 'dialogues') redoGraph();
        else if (tab === 'items') redoItems();
        else if (tab === 'quests') redoQuests();
        else if (tab === 'icons') redoHist();
      };
      if (m('save')) { e.preventDefault(); saveProject(); return; }
      if (m('exportXml')) { e.preventDefault(); exportXml(); return; }
      if (m('globalSearch') || ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'f')) { e.preventDefault(); setGlobalSearchOpen(true); return; }
      if (!typing) {
        if (m('undo')) { e.preventDefault(); undoActive(); return; }
        if (m('redo')) { e.preventDefault(); redoActive(); return; }
        if (m('addStart')) { addNode('START'); return; }
        if (m('addPhrase')) { addNode('PHRASE'); return; }
        if (m('addRandom')) { addNode('RANDOM'); return; }
        if (m('addEnd')) { addNode('END'); return; }
        if ((m('deleteSelected') || e.key === 'Backspace') && selected) { e.preventDefault(); deleteNode(selected); return; }
      }
      if ((e.ctrlKey || e.metaKey) && /^[1-9]$/.test(e.key)) { e.preventDefault(); const tt = TABS[+e.key - 1]; if (tt) setTab(tt[0]); }
      if (e.key === '?' && !typing) { setShortcutsOpen(true); }
      if (m('escape')) { setConnectFrom(null); setCtxMenu(null); setGlobalSearchOpen(false); setShortcutsOpen(false); }
    };
    window.addEventListener('keydown', key);
    return function () { window.removeEventListener('keydown', key); };
  });

  if (welcomeOpen) {
    return React.createElement('div', { className: 'h-screen w-screen overflow-auto flex flex-col ' + ui.app },
      React.createElement('div', { className: 'max-w-4xl w-full mx-auto px-6 py-10 grid gap-6' },
        React.createElement('header', { className: 'flex items-center justify-between gap-4' },
          React.createElement('div', { className: 'flex items-center gap-4' },
            React.createElement('div', { className: 'w-14 h-14 rounded-2xl accent text-white grid place-items-center font-bold text-2xl' }, '☢'),
            React.createElement('div', {},
              React.createElement('h1', { className: 'font-bold text-2xl' }, 'X-Ray Forge'),
              React.createElement('p', { className: 'text-sm ' + ui.muted }, 'Редактор диалогов, персонажей, предметов, иконок и локализаций.')
            )
          ),
          React.createElement('button', { className: btn, onClick: function () { setWelcomeOpen(false); } }, t('continue') + ' →')
        ),

        React.createElement('section', { className: 'rounded-2xl border p-5 grid gap-4 ' + ui.panel },
          React.createElement('h2', { className: 'font-bold' }, 'Новый проект'),
          React.createElement('div', { className: 'grid md:grid-cols-3 gap-3' },
            React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Название',
              React.createElement('input', { className: field, value: newName, onChange: function (e) { setNewName(e.target.value); }, placeholder: 'new_dialogue' })),
            React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Серия игры',
              React.createElement('select', { className: field, value: newSeries, onChange: function (e) { setNewSeries(e.target.value); } },
                SERIES.map(function (s) { return React.createElement('option', { key: s.value, value: s.value }, s.label); }))),
            React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Тема',
              React.createElement('select', { className: field, value: newTheme, onChange: function (e) { setNewTheme(e.target.value); } },
                React.createElement('option', { value: 'dark' }, 'Тёмная'),
                React.createElement('option', { value: 'light' }, 'Светлая'),
                React.createElement('option', { value: 'auto' }, 'Авто')))
          ),
          React.createElement('div', { className: 'flex flex-wrap gap-2' },
            React.createElement('button', { className: accentBtn, onClick: createProject }, '＋ ' + t('createProject')),
            React.createElement('button', { className: btn, onClick: function () { importRef.current.click(); } }, t('importProject')),
            React.createElement('button', { className: btn, onClick: function () { loadDemo(); setWelcomeOpen(false); } }, t('demoGraph'))
          )
        ),

        React.createElement('section', { className: 'rounded-2xl border p-5 grid gap-3 ' + ui.panel },
          React.createElement('h2', { className: 'font-bold' }, t('savedProjects')),
          projects.length === 0
            ? React.createElement('p', { className: 'text-xs ' + ui.muted }, 'Сохранённых проектов нет. Создайте новый или импортируйте существующий.')
            : projects.map(function (p) {
                const s = SERIES.find(function (x) { return x.value === (p.data && p.data.series); }) || SERIES[0];
                return React.createElement('div', { key: p.name + '_' + p.updatedAt, className: 'flex items-center justify-between gap-3 rounded-xl border px-4 py-3 ' + ui.card },
                  React.createElement('button', { className: 'text-left flex-1 min-w-0', onClick: function () { openProjectItem(p); } },
                    React.createElement('div', { className: 'text-sm font-semibold truncate' }, p.name),
                    React.createElement('div', { className: 'text-[11px] ' + ui.muted }, s.label + ' · ' + new Date(p.updatedAt).toLocaleString())
                  ),
                  React.createElement('div', { className: 'flex gap-2 shrink-0' },
                    React.createElement('button', { className: btn, onClick: function () { openProjectItem(p); } }, 'Открыть'),
                    React.createElement('button', { className: btn, onClick: function () { renameProjectItem(p); } }, 'Переименовать'),
                    React.createElement('button', { className: 'text-red-500 text-xs', onClick: function () { deleteProjectItem(p.name); } }, 'Удалить')
                  )
                );
              })
        ),

        React.createElement('p', { className: 'text-[11px] text-center ' + ui.muted }, 'Данные хранятся локально в браузере. Для резервной копии экспортируйте проект в .json.')
      )
    );
  }

  // Единый контекст для вынесенных вкладок (src/tabs/*).
  const ctx = {
    // состояния и сеттеры
    project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen,
    programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme,
    metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch,
    scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff,
    includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys,
    itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml,
    paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull,
    langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee,
    scriptViewMode, setScriptViewMode, ltxViewMode, setLtxViewMode, collapsedBlocks, paletteFilter, setPaletteFilter,
    graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults,
    pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory,
    selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell,
    histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar,
    selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView,
    selLtxPath, setSelLtxPath, spawnData, setSpawnData, unlockedRoots, unlockRootSection,
    questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen,
    activePhraseLocale, setActivePhraseLocale, projects, toast, dirty, globalSearchOpen, setGlobalSearchOpen, globalSearchQuery, setGlobalSearchQuery, ctxMenu, setCtxMenu, listSort, setListSort, playthrough, startPlaythrough, stepPlaythrough, exitPlaythrough,
    // refs
    canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, spawnRef, stringsRef, scriptTextRef,
    // производные значения
    dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex,
    selSection, itemsByName, ltxSectionsMap, ltxSectionNames, includeRows, includeSvg, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, tabCounts,
    // обработчики
    patch, patchNode, say, addNode, duplicateNode, openCtxMenu, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject,
    exportProject, exportXml, exportGameZip, exportAllDialogues, openProjectItem, deleteProjectItem, renameProjectItem, createProject,
    loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder,
    scanScripts, importStringTables, updateScript, insertSnippet, insertSnippetText, insertLuaAtCursor, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock,
    deleteScript, renameScript, loadDds, loadSpawn, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon,
    unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, cropAtlas, fillSelection, alphaSelection, flipSelection, rotateSelection, exportSpritesheet,
    autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry,
    addSysEntry, addSysSection, updateLtxFile, deleteLtxFile, mutateItems, undoItems, redoItems, addItem, deleteItem, renameItemSection, replaceItemField, setQuestField,
    setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests,
    redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests,
    undoGraph, redoGraph, graphToSvg,
    // модульные константы и утилиты
    NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES,
    ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, suggestTextId, validateMetadataSyntax, dialogueGroup, parseLtx,
    parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, questChainSvg, parseCondlistEntry, condlistEntryToStr, includeTreeRows,
    includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua,
    luaFunctionBlocks, luaCheck, luaUndeclaredGlobals, LUA_API, LUA_SNIPPETS, LUA_ENGINE_GLOBALS, luaCallGraph, luaUnused, luaCompletions, validateScriptBindings, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives,
    validateLtxRefs, findDuplicateSections, findInheritanceCycles, renameSection, replaceFieldValues, gameFilesFromDialogues, validateTextIds, dialogueToXml, findTextIdIssues, resolveTextId,
    mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes,
    escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, ITEM_FIELD_PROFILES, itemFieldProfile, highlightLtx, ltxInheritanceTree, FIELD_DESCRIPTIONS,
    parseStringTableXml, parseGeneralCharactersXml, parseCharacterDescGeneral, characterDescGeneralToXml, analyzeSpawn,
    parseInfoPortions, parseEncyclopedia, parseSquadDescr, parseRatingScales, parseSecrets, parseSpawnSections, parseMonsters, parseActor, parseSurgeManager, parseNpcProfile, parseTrade, parseTextureDescriptor
  };

  return React.createElement('div', { className: 'h-screen w-screen overflow-hidden flex flex-col ' + ui.app, style: { '--accent': accentColors[0], '--accent-hover': accentColors[1] } },
    !canvasFull && React.createElement('header', { className: 'h-14 px-4 flex items-center justify-between border-b ' + ui.panel },
      React.createElement('div', { className: 'flex items-center gap-3 min-w-0' },
        React.createElement('div', { className: 'w-9 h-9 rounded-xl accent text-white grid place-items-center font-bold' }, '☢'),
        React.createElement('div', { className: 'min-w-0' },
          React.createElement('div', { className: 'font-bold leading-tight truncate' }, 'X-Ray Forge', dirty ? React.createElement('span', { className: 'text-amber-400', title: 'Есть несохранённые изменения' }, ' ●') : null),
          React.createElement('div', { className: 'text-[11px] ' + ui.muted + ' truncate' }, project.projectName + ' · ' + (SERIES.find(function (s) { return s.value === project.series; }) || SERIES[0]).label)
        )
      ),
      React.createElement('div', { className: 'flex items-center gap-2' },
        React.createElement('select', { className: btn + ' max-w-[150px]', value: project.projectName, title: t('projects'), onChange: function (e) { switchProject(e.target.value); } },
          (function () {
            const names = projects.map(function (p) { return p.name; });
            if (names.indexOf(project.projectName) === -1) names.unshift(project.projectName);
            return names.map(function (n) { return React.createElement('option', { key: n, value: n }, n); });
          })()
        ),
        lastAutosave && React.createElement('span', { className: 'text-[11px] ' + ui.muted }, 'Автосохр. ' + lastAutosave.toLocaleTimeString()),
        React.createElement('button', { className: btn, onClick: function () { setWelcomeOpen(true); } }, '🏠 ' + t('projects')),
        React.createElement('button', { className: btn, onClick: saveProject }, '💾 ' + t('save')),
        React.createElement('button', { className: btn, onClick: function () { setSettingsOpen(true); } }, '📁 ' + t('projectSettings')),
        React.createElement('button', { className: btn, onClick: function () { setGlobalSearchOpen(true); } }, '🔍 ' + t('globalSearch')),
        React.createElement('button', { className: accentBtn, onClick: function () { setProgramSettingsOpen(true); } }, '⚙ ' + t('programSettings')),
        React.createElement('button', { className: btn, onClick: function () { setHelpOpen(true); } }, '❓ ' + t('help'))
      )
    ),
    !canvasFull && React.createElement('nav', { className: 'h-11 px-3 flex items-center gap-1 border-b overflow-x-auto ' + ui.soft },
      TABS.map(function (t, idx) {
        return React.createElement('button', {
          key: t[0], onClick: function () { setTab(t[0]); },
          title: t[2] + ' · Ctrl+' + (idx + 1),
          className: 'px-2.5 py-1.5 rounded-lg text-xs font-semibold whitespace-nowrap flex items-center gap-1.5 ' + (tab === t[0] ? 'accent text-white' : ui.muted + ' ' + ui.hover)
        },
          React.createElement('span', { className: 'text-[9px] font-mono opacity-50' }, idx + 1),
          React.createElement('span', {}, t[1]),
          React.createElement('span', {}, t[2]),
          tabCounts[t[0]] ? React.createElement('span', { className: 'text-[10px] px-1 rounded-full ' + (tab === t[0] ? 'bg-white/20 text-white' : (dark ? 'bg-zinc-700/70 text-zinc-300' : 'bg-slate-200 text-slate-600')) }, tabCounts[t[0]]) : null
        );
      }),
      tab === 'dialogues' && React.createElement('span', { className: 'ml-auto text-[11px] ' + ui.muted + ' whitespace-nowrap' }, 'Нод: ' + nodes.length + ' · Связей: ' + edges.length)
    ),
    React.createElement('input', { ref: importRef, type: 'file', accept: '.json', className: 'hidden', onChange: importProject }),
    React.createElement('input', { ref: gamedataRef, type: 'file', className: 'hidden', multiple: true, webkitdirectory: '', directory: '', onChange: scanGamedataFiles }),
    React.createElement('input', { ref: scriptRef, type: 'file', accept: '.script,.lua,.txt', multiple: true, className: 'hidden', onChange: scanScripts }),
    React.createElement('input', { ref: ddsRef, type: 'file', accept: '.dds', className: 'hidden', onChange: loadDds }),
    React.createElement('input', { ref: compareRef, type: 'file', accept: '.dds', className: 'hidden', onChange: loadCompareDds }),
    React.createElement('input', { ref: spawnRef, type: 'file', accept: '.spawn', className: 'hidden', onChange: loadSpawn }),
    React.createElement('input', { ref: stringsRef, type: 'file', accept: '.xml', multiple: true, className: 'hidden', onChange: importStringTables }),

    tab === 'dialogues' && renderDialoguesTab(ctx),

    tab === 'characters' && renderCharactersTab(ctx),

    tab === 'items' && renderItemsTab(ctx),

    tab === 'icons' && renderIconsTab(ctx),

    tab === 'scripts' && renderScriptsTab(ctx),

    tab === 'system' && renderSystemTab(ctx),

    tab === 'languages' && renderLanguagesTab(ctx),

    tab === 'quests' && renderQuestsTab(ctx),

    tab === 'selfcheck' && React.createElement('div', { className: 'flex-1 p-6 overflow-auto' },
      React.createElement('div', { className: 'max-w-3xl mx-auto grid gap-4' },
        React.createElement('div', { className: 'flex items-center justify-between' },
          React.createElement('div', {},
            React.createElement('h2', { className: 'font-bold' }, 'Самопроверка'),
            React.createElement('p', { className: 'text-xs ' + ui.muted }, 'Встроенные тесты и проверка целостности проекта.')
          ),
          React.createElement('div', { className: 'flex gap-2' },
            React.createElement('button', { className: accentBtn, onClick: function () { setSelfResults(runSelfCheck()); } }, 'Запустить тесты'),
            React.createElement('button', { className: btn, onClick: runIntegrity }, 'Проверить целостность')
          )
        ),
        React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
          React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Целостность проекта (' + integrityResults.length + ')'),
          integrityResults.length ? integrityResults.map(function (r, i) { return React.createElement('div', { key: i, className: 'text-xs ' + (r.where === 'system.ltx' ? 'text-red-400' : 'text-amber-400') }, '[' + r.where + '] ' + r.msg); })
            : React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Проблем не найдено или проверка не запускалась.')
        ),
        selfResults.map(function (r, i) {
          return React.createElement('div', { key: i, className: 'rounded-xl border p-3 flex items-center gap-3 ' + ui.card },
            React.createElement('span', { className: 'text-lg' }, r.ok ? '✅' : '❌'),
            React.createElement('div', { className: 'flex-1' },
              React.createElement('div', { className: 'text-sm font-semibold' }, r.name),
              r.detail && React.createElement('div', { className: 'text-xs ' + ui.muted }, r.detail)
            )
          );
        }),
        selfResults.length === 0 && React.createElement('p', { className: ui.muted }, 'Нажмите «Запустить тесты».')
      )
    ),

    tab === 'overview' && React.createElement('div', { className: 'flex-1 p-6 overflow-auto' },
      React.createElement('div', { className: 'max-w-5xl mx-auto grid gap-4' },
        React.createElement('div', { className: 'flex items-center justify-between' },
          React.createElement('div', {},
            React.createElement('h2', { className: 'font-bold' }, t('overviewTitle')),
            React.createElement('p', { className: 'text-xs ' + ui.muted }, t('overviewSub'))
          ),
          React.createElement('div', { className: 'flex gap-2' },
            React.createElement('button', { className: accentBtn, onClick: saveProject }, '💾 Сохранить'),
            React.createElement('button', { className: btn, onClick: validate }, t('checkGraph'))
          )
        ),
        React.createElement('div', { className: 'grid grid-cols-2 md:grid-cols-4 gap-3' },
          [
            ['Диалоги', (project.dialogues || []).length, 'dialogues'],
            ['Персонажи', (project.characters || []).length, 'characters'],
            ['Предметы', parsedItems.sections.length, 'items'],
            ['Иконки', (project.pinnedIcons || []).length, 'icons'],
            ['Скрипты', (project.scripts || []).length, 'scripts'],
            ['Файлы LTX', Object.keys(project.ltxFiles || {}).length, 'system'],
            ['Языки', (project.languages || []).length, 'languages'],
            ['Квесты', (project.quests || []).length, 'quests']
          ].map(function (s) {
            return React.createElement('button', { key: s[2], className: 'rounded-xl border p-4 text-left ' + ui.card + ' ' + ui.hover, onClick: function () { setTab(s[2]); } },
              React.createElement('div', { className: 'text-2xl font-bold' }, s[1]),
              React.createElement('div', { className: 'text-xs ' + ui.muted }, s[0])
            );
          })
        ),
        React.createElement('div', { className: 'grid md:grid-cols-2 gap-3' },
          React.createElement('div', { className: 'rounded-xl border p-4 grid gap-2 ' + ui.card },
            React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Граф диалога'),
            React.createElement('div', { className: 'text-sm' }, 'Нод: ' + nodes.length + ' · Связей: ' + edges.length + ' · Старт: ' + nodes.filter(function (n) { return n.type === 'START'; }).length + ' · Конец: ' + nodes.filter(function (n) { return n.type === 'END'; }).length),
            React.createElement('div', { className: 'text-sm' }, 'Битых связей: ' + errors.filter(function (e) { return e.code === 'broken_edge'; }).length + ' · Предупреждений: ' + errors.filter(function (e) { return e.severity === 'warning'; }).length)
          ),
          React.createElement('div', { className: 'rounded-xl border p-4 grid gap-2 ' + ui.card },
            React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Проект'),
            React.createElement('div', { className: 'text-sm' }, 'Серия: ' + (SERIES.find(function (s) { return s.value === project.series; }) || SERIES[0]).label),
            React.createElement('div', { className: 'text-sm' }, 'Кодировка экспорта: ' + ((project.settings && project.settings.exportEncoding) || 'windows-1251')),
            React.createElement('div', { className: 'text-sm ' + (dirty ? 'text-amber-400' : ui.muted) }, dirty ? '● Есть несохранённые изменения' : 'Изменения сохранены')
          )
        )
      )
    ),

    settingsOpen && React.createElement('div', { className: 'fixed inset-0 z-50 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4' },
      React.createElement('div', { className: 'w-full max-w-3xl max-h-[90vh] flex flex-col rounded-2xl border overflow-hidden ' + ui.panel },
        React.createElement('div', { className: 'p-5 border-b border-inherit flex items-center justify-between' },
          React.createElement('div', {},
            React.createElement('h2', { className: 'font-bold text-lg' }, 'Настройки проекта'),
            React.createElement('p', { className: 'text-xs ' + ui.muted }, 'Название, диалог, серия и папки gamedata.')
          ),
          React.createElement('button', { className: btn + ' text-lg', onClick: function () { setSettingsOpen(false); } }, '×')
        ),
        React.createElement('div', { className: 'p-5 grid gap-4 overflow-y-auto flex-1 min-h-0' },
          React.createElement('div', { className: 'grid md:grid-cols-2 gap-4' },
            React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Название проекта', React.createElement('input', { className: field, value: project.projectName, onChange: function (e) { patch({ projectName: e.target.value }); } })),
            React.createElement('label', { className: 'grid gap-1 text-xs' }, 'ID диалога', React.createElement('input', { className: field, value: project.dialogueId || project.projectName, onChange: function (e) { patch({ dialogueId: e.target.value }); } })),
            React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Серия игры', React.createElement('select', { className: field, value: project.series, onChange: function (e) { patch({ series: e.target.value }); } }, SERIES.map(function (s) { return React.createElement('option', { key: s.value, value: s.value }, s.label); }))),
            React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Импорт: кодировка', React.createElement('select',{className:field,value:(project.settings||{}).importEncoding||'auto',onChange:function(e){patch({settings:Object.assign({},project.settings,{importEncoding:e.target.value})});}},React.createElement('option',{value:'auto'},'Авто'),React.createElement('option',{value:'windows-1251'},'Windows-1251'),React.createElement('option',{value:'utf-8'},'UTF-8'))),
            React.createElement('label', { className: 'grid gap-1 text-xs md:col-span-2' }, 'Экспорт: кодировка', React.createElement('select',{className:field,value:(project.settings||{}).exportEncoding||'windows-1251',onChange:function(e){patch({settings:Object.assign({},project.settings,{exportEncoding:e.target.value})});}},React.createElement('option',{value:'windows-1251'},'Windows-1251'),React.createElement('option',{value:'utf-8'},'UTF-8')))
          ),
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'gamedata (редактируемая)',
            React.createElement('div', { className: 'flex gap-2' },
              React.createElement('input', { className: field + ' flex-1', value: project.gamedataPath || '', onChange: function (e) { patch({ gamedataPath: e.target.value }); }, placeholder: 'путь или имя папки' }),
              React.createElement('button', { className: accentBtn, onClick: pickGamedataFolder }, 'Выбрать и сканировать')
            )
          ),
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'gamedata с ресурсами',
            React.createElement('div', { className: 'flex gap-2' },
              React.createElement('input', { className: field + ' flex-1', value: project.resourcesPath || '', onChange: function (e) { patch({ resourcesPath: e.target.value }); }, placeholder: 'опционально' }),
              React.createElement('button', { className: btn, onClick: pickResourcesFolder }, 'Выбрать')
            )
          ),
          React.createElement('div', { className: 'grid gap-2 text-xs' },
            React.createElement('div', { className: 'font-semibold' }, 'Экспорт диалогов (в какой XML)'),
            (function () {
              const list = [{ id: project.dialogueId || project.projectName, name: project.projectName }];
              (project.dialogues || []).forEach(function (d) { const id = d.id || d.name; if (!list.some(function (x) { return x.id === id; })) list.push({ id: id, name: d.name }); });
              return list.map(function (d) {
                const val = (dlgExportFiles || {})[d.id] || d.id;
                return React.createElement('div', { key: d.id, className: 'flex items-center gap-2' },
                  React.createElement('span', { className: 'font-mono text-[11px] ' + ui.muted + ' flex-1 truncate', title: d.name }, d.id),
                  React.createElement('input', { className: field + ' w-48', value: val, onChange: function (e) { setDlgExportFiles(Object.assign({}, dlgExportFiles, (function () { const o = {}; o[d.id] = e.target.value; return o; })())); }, placeholder: 'имя dialogs_*.xml' })
                );
              });
            })()
          )
        ),
        React.createElement('div', { className: 'p-5 border-t border-inherit flex items-center justify-between' },
          React.createElement('div', { className: 'flex flex-wrap gap-2' },
            React.createElement('button', { className: btn, onClick: exportProject }, 'Экспорт проекта'),
            React.createElement('button', { className: btn, onClick: exportXml }, 'XML редактора'),
            React.createElement('button', { className: btn, onClick: exportGameZip }, 'Игровой ZIP'),
            React.createElement('button', { className: btn, onClick: exportAllDialogues }, 'Все диалоги (+dialogs.ltx)'),
            React.createElement('button', { className: btn, onClick: exportFullPackage }, 'Полный пакет')
          ),
          React.createElement('button', { className: accentBtn, onClick: function () { setSettingsOpen(false); } }, 'Готово')
        )
      )
    ),

    programSettingsOpen && React.createElement('div', { className: 'fixed inset-0 z-50 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4' },
      React.createElement('div', { className: 'w-full max-w-md max-h-[90vh] flex flex-col rounded-2xl border overflow-hidden ' + ui.panel },
        React.createElement('div', { className: 'p-5 border-b border-inherit flex items-center justify-between' },
          React.createElement('div', {},
            React.createElement('h2', { className: 'font-bold text-lg' }, t('programSettingsTitle')),
            React.createElement('p', { className: 'text-xs ' + ui.muted }, t('programSettingsSub'))
          ),
          React.createElement('button', { className: btn + ' text-lg', onClick: function () { setProgramSettingsOpen(false); } }, '×')
        ),
        React.createElement('div', { className: 'p-5 grid gap-4 overflow-y-auto flex-1 min-h-0' },
          React.createElement('label', { className: 'grid gap-1 text-xs' }, t('theme'), React.createElement('select', { className: field, value: programSettings.theme, onChange: function (e) { setProgramSettings(Object.assign({}, programSettings, { theme: e.target.value })); } }, React.createElement('option', { value: 'dark' }, 'Тёмная'), React.createElement('option', { value: 'light' }, 'Светлая'), React.createElement('option', { value: 'auto' }, 'Авто (системная)'))),
          React.createElement('label', { className: 'grid gap-1 text-xs' }, t('language'), React.createElement('select', { className: field, value: programSettings.locale || 'ru', onChange: function (e) { setProgramSettings(Object.assign({}, programSettings, { locale: e.target.value })); } }, React.createElement('option', { value: 'ru' }, 'Русский'), React.createElement('option', { value: 'en' }, 'English'))),
          React.createElement('div', { className: 'grid gap-1 text-xs' },
            React.createElement('span', {}, 'Акцентный цвет'),
            React.createElement('div', { className: 'flex gap-2' },
              Object.keys(ACCENT_PRESETS).map(function (k) {
                return React.createElement('button', { key: k, type: 'button', title: k, onClick: function () { setProgramSettings(Object.assign({}, programSettings, { accent: k })); }, className: 'w-7 h-7 rounded-full border-2 ' + (programSettings.accent === k ? 'border-white ring-2 ring-white/40' : 'border-transparent'), style: { backgroundColor: ACCENT_PRESETS[k][0] } });
              })
            )
          ),
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Плотность интерфейса', React.createElement('select', { className: field, value: programSettings.density || 'normal', onChange: function (e) { setProgramSettings(Object.assign({}, programSettings, { density: e.target.value })); } }, React.createElement('option', { value: 'compact' }, 'Компактная'), React.createElement('option', { value: 'normal' }, 'Обычная'), React.createElement('option', { value: 'roomy' }, 'Просторная'))),
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Узлы диалогов', React.createElement('select', { className: field, value: programSettings.showNodeText === false ? 'id' : 'text', onChange: function (e) { setProgramSettings(Object.assign({}, programSettings, { showNodeText: e.target.value !== 'id' })); } }, React.createElement('option', { value: 'text' }, 'Показывать текст'), React.createElement('option', { value: 'id' }, 'Показывать id'))),
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Автосохранение (сек, 0 — выкл)', React.createElement('input', { type: 'number', min: 0, className: field, value: programSettings.autosaveInterval || 0, onChange: function (e) { setProgramSettings(Object.assign({}, programSettings, { autosaveInterval: Math.max(0, +e.target.value || 0) })); } })),
          React.createElement('div', { className: 'grid gap-1 text-xs' },
            React.createElement('span', {}, t('hotkeys')),
            React.createElement('div', { className: 'grid grid-cols-2 gap-2' },
              HOTKEY_ACTIONS.map(function (a) { return React.createElement('label', { key: a.id, className: 'grid gap-1 text-[11px]' }, a.label + ' (' + a.def + ')', React.createElement('input', { className: field, value: (programSettings.hotkeys || {})[a.id] || '', placeholder: a.def, onChange: function (e) { setProgramSettings(Object.assign({}, programSettings, { hotkeys: Object.assign({}, programSettings.hotkeys, (function () { const o = {}; o[a.id] = e.target.value; return o; })()) })); } })); })
            ),
            React.createElement('div', { className: 'text-[11px] ' + ui.muted }, 'Формат: mod+s, shift+z, F5, Delete. mod = Ctrl/Cmd. Пусто — значение по умолчанию.')
          ),
          React.createElement('div', { className: 'grid gap-1 text-xs' },
            React.createElement('span', { className: 'font-semibold' }, t('syncSection')),
            React.createElement('div', { className: 'flex gap-2' },
              React.createElement('input', { className: field + ' flex-1', value: programSettings.syncUrl || '', placeholder: 'https://…', onChange: function (e) { setProgramSettings(Object.assign({}, programSettings, { syncUrl: e.target.value })); } }),
              React.createElement('button', { className: btn, onClick: syncProject }, '↑ ' + t('syncNow')),
              React.createElement('button', { className: btn, onClick: loadFromSync }, '↓ ' + t('syncLoad'))
            )
          ),
          React.createElement('div', { className: 'grid gap-1 text-xs' },
            React.createElement('div', { className: 'flex items-center justify-between' },
              React.createElement('span', { className: 'font-semibold' }, t('pluginsSection') + ' (' + plugins.length + ')'),
              React.createElement('button', { className: btn, onClick: addPlugin }, '＋ ' + t('addPlugin'))
            ),
            plugins.map(function (p) {
              return React.createElement('div', { key: p.id, className: 'rounded border p-2 grid gap-1 ' + (dark ? 'border-zinc-800' : 'border-slate-200') },
                React.createElement('div', { className: 'flex gap-2' },
                  React.createElement('input', { className: field + ' flex-1', value: p.name, placeholder: t('pluginName'), onChange: function (e) { updatePlugin(p.id, 'name', e.target.value); } }),
                  React.createElement('button', { className: btn, title: t('runPlugins'), onClick: function () { runPlugin(p); } }, '▶'),
                  React.createElement('button', { className: 'text-red-400', onClick: function () { removePlugin(p.id); } }, '✕')
                ),
                React.createElement('textarea', { rows: 3, className: field + ' font-mono', value: p.code, placeholder: t('pluginCode'), onChange: function (e) { updatePlugin(p.id, 'code', e.target.value); } })
              );
            })
          ),
          React.createElement('div', { className: 'grid gap-1 text-xs' },
            React.createElement('div', { className: 'flex items-center justify-between' },
              React.createElement('span', {}, 'Журнал ошибок (' + appErrors.length + ')'),
              appErrors.length > 0 && React.createElement('button', { className: btn, onClick: function () { setAppErrors([]); } }, 'Очистить')
            ),
            appErrors.length > 0 && React.createElement('div', { className: 'max-h-28 overflow-auto rounded border p-2 grid gap-1 ' + (dark ? 'border-zinc-800' : 'border-slate-200') },
              appErrors.slice(-10).reverse().map(function (er, i) { return React.createElement('div', { key: i, className: 'font-mono text-[11px] text-red-400 truncate' }, new Date(er.t).toLocaleTimeString() + ' ' + er.msg); })
            )
          ),
          React.createElement('div', { className: 'grid gap-1 text-xs' },
            React.createElement('div', { className: 'flex items-center justify-between' },
              React.createElement('span', {}, 'Журнал операций (' + opLog.length + ')'),
              opLog.length > 0 && React.createElement('button', { className: btn, onClick: function () { setOpLog([]); } }, 'Очистить')
            ),
            opLog.length > 0 && React.createElement('div', { className: 'max-h-28 overflow-auto rounded border p-2 grid gap-1 ' + (dark ? 'border-zinc-800' : 'border-slate-200') },
              opLog.slice(-10).reverse().map(function (op, i) { return React.createElement('div', { key: i, className: 'font-mono text-[11px] ' + ui.muted + ' truncate' }, new Date(op.t).toLocaleTimeString() + ' ' + op.msg); })
            )
          ),
          React.createElement('div', { className: 'grid gap-1 text-xs' },
            React.createElement('span', {}, 'Резервные копии (' + backups.length + ')'),
            backups.length > 0 && React.createElement('div', { className: 'grid gap-1 max-h-28 overflow-auto' },
              backups.map(function (b, i) { return React.createElement('button', { key: b.t, className: btn + ' text-left text-[11px]', onClick: function () { restoreBackup(b); } }, new Date(b.t).toLocaleString()); })
            )
          )
        ),
        React.createElement('div', { className: 'p-5 border-t border-inherit flex items-center justify-between' },
          React.createElement('div', { className: 'flex gap-2' },
            React.createElement('button', { className: btn, onClick: exportSettings }, t('exportSettings')),
            React.createElement('button', { className: btn, onClick: function () { const inp = document.createElement('input'); inp.type = 'file'; inp.accept = '.json'; inp.onchange = function (ev) { importSettings(ev); }; inp.click(); } }, t('importSettings'))
          ),
          React.createElement('button', { className: 'text-red-600 ' + btn, onClick: resetStorage }, t('resetData')),
          React.createElement('button', { className: accentBtn, onClick: function () { setProgramSettingsOpen(false); } }, t('done'))
        )
      )
    ),

    helpOpen && React.createElement('div', { className: 'fixed inset-0 z-50 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4' },
      React.createElement('div', { className: 'w-full max-w-lg rounded-2xl border overflow-hidden ' + ui.panel },
        React.createElement('div', { className: 'p-5 border-b border-inherit flex items-center justify-between' },
          React.createElement('h2', { className: 'font-bold text-lg' }, t('helpTitle')),
          React.createElement('button', { className: btn + ' text-lg', onClick: function () { setHelpOpen(false); } }, '×')
        ),
        React.createElement('div', { className: 'p-5 grid gap-3 text-sm overflow-auto max-h-[70vh]' },
          React.createElement('div', { className: 'grid gap-2' },
            React.createElement('b', {}, 'Горячие клавиши'),
            React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Ctrl+S — сохранить · Ctrl+E — XML редактора · Delete — удалить ноду · 1/2/3/4 — добавить Старт/Реплику/Случайный/Конец · Esc — отмена связи'),
            React.createElement('b', {}, 'Диалоги'),
            React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Shift+перетаскивание — рамка выделения · перетаскивание по пустому холсту — панорама · Ctrl+колесо — масштаб'),
            React.createElement('b', {}, 'Иконки'),
            React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Ctrl+колесо — зум · средняя кнопка — панорама · Enter — закрепить · Ctrl+Z/Y — отмена/повтор'),
            React.createElement('b', {}, 'Форматы'),
            React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Поддерживаются game_dialogs (XML), character_desc (XML), game_tasks / task_manager (XML), items.ltx, system.ltx, string_table, .script/.lua. Экспорт в кодировке проекта (Windows-1251 или UTF-8).')
          )
        ),
        React.createElement('div', { className: 'p-5 border-t border-inherit flex justify-end' },
          React.createElement('button', { className: accentBtn, onClick: function () { setHelpOpen(false); } }, t('close'))
        )
      )
    ),

    globalSearchOpen && React.createElement('div', { className: 'fixed inset-0 z-[70] bg-black/50 backdrop-blur-sm flex items-start justify-center pt-24 p-4', onClick: function () { setGlobalSearchOpen(false); } },
      React.createElement('div', { className: 'w-full max-w-xl rounded-2xl border overflow-hidden ' + ui.panel, onClick: function (e) { e.stopPropagation(); } },
        React.createElement('div', { className: 'p-3 border-b border-inherit flex items-center gap-2' },
          React.createElement('input', { autoFocus: true, className: field + ' flex-1', value: globalSearchQuery, onChange: function (e) { setGlobalSearchQuery(e.target.value); }, placeholder: t('globalSearch') + ' (Ctrl+K)…' }),
          React.createElement('button', { className: btn, onClick: function () { setGlobalSearchOpen(false); } }, 'Esc')
        ),
        React.createElement('div', { className: 'max-h-[60vh] overflow-auto p-3 grid gap-1' },
          (function () {
            const q = globalSearchQuery.trim().toLowerCase();
            if (!q) return React.createElement('div', { className: 'text-xs ' + ui.muted + ' py-6 text-center' }, 'Начните вводить — поиск по диалогам, персонажам, предметам, скриптам, квестам и нодам.');
            const out = [];
            (project.dialogues || []).forEach(function (d) { if ((d.name + ' ' + d.id).toLowerCase().indexOf(q) !== -1) out.push({ type: 'Диалог', label: d.name || d.id, sub: d.id, action: function () { loadDialogueIntoGraph(d); setTab('dialogues'); setGlobalSearchOpen(false); } }); });
            (project.characters || []).forEach(function (c) { if ([c.name, c.id, c.community, c.start_dialog, c.actor_dialog].join(' ').toLowerCase().indexOf(q) !== -1) out.push({ type: 'Персонаж', label: c.name || c.id, sub: c.community, action: function () { setSelChar(c.id); setTab('characters'); setGlobalSearchOpen(false); } }); });
            parsedItems.sections.forEach(function (s) { if (s.name.toLowerCase().indexOf(q) !== -1) out.push({ type: 'Предмет', label: s.name, sub: classifyItem(s), action: function () { setSelItem(s.name); setTab('items'); setGlobalSearchOpen(false); } }); });
            (project.scripts || []).forEach(function (s, i) { if ((s.name + ' ' + s.text).toLowerCase().indexOf(q) !== -1) out.push({ type: 'Скрипт', label: s.name, sub: '', action: function () { setSelScript(i); setTab('scripts'); setGlobalSearchOpen(false); } }); });
            (project.quests || []).forEach(function (qq) { if ((qq.id + ' ' + qq.title).toLowerCase().indexOf(q) !== -1) out.push({ type: 'Квест', label: qq.title || qq.id, sub: qq.id, action: function () { setSelQuest(qq.id); setTab('quests'); setGlobalSearchOpen(false); } }); });
            nodes.forEach(function (n) { const lbl = nodeLabel(n, project.languages, true); if ((n.id + ' ' + lbl).toLowerCase().indexOf(q) !== -1) out.push({ type: 'Нода', label: lbl || n.id, sub: n.id, action: function () { setSelected(n.id); setTab('dialogues'); setGlobalSearchOpen(false); } }); });
            return out.length ? out.map(function (r, i) { return React.createElement('button', { key: i, className: 'text-left rounded-lg px-3 py-2 ' + ui.hover, onClick: r.action }, React.createElement('div', { className: 'flex items-center gap-2' }, React.createElement('span', { className: 'text-[10px] px-1.5 py-0.5 rounded ' + (dark ? 'bg-zinc-800' : 'bg-slate-200') }, r.type), React.createElement('b', { className: 'text-xs' }, r.label), r.sub && React.createElement('span', { className: 'text-[10px] ' + ui.muted }, r.sub))); }) : React.createElement('div', { className: 'text-xs ' + ui.muted + ' py-6 text-center' }, 'Ничего не найдено.');
          })()
        )
      )
    ),

    ctxMenu && React.createElement('div', { className: 'fixed inset-0 z-[80]', onClick: function () { setCtxMenu(null); } },
      React.createElement('div', { className: 'absolute rounded-lg border shadow-xl py-1 min-w-[160px] ' + ui.panel, style: { left: Math.min(ctxMenu.x, (typeof window !== 'undefined' ? window.innerWidth : 800) - 180), top: Math.min(ctxMenu.y, (typeof window !== 'undefined' ? window.innerHeight : 600) - 200) }, onClick: function (e) { e.stopPropagation(); } },
        ctxMenu.items.map(function (it, i) { return React.createElement('button', { key: i, className: 'block w-full text-left px-3 py-1.5 text-xs ' + ui.hover, onClick: function () { setCtxMenu(null); it.action(); } }, it.label); })
      )
    ),

    loadProgress && React.createElement('div', { className: 'fixed top-0 left-0 right-0 z-[60] pointer-events-none' },
      React.createElement('div', { className: 'h-1.5 bg-zinc-700/40' },
        React.createElement('div', { className: 'h-full accent transition-all duration-150', style: { width: (loadProgress.total ? Math.round(loadProgress.done / loadProgress.total * 100) : 0) + '%' } })
      ),
      React.createElement('div', { className: 'text-center text-[11px] bg-zinc-900/90 text-white py-1 px-3 shadow' }, 'Подгрузка gamedata: ' + loadProgress.done + ' / ' + loadProgress.total)
    ),
    toast && React.createElement('div', { className: 'fixed bottom-5 left-1/2 -translate-x-1/2 bg-zinc-900 text-white text-xs px-4 py-2 rounded-xl shadow-2xl z-50 flex items-center gap-3' }, React.createElement('span', {}, toast), toastAction && React.createElement('button', { className: 'font-bold underline', onClick: function () { setToast(''); setToastAction(null); toastAction.action(); } }, toastAction.label)),
    React.createElement('style', {}, '.accent{background-color:var(--accent)!important}.accent:hover{background-color:var(--accent-hover)!important}*{box-sizing:border-box}body{-webkit-font-smoothing:antialiased;-moz-osx-font-smoothing:grayscale}button{-webkit-tap-highlight-color:transparent}input:focus,select:focus,textarea:focus{border-color:var(--accent)}:focus-visible{outline:2px solid var(--accent);outline-offset:1px}button,input,select,textarea{transition:border-color .15s ease,background-color .15s ease,color .15s ease}::-webkit-scrollbar{width:8px;height:8px}::-webkit-scrollbar-thumb{background:#94a3b8;border-radius:99px}::-webkit-scrollbar-thumb:hover{background:#64748b}::-webkit-scrollbar-track{background:transparent}.line-clamp-3{display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden}')
  );
}