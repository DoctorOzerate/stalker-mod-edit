import React from 'react';

// Вкладка «Скрипты» — редактор .script/.lua с подсветкой Lua.
// Состав: список файлов, поиск/замена, режим просмотра со свёрткой блоков и
// подсветкой, сниппеты, список функций с переходом к определению, diff с оригиналом.
// Принимает единый контекст ctx, сформированный в App.js.
export function renderScriptsTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, updateScript, insertSnippet, insertSnippetText, insertLuaAtCursor, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, mutateItems, undoItems, redoItems, addItem, deleteItem, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, luaCheck, luaUndeclaredGlobals, LUA_SNIPPETS, LUA_API, luaCallGraph, luaUnused, luaCompletions, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml, luaDuplicateFunctions, openCtxMenu, listSort, setListSort } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 flex min-h-0' },
      React.createElement('aside', { className: 'w-72 border-r p-4 overflow-auto ' + ui.soft },
        React.createElement('button', { className: accentBtn + ' w-full', onClick: function () { scriptRef.current.click(); } }, 'Сканировать скрипты'),
        React.createElement('button', { className: btn + ' w-full mt-2', onClick: function () { patch({ scripts: (project.scripts || []).concat([{ name: 'new.script', path: 'new.script', text: '-- Lua script' + NL + 'function init()' + NL + 'end' }]) }); say('Файл создан'); } }, '＋ Создать файл'),
        React.createElement('input', { className: field + ' mt-2', value: scriptSearch, onChange: function (e) { setScriptSearch(e.target.value); }, placeholder: 'Поиск по скриптам...' }),
        React.createElement('select', { className: field + ' mt-2', value: listSort, onChange: function (e) { setListSort(e.target.value); }, title: 'Сортировка списка' },
          React.createElement('option', { value: 'name' }, 'Сортировка: по имени'),
          React.createElement('option', { value: 'default' }, 'Сортировка: как есть')),
        React.createElement('div', { className: 'flex gap-1 mt-2' },
          React.createElement('input', { className: field + ' flex-1', value: scriptReplace, onChange: function (e) { setScriptReplace(e.target.value); }, placeholder: 'Замена…' }),
          React.createElement('button', { className: btn + ' text-[11px]', onClick: replaceAllInScripts }, 'Заменить всё')
        ),
        React.createElement('div', { className: 'grid gap-1 mt-2' },
          React.createElement('input', { className: field, list: 'dl_lua_api', placeholder: 'API движка (вставить в курсор)…', onChange: function (e) { const v = e.target.value; if (LUA_API.indexOf(v) !== -1) { insertLuaAtCursor(selScript, v); e.target.value = ''; } } }),
          React.createElement('datalist', { id: 'dl_lua_api' }, LUA_API.map(function (f) { return React.createElement('option', { key: f, value: f }); }))
        ),
        React.createElement('div', { className: 'mt-4 grid gap-2' },
          (project.scripts || []).map(function (s, idx) { return { s: s, i: idx }; }).filter(function (o) { return !scriptSearch || (o.s.name || '').toLowerCase().indexOf(scriptSearch.toLowerCase()) !== -1 || (o.s.text || '').toLowerCase().indexOf(scriptSearch.toLowerCase()) !== -1; }).sort(function (a, b) { return listSort === 'name' ? String(a.s.name).localeCompare(String(b.s.name)) : 0; }).map(function (o) {
            const s = o.s, i = o.i;
            return React.createElement('div', { key: s.path, className: 'rounded-xl border p-3 ' + ui.card, onContextMenu: function (e) { openCtxMenu(e, [{ label: '✏ Переименовать', action: function () { renameScript(i); } }, { label: '✂ Удалить', action: function () { deleteScript(i); } }]); } },
              React.createElement('button', { className: 'w-full text-left', onClick: function () { setSelScript(i); } },
                React.createElement('b', { className: 'text-xs' }, s.name),
                React.createElement('div', { className: 'text-[11px] ' + ui.muted }, s.path + ' · ' + String(s.text || '').split('\n').length + ' стр')
              ),
              React.createElement('div', { className: 'flex gap-2 mt-1' },
                React.createElement('button', { className: 'text-[11px] ' + btn, onClick: function () { renameScript(i); } }, 'Переименовать'),
                React.createElement('button', { className: 'text-[11px] text-red-500', onClick: function () { deleteScript(i); } }, 'Удалить')
              )
            );
          })
        )
      ),
      React.createElement('main', { className: 'flex-1 p-6 overflow-auto' },
        (project.scripts || []).length === 0 ? React.createElement('p', { className: ui.muted }, 'Скриптов нет.')
          : (function () {
            const s = (project.scripts || [])[selScript] || (project.scripts || [])[0];
            const lines = String(s.text || '').split(/\n/);
            const lineTokens = tokenizeLuaLines(s.text);
            const brc = countLuaBrackets(s.text), open = brc.open, close = brc.close;
            return React.createElement('div', { className: 'grid gap-3 max-w-5xl' },
              React.createElement('div', { className: 'flex items-center justify-between' },
                React.createElement('b', {}, s.name),
                React.createElement('div', { className: 'flex items-center gap-2' },
                  React.createElement('button', { className: 'text-[11px] ' + btn, onClick: function () { setScriptViewMode(!scriptViewMode); } }, scriptViewMode ? '✎ Редактировать' : '👁 Просмотр'),
                  React.createElement('span', { className: 'text-[11px] ' + (open === close ? 'text-green-500' : 'text-red-500') }, 'Скобки: ' + (open === close ? '✓' : '✗ ' + open + '/' + close)),
                  React.createElement('button', { className: btn, onClick: formatCurrentScript }, 'Формат'),
                  s.original && s.original !== s.text && React.createElement('span', { className: 'text-[11px] text-amber-400' }, '● изменён'),
                  s.original && React.createElement('button', { className: btn, onClick: function () { setScriptDiff(!scriptDiff); } }, scriptDiff ? 'Скрыть diff' : 'Diff'),
                  React.createElement('button', { className: btn, onClick: function () { downloadBytes(textBytes(s.text, (project.settings && project.settings.exportEncoding) || 'windows-1251'), s.name, 'text/plain'); } }, 'Скачать')
                )
              ),
              (function () {
                const issues = luaCheck(s.text).concat(luaUndeclaredGlobals(s.text)).concat(luaDuplicateFunctions(s.text).map(function (d) { return { severity: 'warning', msg: 'Дубликат функции: ' + d.name + ' (' + d.count + ' раз)' }; }));
                if (!issues.length) return null;
                return React.createElement('div', { className: 'rounded-xl border p-2 grid gap-1 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Проверка Lua (' + issues.length + ')'),
                  issues.map(function (iss, i) { return React.createElement('div', { key: i, className: 'text-[11px] ' + (iss.severity === 'error' ? 'text-red-400' : 'text-amber-400') }, (iss.severity === 'error' ? '⛔ ' : '⚠ ') + (iss.line ? 'строка ' + iss.line + ': ' : '') + iss.msg); })
                );
              })(),
              (function () {
                const g = luaCallGraph(s.text);
                const unused = luaUnused(s.text);
                if (!g.functions.length && !unused.length) return null;
                return React.createElement('div', { className: 'rounded-xl border p-2 grid gap-1 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Граф вызовов · ' + g.functions.length + ' функций'),
                  g.edges.length > 0 && React.createElement('div', { className: 'flex flex-wrap gap-1' },
                    g.edges.map(function (e, i) { return React.createElement('span', { key: i, className: 'text-[11px] font-mono px-1.5 py-0.5 rounded bg-zinc-800 text-zinc-300' }, e.from + ' → ' + e.to); })
                  ),
                  unused.length > 0 && React.createElement('div', { className: 'text-[11px] text-amber-400' }, 'Не используется: ' + unused.join(', '))
                );
              })(),
              !scriptViewMode && React.createElement('div', { className: 'flex flex-wrap gap-1 items-center' },
                ['function', 'if/then', 'for', 'while', 'local'].map(function (sn) { return React.createElement('button', { key: sn, className: 'text-[11px] px-2 py-0.5 rounded ' + btn, onClick: function () { insertSnippet(selScript, sn); } }, sn); }),
                React.createElement('span', { className: 'text-[11px] ' + ui.muted }, '· STALKER:'),
                (LUA_SNIPPETS || []).map(function (sn) { return React.createElement('button', { key: sn.label, className: 'text-[11px] px-2 py-0.5 rounded ' + btn, title: sn.text, onClick: function () { insertSnippetText(selScript, sn.text); } }, sn.label); })
              ),
              scriptViewMode
                ? React.createElement('div', { className: 'rounded-xl border p-3 font-mono text-xs ' + ui.card },
                    (function () {
                      const blocks = luaFunctionBlocks(lines);
                      const out = [];
                      for (let i = 0; i < lines.length; i++) {
                        const blk = blocks.find(function (b) { return b.start === i; });
                        if (blk) {
                          const collapsed = collapsedBlocks[blk.start];
                          out.push(React.createElement('div', { key: i, className: 'flex items-center gap-2 cursor-pointer text-blue-400', onClick: function () { toggleBlock(blk.start); } },
                            React.createElement('span', {}, collapsed ? '▸' : '▾'), React.createElement('span', { className: 'text-slate-500' }, i + 1), React.createElement('span', {}, lines[i])
                          ));
                          if (collapsed) { i = blk.end; out.push(React.createElement('div', { key: 'x' + i, className: 'text-slate-500 pl-8' }, '… свернуто ' + (blk.end - blk.start) + ' строк')); }
                          continue;
                        }
                        out.push(React.createElement('div', { key: i, className: 'flex gap-2' }, React.createElement('span', { className: 'text-slate-500 w-6 text-right' }, i + 1), (lineTokens[i] || []).map(function (tk, j) { return React.createElement('span', { key: j, style: { color: luaColor[tk.cls] } }, tk.text); })));
                      }
                      return out;
                    })()
                  )
                : React.createElement('div', { className: 'flex rounded-xl border overflow-hidden ' + ui.card },
                    React.createElement('div', { className: 'text-right pr-2 py-3 font-mono text-xs text-slate-500 select-none bg-black/5', style: { lineHeight: '1.5' } },
                      lines.map(function (_, i) { return React.createElement('div', { key: i }, i + 1); })
                    ),
                    React.createElement('textarea', { ref: scriptTextRef, className: 'flex-1 p-3 font-mono text-xs outline-none ' + ui.field, rows: Math.max(12, lines.length), value: s.text, onChange: function (e) { updateScript(selScript, e.target.value); }, style: { lineHeight: '1.5' } })
                  ),
              scriptDiff && s.original && React.createElement('div', { className: 'grid md:grid-cols-2 gap-2' },
                React.createElement('div', { className: 'rounded-xl border p-2 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase font-bold ' + ui.muted }, 'Оригинал'),
                  React.createElement('pre', { className: 'mt-1 text-[11px] font-mono whitespace-pre-wrap overflow-auto max-h-64 ' + ui.muted }, s.original)
                ),
                React.createElement('div', { className: 'rounded-xl border p-2 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase font-bold ' + ui.muted }, 'Текущая'),
                  React.createElement('pre', { className: 'mt-1 text-[11px] font-mono whitespace-pre-wrap overflow-auto max-h-64 ' + ui.muted }, s.text)
                )
              ),
              React.createElement('div', { className: 'flex flex-wrap gap-1 items-center' },
                React.createElement('span', { className: 'text-[11px] ' + ui.muted }, 'Функции:'),
                (function () {
                  const defs = (String(s.text || '').match(/function\s+([A-Za-z_][\w.:]*)/g) || []).map(function (fn) { return fn.replace(/^function\s+/, ''); });
                  return defs.map(function (name, i) {
                    const calls = Math.max(0, (String(s.text || '').split(name).length - 1) - 1);
                    return React.createElement('button', { key: i, className: 'text-[11px] px-2 py-0.5 rounded bg-zinc-800 text-zinc-300 font-mono hover:bg-zinc-700', title: 'Перейти к определению · вызовов: ' + calls, onClick: function () { jumpToDefinition(name); } }, name + ' (' + calls + ')');
                  });
                })()
              )
            );
          })()
      )
    )
  );
}
