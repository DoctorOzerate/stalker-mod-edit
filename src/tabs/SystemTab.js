import React from 'react';

// Вкладка «system.ltx» — редактирование и проверка инклюдов.
// Состав: текстовый редактор, список #include (подсветка битых + автоисправление),
// свёртка секций с редактированием, дерево/граф инклюдов, список секций.
// Принимает единый контекст ctx, сформированный в App.js.
export function renderSystemTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, selLtxPath, setSelLtxPath, spawnData, setSpawnData, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, spawnRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ltxSectionsMap, ltxSectionNames, includeRows, includeSvg, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, updateScript, insertSnippet, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadSpawn, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, updateLtxFile, deleteLtxFile, mutateItems, undoItems, redoItems, addItem, deleteItem, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, validateLtxRefs, findDuplicateSections, findInheritanceCycles, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 p-6 overflow-auto' },
      React.createElement('div', { className: 'max-w-5xl mx-auto grid gap-4' },
        React.createElement('div', { className: 'flex items-center justify-between' },
          React.createElement('div', {},
            React.createElement('h2', { className: 'font-bold' }, 'system.ltx'),
            React.createElement('p', { className: 'text-xs ' + ui.muted }, 'Редактирование и проверка подключённых файлов.')
          ),
          React.createElement('div', { className: 'flex gap-2' },
            React.createElement('select', { className: field + ' w-44', value: '', onChange: function (e) { if (e.target.value) { patch({ systemLtx: (project.systemLtx ? project.systemLtx.replace(/\s+$/, '') + NL : '') + '#include ' + Q + e.target.value + Q + NL }); } } },
              React.createElement('option', { value: '' }, 'Добавить include…'),
              Object.keys(project.ltxFiles || {}).sort().map(function (p) { return React.createElement('option', { key: p, value: p }, p); })),
            React.createElement('button', { className: btn, onClick: function () { patch({ systemLtx: (project.systemLtx ? project.systemLtx.replace(new RegExp(BS + 's+$'), '') + NL + NL : '') + '#include ' + Q + 'your_file.ltx' + Q + NL }); say('Include добавлен'); } }, '＋ Include'),
            React.createElement('button', { className: btn, onClick: function () { downloadBytes(textBytes(project.systemLtx || '', (project.settings && project.settings.exportEncoding) || 'windows-1251'), 'system.ltx', 'text/plain'); } }, 'Скачать')
          )
        ),
        React.createElement('textarea', { rows: 14, className: field + ' font-mono text-xs', value: project.systemLtx || '', onChange: function (e) { patch({ systemLtx: e.target.value }); }, placeholder: '#include ' + Q + '...' + Q }),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold flex items-center justify-between' }, React.createElement('span', {}, 'Секции system.ltx (свёртка)'), React.createElement('button', { className: btn + ' text-[11px]', onClick: addSysSection }, '＋ Секция')),
          React.createElement('div', { className: 'p-2 grid gap-1 max-h-80 overflow-auto' },
            (function () {
              const secs = parseLtx(project.systemLtx || '').sections;
              if (!secs.length) return React.createElement('div', { className: 'text-xs ' + ui.muted + ' p-1' }, 'Секций нет.');
              return secs.map(function (sec) {
                const open = !!expandedSys[sec.name];
                return React.createElement('div', { key: sec.name + '_' + sec.startLine, className: 'rounded border ' + (dark ? 'border-zinc-800' : 'border-slate-200') },
                  React.createElement('div', { className: 'flex items-center' },
                    React.createElement('button', { className: 'flex-1 text-left px-2 py-1.5 text-xs font-mono flex items-center gap-2 ' + ui.hover, onClick: function () { setExpandedSys(function (prev) { const n = Object.assign({}, prev); n[sec.name] = !n[sec.name]; return n; }); } },
                      React.createElement('span', {}, open ? '▾' : '▸'), React.createElement('span', {}, '[' + sec.name + ']'), (sec.parents && sec.parents.length ? React.createElement('span', { className: ui.muted }, ':' + sec.parents.join(',')) : null)
                    ),
                    React.createElement('button', { className: 'text-[11px] ' + btn + ' mr-1', onClick: function () { addSysEntry(sec.name); } }, '＋ запись')
                  ),
                  open && React.createElement('div', { className: 'px-3 pb-2 grid gap-0.5' }, sec.entries.map(function (e) {
                    return React.createElement('div', { key: e.line, className: 'grid grid-cols-[1fr_1fr_auto] gap-1 items-center' },
                      React.createElement('input', { className: field + ' font-mono text-[11px]', value: e.key, onChange: function (ev) { setSysEntry(e.line, ev.target.value, e.value); } }),
                      React.createElement('input', { className: field + ' font-mono text-[11px]', value: e.value, onChange: function (ev) { setSysEntry(e.line, e.key, ev.target.value); } }),
                      React.createElement('button', { className: 'text-red-400 text-xs', onClick: function () { removeSysEntry(e.line); } }, '×')
                    );
                  }))
                );
              });
            })()
          )
        ),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold' }, 'Подключения: ' + includes.length + ' · битых: ' + includes.filter(function (x) { return !isFileLoaded(x.path, project.ltxFiles); }).length),
          includes.map(function (x) {
            const found = isFileLoaded(x.path, project.ltxFiles);
            return React.createElement('div', { key: x.line, className: 'px-3 py-2 border-b border-inherit text-xs font-mono flex items-center gap-2 ' + (found ? '' : 'bg-red-500/10') },
              React.createElement('span', { className: ui.muted }, x.line + ':'),
              React.createElement('span', { className: 'flex-1 cursor-pointer hover:underline', onClick: function () { const k = Object.keys(project.ltxFiles || {}).find(function (p) { return p.toLowerCase() === x.path.toLowerCase() || p.toLowerCase().endsWith('/' + x.path.toLowerCase()); }); if (k != null) setIncludePreview({ path: x.path, text: project.ltxFiles[k] }); } }, x.raw),
              React.createElement('span', { className: found ? 'text-green-500' : 'text-red-500' }, found ? '✓' : '✗'),
              !found && (function () { const sugg = findIncludeSuggestion(x.path, project.ltxFiles); if (!sugg) return null; return React.createElement('button', { className: 'text-[11px] ' + btn, title: 'Заменить на ' + sugg, onClick: function () { patch({ systemLtx: updateLtxLine(project.systemLtx, x.line, '#include ' + Q + sugg + Q) }); say('Include исправлен: ' + sugg); } }, '🔧'); })(),
              React.createElement('button', { className: 'text-[11px] ' + btn, onClick: function () { const lines = splitLines(project.systemLtx); const orig = (lines[x.line - 1] || '').trim(); const nl = orig.charAt(0) === ';' ? orig.replace(/^;\s*/, '') : ';' + orig; patch({ systemLtx: updateLtxLine(project.systemLtx, x.line, nl) }); } }, 'комм.')
            );
          })
        ),
        includePreview && React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold flex justify-between' }, React.createElement('span', { className: 'font-mono' }, includePreview.path), React.createElement('button', { className: 'text-red-400', onClick: function () { setIncludePreview(null); } }, '×')),
          React.createElement('textarea', { readOnly: true, rows: 12, className: field + ' font-mono text-xs', value: includePreview.text })
        ),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold flex items-center justify-between' }, React.createElement('span', {}, 'Секции в подключённых файлах'), React.createElement('input', { value: sectionSearch, onChange: function (e) { setSectionSearch(e.target.value); }, placeholder: 'Поиск секции…', className: field + ' w-40', list: 'dl_ltx_sections' }), React.createElement('datalist', { id: 'dl_ltx_sections' }, Object.keys(ltxSectionsMap).sort().map(function (n) { return React.createElement('option', { key: n, value: n }); }))),
          React.createElement('div', { className: 'p-3 grid gap-1 max-h-48 overflow-auto' },
            (function () {
              const arr = Object.keys(ltxSectionsMap).sort().filter(function (n) { return !sectionSearch || n.toLowerCase().indexOf(sectionSearch.toLowerCase()) !== -1; });
              return arr.length ? arr.map(function (n) { const dup = ltxSectionsMap[n].length > 1; return React.createElement('div', { key: n, className: 'text-xs font-mono ' + (dup ? 'text-amber-400' : ui.muted), title: dup ? 'Файлы: ' + ltxSectionsMap[n].join(', ') : ltxSectionsMap[n][0] }, (dup ? '⚠ ' : '') + n + ' · ' + ltxSectionsMap[n].length); }) : React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Нет загруженных секций.');
            })()
          )
        ),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold' }, 'Проверка ссылок и дубликаты'),
          React.createElement('div', { className: 'p-3 grid gap-2' },
            (function () {
              const dups = findDuplicateSections(ltxSectionsMap);
              const refs = validateLtxRefs((project.itemsLtx || '') + NL + (project.systemLtx || ''), ltxSectionNames);
              const cycles = findInheritanceCycles((project.itemsLtx || '') + NL + (project.systemLtx || ''));
              return React.createElement(React.Fragment, null,
                React.createElement('div', { className: 'text-[11px] ' + ui.muted }, 'Дубликаты секций: ' + dups.length + ' · битые ссылки: ' + refs.length + ' · циклы наследования: ' + cycles.length),
                dups.length > 0 && React.createElement('div', { className: 'grid gap-1 max-h-32 overflow-auto' },
                  dups.slice(0, 50).map(function (d, i) { return React.createElement('div', { key: i, className: 'text-[11px] font-mono text-amber-400', title: d.files.join(', ') }, '⚠ ' + d.name + ' (' + d.files.length + ')'); })
                ),
                cycles.length > 0 && React.createElement('div', { className: 'grid gap-1 max-h-28 overflow-auto' },
                  cycles.map(function (c, i) { return React.createElement('div', { key: i, className: 'text-[11px] font-mono text-red-400' }, '↻ ' + c.msg); })
                ),
                refs.length > 0 && React.createElement('div', { className: 'grid gap-1 max-h-40 overflow-auto' },
                  refs.slice(0, 100).map(function (r, i) { return React.createElement('div', { key: i, className: 'text-[11px] font-mono ' + (r.severity === 'error' ? 'text-red-400' : 'text-amber-400') }, r.msg); })
                )
              );
            })()
          )
        ),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold' }, 'Дерево инклюдов'),
          React.createElement('div', { className: 'p-3 grid gap-3' },
            React.createElement('div', { className: 'grid gap-1 max-h-40 overflow-auto' },
              (function () {
                return includeRows.length ? includeRows.map(function (r, i) { return React.createElement('div', { key: i, className: 'text-xs font-mono ' + (r.cyclic ? 'text-red-500' : r.found ? ui.muted : 'text-red-400'), style: { paddingLeft: r.depth * 14 } }, (r.cyclic ? '↻ ' : '') + r.path + (r.found ? '' : ' ✗')); }) : React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Нет инклюдов.');
              })()
            ),
            (function () { return includeSvg ? React.createElement('div', { className: 'overflow-auto max-h-72' }, React.createElement('div', { dangerouslySetInnerHTML: { __html: includeSvg } })) : null; })()
          )
        ),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold' }, 'Файлы LTX (gamedata)'),
          React.createElement('div', { className: 'p-3 grid gap-2' },
            React.createElement('div', { className: 'flex flex-wrap gap-1' },
              Object.keys(project.ltxFiles || {}).sort().map(function (p) {
                return React.createElement('button', { key: p, className: 'text-[11px] font-mono px-2 py-1 rounded ' + (selLtxPath === p ? 'accent text-white' : btn), title: p, onClick: function () { setSelLtxPath(p); } }, p.split('/').pop());
              })
            ),
            selLtxPath && (project.ltxFiles || {})[selLtxPath] != null && React.createElement(React.Fragment, null,
              React.createElement('div', { className: 'flex items-center justify-between' },
                React.createElement('span', { className: 'font-mono text-[11px] ' + ui.muted }, selLtxPath),
                React.createElement('div', { className: 'flex gap-2' },
                  React.createElement('button', { className: btn, onClick: function () { download((project.ltxFiles || {})[selLtxPath] || '', selLtxPath.split('/').pop() || 'file.ltx', 'text/plain', false); } }, 'Скачать'),
                  React.createElement('button', { className: 'text-red-400', onClick: function () { deleteLtxFile(selLtxPath); } }, '✕')
                )
              ),
              React.createElement('textarea', { rows: 16, className: field + ' font-mono text-xs', value: (project.ltxFiles || {})[selLtxPath] || '', onChange: function (e) { updateLtxFile(selLtxPath, e.target.value); } })
            )
          )
        ),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold flex items-center justify-between' },
            React.createElement('span', {}, 'Спавн (all.spawn)'),
            React.createElement('button', { className: btn, onClick: function () { spawnRef.current.click(); } }, 'Загрузить .spawn')
          ),
          spawnData && React.createElement('div', { className: 'p-3 grid gap-2' },
            React.createElement('div', { className: 'text-[11px] ' + ui.muted }, spawnData.name + ' · ' + Math.round(spawnData.size / 1024) + ' КБ · версия ' + spawnData.header.version + ' · объектов ' + spawnData.header.spawnCount + ' · уровней ' + spawnData.header.levelCount + ' · строк ' + spawnData.stringCount),
            React.createElement('div', { className: 'grid gap-1 max-h-48 overflow-auto' },
              (spawnData.sections || []).slice(0, 500).map(function (s, i) { return React.createElement('div', { key: i, className: 'text-[11px] font-mono ' + ui.muted }, s); })
            )
          )
        ),
        React.createElement('div', { className: 'rounded-xl border ' + ui.card },
          React.createElement('div', { className: 'p-3 border-b border-inherit text-xs font-bold' }, 'Ресурсы (squad_descr · info_portions · encyclopedia · secrets)'),
          React.createElement('div', { className: 'p-3 grid gap-3' },
            (project.squads && project.squads.length > 0) ? React.createElement('div', { className: 'grid gap-1' },
              React.createElement('div', { className: 'text-[10px] uppercase font-bold ' + ui.muted }, 'Отряды · squad_descr (' + project.squads.length + ')'),
              React.createElement('div', { className: 'grid gap-0.5 max-h-40 overflow-auto' },
                project.squads.map(function (s) { return React.createElement('div', { key: s.id, className: 'text-[11px] font-mono ' + ui.muted, title: 'npc: ' + (s.npc || '') + ' · target_smart: ' + (s.target_smart || '') }, s.faction + ' · ' + s.id + ' · npc: ' + (s.npc ? s.npc.split(',').length : 0)); })
              )
            ) : null,
            (project.infoPortions && project.infoPortions.length > 0) ? React.createElement('div', { className: 'grid gap-1' },
              React.createElement('div', { className: 'text-[10px] uppercase font-bold ' + ui.muted }, 'Info-порции (' + project.infoPortions.length + ')'),
              React.createElement('div', { className: 'grid gap-0.5 max-h-40 overflow-auto' },
                project.infoPortions.map(function (ip) { return React.createElement('div', { key: ip.id, className: 'text-[11px] font-mono ' + ui.muted, title: 'tasks: ' + ip.tasks.join(', ') + ' · dialogs: ' + ip.dialogs.join(', ') + ' · disables: ' + ip.disables.join(', ') }, ip.id + (ip.tasks.length ? ' · задач: ' + ip.tasks.length : '') + (ip.dialogs.length ? ' · диалогов: ' + ip.dialogs.length : '') + (ip.disables.length ? ' · disables: ' + ip.disables.length : '')); })
              )
            ) : null,
            (project.encyclopedia && project.encyclopedia.length > 0) ? React.createElement('div', { className: 'grid gap-1' },
              React.createElement('div', { className: 'text-[10px] uppercase font-bold ' + ui.muted }, 'Энциклопедия (' + project.encyclopedia.length + ')'),
              React.createElement('div', { className: 'grid gap-0.5 max-h-40 overflow-auto' },
                project.encyclopedia.map(function (a) { return React.createElement('div', { key: a.id, className: 'text-[11px] font-mono ' + ui.muted, title: 'ltx: ' + a.ltx + ' · text: ' + a.text }, (a.group ? a.group + ' / ' : '') + a.id + (a.name ? ' (' + a.name + ')' : '')); })
              )
            ) : null,
            (project.secrets && project.secrets.treasures && project.secrets.treasures.length > 0) ? React.createElement('div', { className: 'grid gap-1' },
              React.createElement('div', { className: 'text-[10px] uppercase font-bold ' + ui.muted }, 'Тайники · secrets (' + project.secrets.treasures.length + ')' + (project.secrets.lists && project.secrets.lists.length ? ' · списки: ' + project.secrets.lists.join(', ') : '')),
              React.createElement('div', { className: 'grid gap-0.5 max-h-40 overflow-auto' },
                project.secrets.treasures.map(function (t) { return React.createElement('div', { key: t.id, className: 'text-[11px] font-mono ' + ui.muted, title: t.items.map(function (i) { return i.item + '=' + i.value; }).join(', ') }, t.id + ' · предметов: ' + t.items.length); })
              )
            ) : null,
            (!project.squads || !project.squads.length) && (!project.infoPortions || !project.infoPortions.length) && (!project.encyclopedia || !project.encyclopedia.length) && (!project.secrets || !project.secrets.treasures || !project.secrets.treasures.length) ? React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Нет просканированных ресурсов этого типа. Просканируйте gamedata.') : null
          )
        )
      )
    )
  );
}
