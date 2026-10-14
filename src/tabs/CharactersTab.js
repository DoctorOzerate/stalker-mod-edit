import React from 'react';

// Вкладка «Персонажи» — редактор specific_character (character_desc_*.xml).
// Состав: список с поиском и группировкой по community, редактор полей,
// связь с диалогами, предпросмотр иконки/визуала, diff с оригиналом, исходный XML.
// Принимает единый контекст ctx, сформированный в App.js.
export function renderCharactersTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, updateScript, insertSnippet, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, mutateItems, undoItems, redoItems, addItem, deleteItem, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml, characterDescGeneralToXml, listSort, setListSort } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 flex min-h-0' },
      React.createElement('aside', { className: 'w-72 border-r p-4 overflow-auto ' + ui.soft },
        React.createElement('button', { className: accentBtn + ' w-full', onClick: function () { const c = { id: 'char_' + Date.now().toString(36), name: 'Новый персонаж', community: 'stalker', rank: '0', bio: '', icon: '', visual: '', start_dialog: '' }; patch({ characters: project.characters.concat([c]) }); setSelChar(c.id); } }, '＋ Создать персонажа'),
        React.createElement('button', { className: btn + ' w-full mt-2', onClick: autofillCharDialogs }, 'Автосвязь с диалогами'),
        React.createElement('div', { className: 'mt-4 grid gap-2' },
          React.createElement('input', { value: charSearch, onChange: function (e) { setCharSearch(e.target.value); }, placeholder: 'Поиск персонажа...', className: field }),
          React.createElement('select', { className: field + ' mt-2', value: listSort, onChange: function (e) { setListSort(e.target.value); }, title: 'Сортировка списка' },
            React.createElement('option', { value: 'name' }, 'Сортировка: по имени'),
            React.createElement('option', { value: 'default' }, 'Сортировка: по умолчанию')),
          (function () {
            const dup = {};
            project.characters.forEach(function (c) { dup[c.id] = (dup[c.id] || 0) + 1; });
            const q = charSearch.toLowerCase();
            const filtered = project.characters.filter(function (c) {
              if (!q) return true;
              const hay = [c.name, c.id, c.bio, c.visual, c.community, c.start_dialog, c.actor_dialog].join(' ').toLowerCase();
              return hay.indexOf(q) !== -1;
            });
            const groups = {};
            filtered.forEach(function (c) { const g = c.community || 'Без группы'; (groups[g] = groups[g] || []).push(c); });
            if (listSort === 'name') Object.keys(groups).forEach(function (g) { groups[g].sort(function (a, b) { return String(a.name || a.id).localeCompare(String(b.name || b.id)); }); });
            return Object.keys(groups).sort().map(function (g) {
              return React.createElement('div', { key: g },
                React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted + ' mt-2 mb-1' }, (COMMUNITY_ICONS[g.toLowerCase()] || '👤') + ' ' + g + ' · ' + groups[g].length),
                groups[g].map(function (c) {
                  const isDup = dup[c.id] > 1;
                  return React.createElement('button', { key: c.id, onClick: function () { setSelChar(c.id); }, className: 'text-left rounded-xl border p-3 w-full ' + ui.card + ' ' + ui.hover + (isDup ? ' border-red-400' : '') },
                    React.createElement('b', { className: 'text-xs' }, c.name || c.id),
                    React.createElement('div', { className: 'text-[11px] ' + ui.muted + (isDup ? ' text-red-400' : '') }, c.id + (isDup ? ' · дубликат' : ''))
                  );
                })
              );
            });
          })()
        )
      ),
      React.createElement('main', { className: 'flex-1 p-6 overflow-auto' },
        React.createElement('div', { className: 'grid gap-6 max-w-3xl' },
          (function () {
            const gr = project.gameRelations;
            if (!gr || !(gr.communities || []).length) return null;
            return React.createElement('div', { className: 'rounded-xl border p-4 grid gap-3 ' + ui.card },
              React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Группировки · game_relations (' + gr.communities.length + ')'),
              React.createElement('div', { className: 'flex flex-wrap gap-1' },
                gr.communities.map(function (c) { return React.createElement('span', { key: c, className: 'text-[11px] px-2 py-0.5 rounded ' + (dark ? 'bg-zinc-800' : 'bg-slate-200'), title: c }, (COMMUNITY_ICONS[c] || '•') + ' ' + c); })
              ),
              gr.monsterCommunities && gr.monsterCommunities.length > 0 && React.createElement(React.Fragment, null,
                React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Монстры (' + gr.monsterCommunities.length + ')'),
                React.createElement('div', { className: 'flex flex-wrap gap-1' },
                  gr.monsterCommunities.map(function (c) { return React.createElement('span', { key: c, className: 'text-[11px] px-2 py-0.5 rounded ' + (dark ? 'bg-zinc-800' : 'bg-slate-200'), title: c }, (COMMUNITY_ICONS[c] || '•') + ' ' + c); })
                )
              )
            );
          })(),
          project.characters.length === 0 ? React.createElement('p', { className: ui.muted }, 'Персонажей нет. Создайте нового или просканируйте gamedata.')
          : (function () {
            const c = project.characters.find(function (x) { return x.id === selChar; }) || project.characters[0];
            const setC = function (k, v) { patch({ characters: project.characters.map(function (x) { return x.id === c.id ? Object.assign({}, x, (function () { const o = {}; o[k] = v; return o; })()) : x; }) }); };
            return React.createElement('div', { className: 'max-w-2xl grid gap-3' },
              React.createElement('h2', { className: 'font-bold' }, 'Редактор персонажа'),
              (function () {
                const warns = [];
                if (!String(c.visual || '').trim()) warns.push('не указан visual (модель)');
                if (!String(c.icon || '').trim()) warns.push('не указана icon (портрет)');
                if (!warns.length) return null;
                return React.createElement('div', { className: 'rounded-xl border p-2 grid gap-1 ' + (dark ? 'border-amber-700 bg-amber-900/10' : 'border-amber-300 bg-amber-50') },
                  warns.map(function (w, i) { return React.createElement('div', { key: i, className: 'text-[11px] text-amber-600' }, '⚠ ' + w); })
                );
              })(),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'ID', React.createElement('input', { className: field, value: c.id, onChange: function (e) { setC('id', e.target.value); } })),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Имя (name)', React.createElement('input', { className: field, value: c.name || '', onChange: function (e) { setC('name', e.target.value); } })),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Группировка (community)', React.createElement('input', { className: field, value: c.community || '', onChange: function (e) { setC('community', e.target.value); }, placeholder: 'stalker', list: 'dl_communities' }), (project.generalCommunities || []).length > 0 && React.createElement('datalist', { id: 'dl_communities' }, project.generalCommunities.map(function (g) { return React.createElement('option', { key: g, value: g }); }))),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Ранг (rank)', React.createElement('input', { className: field, value: c.rank || '', onChange: function (e) { setC('rank', e.target.value); }, list: 'dl_ranks' }), (function () { const sc = project.gameRelations && project.gameRelations.scales && project.gameRelations.scales.rating; if (!sc || !sc.length) return null; return React.createElement('div', { className: 'flex flex-wrap gap-1' }, React.createElement('datalist', { id: 'dl_ranks' }, sc.map(function (s) { return React.createElement('option', { key: s.name, value: s.value, label: s.name }); })), sc.map(function (s) { return React.createElement('span', { key: s.name, className: 'text-[10px] font-mono px-1 rounded ' + (dark ? 'bg-zinc-800' : 'bg-slate-100'), title: 'Ранг ' + s.name }, s.value + ' ' + s.name); })); })()),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Репутация (reputation)', React.createElement('input', { className: field, value: c.reputation || '', onChange: function (e) { setC('reputation', e.target.value); }, list: 'dl_reps' }), (function () { const sc = project.gameRelations && project.gameRelations.scales && project.gameRelations.scales.reputation; if (!sc || !sc.length) return null; return React.createElement('div', { className: 'flex flex-wrap gap-1' }, React.createElement('datalist', { id: 'dl_reps' }, sc.map(function (s) { return React.createElement('option', { key: s.name, value: s.value, label: s.name }); })), sc.map(function (s) { return React.createElement('span', { key: s.name, className: 'text-[10px] font-mono px-1 rounded ' + (dark ? 'bg-zinc-800' : 'bg-slate-100'), title: 'Репутация ' + s.name }, s.value + ' ' + s.name); })); })()),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Ранг NPC (npc_rank)', React.createElement('input', { className: field, value: c.npc_rank || '', onChange: function (e) { setC('npc_rank', e.target.value); } })),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Био', React.createElement('textarea', { rows: 3, className: field, value: c.bio || '', onChange: function (e) { setC('bio', e.target.value); } })),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Иконка (icon)', React.createElement('input', { className: field, value: c.icon || '', onChange: function (e) { setC('icon', e.target.value); } })),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Визуал (visual)', React.createElement('input', { className: field, value: c.visual || '', onChange: function (e) { setC('visual', e.target.value); } })),
              React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
                React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Иконка и визуал'),
                React.createElement('div', { className: 'flex items-center gap-2' },
                  React.createElement('span', { className: 'w-10 h-10 grid place-items-center rounded border text-xl ' + (dark ? 'border-zinc-700 bg-zinc-800' : 'border-slate-300 bg-slate-100') }, '👤'),
                  React.createElement('div', { className: 'grid gap-0.5 text-xs font-mono ' + ui.muted },
                    React.createElement('div', {}, 'icon: ' + (c.icon || '—')),
                    React.createElement('div', {}, 'visual: ' + (c.visual || '—'))
                  )
                )
              ),
              React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Стартовый диалог (start_dialog)', React.createElement('input', { className: field, value: c.start_dialog || '', onChange: function (e) { setC('start_dialog', e.target.value); } })),
              React.createElement('div', { className: 'rounded-xl border p-3 grid gap-2 ' + ui.card },
                React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Диалоги персонажа'),
                (function () {
                  const refs = [].concat(c.start_dialog || [], c.actor_dialog || []);
                  const related = (project.dialogues || []).filter(function (d) { return refs.indexOf(d.id) !== -1 || refs.indexOf(d.name) !== -1; });
                  return related.length
                    ? related.map(function (d) { return React.createElement('button', { key: d.id, className: btn + ' text-left', onClick: function () { loadDialogueIntoGraph(d); } }, d.name || d.id); })
                    : React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Нет связанных диалогов (start_dialog/actor_dialog).');
                })()
              ),
              c.original && (function () {
                const fields = [['id', 'ID'], ['name', 'Имя'], ['community', 'Группа'], ['rank', 'Ранг'], ['bio', 'Био'], ['icon', 'Иконка'], ['visual', 'Визуал'], ['start_dialog', 'Старт. диалог'], ['actor_dialog', 'Диалог актёра']];
                const rows = fields.map(function (f) { return { label: f[1], a: String(c.original[f[0]] || ''), b: String(c[f[0]] || '') }; }).filter(function (r) { return r.a !== r.b; });
                if (!rows.length) return null;
                return React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Изменения от оригинала'),
                  rows.map(function (r, i) { return React.createElement('div', { key: i, className: 'text-xs grid grid-cols-[90px_1fr_1fr] gap-2' },
                    React.createElement('span', { className: ui.muted }, r.label),
                    React.createElement('span', { className: 'text-red-400 line-through truncate' }, r.a || '—'),
                    React.createElement('span', { className: 'text-green-400 truncate' }, r.b || '—')
                  ); })
                );
              })(),
              React.createElement('button', { className: btn, onClick: function () { setCharXml(extractCharacterXml(c.id, project.ltxFiles) || 'Исходный XML не найден в загруженных файлах.'); } }, 'Исходный XML'),
              charXml && React.createElement('div', { className: 'rounded-xl border p-3 grid gap-2 ' + ui.card },
                React.createElement('div', { className: 'flex justify-between text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, React.createElement('span', {}, 'Исходный XML'), React.createElement('button', { className: 'text-red-400', onClick: function () { setCharXml(null); } }, '×')),
                React.createElement('textarea', { readOnly: true, rows: 10, className: field + ' font-mono text-xs', value: charXml })
              ),
              React.createElement('button', { className: btn, onClick: function () { patch({ characters: project.characters.filter(function (x) { return x.id !== c.id; }) }); setSelChar(null); say('Персонаж удалён'); } }, 'Удалить персонажа')
            );
          })()
        )
      )
    )
  );
}
