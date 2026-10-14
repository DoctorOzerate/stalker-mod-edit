import React from 'react';

// Вкладка «Предметы» — редактор items.ltx с поддержкой LTX-наследования.
// Состав: список по категориям с поиском, редактор секции (собственные/унаследованные
// поля), позиция в инвентаре, слоты экипировки, экспорт CSV/LTX.
// Принимает единый контекст ctx, сформированный в App.js.
export function renderItemsTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, updateScript, insertSnippet, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, mutateItems, undoItems, redoItems, addItem, deleteItem, renameItemSection, replaceItemField, unlockRootSection, unlockedRoots, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, classifyItem, itemFileCategory, itemFieldProfile, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml, openCtxMenu, listSort, setListSort } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 flex min-h-0' },
      React.createElement('aside', { className: 'w-72 border-r p-4 overflow-auto ' + ui.soft },
        React.createElement('button', { className: accentBtn + ' w-full', onClick: addItem }, '＋ Создать предмет'),
        React.createElement('div', { className: 'mt-2 flex flex-col gap-2' },
          React.createElement('button', { className: btn, onClick: exportItemsLtx }, 'Экспорт items.ltx'),
          React.createElement('button', { className: btn, onClick: exportItemsCsv }, 'Экспорт CSV')
        ),
        React.createElement('div', { className: 'mt-2 grid gap-1' },
          React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Массовое редактирование'),
          React.createElement('button', { className: btn, onClick: renameItemSection }, '↺ Переименовать секцию'),
          React.createElement('button', { className: btn, onClick: replaceItemField }, '⧉ Заменить в поле')
        ),
        React.createElement('select',{className:field,value:itemCategory,onChange:function(e){setItemCategory(e.target.value);}},['Все'].concat(ITEM_CATEGORIES).map(function(x){return React.createElement('option',{key:x},x);})),
        React.createElement('input', { value: itemSearch, onChange: function (e) { setItemSearch(e.target.value); }, placeholder: 'Поиск предмета...', className: field }),
        React.createElement('select', { className: field, value: listSort, onChange: function (e) { setListSort(e.target.value); }, title: 'Сортировка списка' },
          React.createElement('option', { value: 'name' }, 'Сортировка: по имени'),
          React.createElement('option', { value: 'count' }, 'Сортировка: по полям')),
        React.createElement('div', { className: 'mt-4 grid gap-2' },
          ITEM_CATEGORIES.map(function (cat) {
            const items = parsedItems.sections.filter(function (s) {
              if (classifyItem(s) !== cat) return false;
              if (!itemSearch) return true;
              const hay = (s.name + ' ' + (s.entries || []).map(function (e) { return e.key + '=' + e.value; }).join(' ')).toLowerCase();
              return hay.indexOf(itemSearch.toLowerCase()) !== -1;
            });
            if (listSort === 'name') items.sort(function (a, b) { return a.name.localeCompare(b.name); });
            else if (listSort === 'count') items.sort(function (a, b) { return b.entries.length - a.entries.length; });
            if (!items.length) return null;
            if (itemCategory !== 'Все' && itemCategory !== cat) return null;
            return React.createElement('div', { key: cat },
              React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted + ' mt-2 mb-1' }, cat + ' · ' + items.length),
              items.map(function (s) {
                return React.createElement('button', { key: s.name, onClick: function () { setSelItem(s.name); }, onContextMenu: function (e) { openCtxMenu(e, [{ label: '✂ Удалить предмет', action: function () { deleteItem(s.name); } }]); }, className: 'text-left rounded-xl border p-3 w-full ' + ui.card + ' ' + ui.hover },
                  React.createElement('b', { className: 'text-xs' }, s.name),
                  React.createElement('div', { className: 'text-[11px] ' + ui.muted }, s.entries.length + ' полей')
                );
              })
            );
          })
        )
      ),
      React.createElement('main', { className: 'flex-1 p-6 overflow-auto' },
        React.createElement('div', { className: 'max-w-4xl grid gap-6' },
        !selSection ? React.createElement('p', { className: ui.muted }, 'Выберите предмет или создайте новый.')
          : React.createElement('div', { className: 'max-w-3xl grid gap-3' },
            React.createElement('div', { className: 'flex items-center justify-between' },
              React.createElement('h2', { className: 'font-bold' }, '[' + selSection.name + ']'),
              React.createElement('div', { className: 'flex gap-2' },
                React.createElement('button', { className: 'text-[11px] ' + btn, disabled: itemsHist.pos <= 0, onClick: undoItems, title: 'Отменить' }, '↶'),
                React.createElement('button', { className: 'text-[11px] ' + btn, disabled: itemsHist.pos >= itemsHist.hist.length - 1, onClick: redoItems, title: 'Повторить' }, '↷'),
                React.createElement('button', { className: 'text-red-500 text-xs', onClick: function () { deleteItem(selSection.name); } }, 'Удалить предмет')
              )
            ),
            (function () {
              const merged = mergedItemEntries(selSection, itemsByName);
              const isRoot = !(selSection.parents && selSection.parents.length);
              const locked = isRoot && !unlockedRoots[selSection.name];
              const inheritedCount = merged.filter(function (e) { return e.inherited; }).length;
              const gx = selSection.entries.find(function (e) { return e.key === 'inv_grid_x'; });
              const gy = selSection.entries.find(function (e) { return e.key === 'inv_grid_y'; });
              const gw = selSection.entries.find(function (e) { return e.key === 'inv_grid_width'; });
              const gh = selSection.entries.find(function (e) { return e.key === 'inv_grid_height'; });
              const gxOk = gx ? isValidGridValue(gx.value) : true;
              const gyOk = gy ? isValidGridValue(gy.value) : true;
              const gwOk = gw ? isValidGridWH(gw.value) : true;
              const ghOk = gh ? isValidGridWH(gh.value) : true;
              const previewIcon = atlasTex && gxOk && gyOk && gx && gy;
              const cellSize = project.atlas.cellSize || 50;
              const gwVal = (gw && gwOk) ? +gw.value : 1;
              const ghVal = (gh && ghOk) ? +gh.value : 1;
              return React.createElement(React.Fragment, null,
                (selSection.parents && selSection.parents.length
                  ? React.createElement('div', { className: 'grid gap-1' },
                      React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Наследует от: ' + selSection.parents.join(', ') + ' · унаследовано полей: ' + inheritedCount),
                      React.createElement('div', { className: 'flex flex-wrap gap-1' }, selSection.parents.map(function (p) { return React.createElement('button', { key: p, className: 'text-[11px] ' + btn + (itemsByName[p.toLowerCase()] ? '' : ' text-red-400'), onClick: function () { if (itemsByName[p.toLowerCase()]) setSelItem(p); } }, '→ ' + p); }))
                    )
                  : React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Нет родителей')),
                (function () {
                  if (!atlasTex) return null;
                  const cs = project.atlas.cellSize || 50;
                  const chh = project.atlas.cellHeight || cs;
                  const cols = Math.max(1, Math.floor(atlasTex.width / cs));
                  const rows = Math.max(1, Math.floor(atlasTex.height / chh));
                  const curX = (gx && gxOk) ? +gx.value : -1;
                  const curY = (gy && gyOk) ? +gy.value : -1;
                  const cell = 16;
                  return React.createElement('div', { className: 'rounded-xl border p-3 grid gap-2 ' + ui.card },
                    React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Иконка в атласе · клик задаёт inv_grid_x/y (' + cols + '×' + rows + ')'),
                    React.createElement('div', { className: 'grid gap-px', style: { gridTemplateColumns: 'repeat(' + cols + ', ' + cell + 'px)', width: cols * cell } },
                      Array.from({ length: cols * rows }).map(function (_, i) {
                        const x = i % cols, y = Math.floor(i / cols);
                        const active = x === curX && y === curY;
                        return React.createElement('button', {
                          key: i,
                          type: 'button',
                          onClick: function () { setItemGrid(x, y); },
                          className: 'border ' + (active ? 'border-green-400 outline outline-1 outline-green-400' : (dark ? 'border-zinc-800' : 'border-slate-300')),
                          style: { width: cell, height: cell, backgroundImage: 'url(' + atlasTex.dataURL + ')', backgroundSize: (cols * cell) + 'px ' + (rows * cell) + 'px', backgroundPosition: '-' + (x * cell) + 'px -' + (y * cell) + 'px', imageRendering: 'pixelated' },
                          title: 'Ячейка ' + x + '×' + y + (active ? ' (текущая)' : '')
                        });
                      })
                    ),
                    React.createElement('div', { className: 'text-[11px] font-mono ' + ui.muted }, 'inv_grid_x=' + curX + ' · inv_grid_y=' + curY)
                  );
                })(),
                (function () {
                  const brokenParents = (selSection.parents || []).filter(function (p) { return !itemsByName[p.toLowerCase()]; });
                  if (!brokenParents.length) return null;
                  return React.createElement('div', { className: 'text-xs text-red-400' }, '⚠ Битые родители (не найдены): ' + brokenParents.join(', '));
                })(),
                (function () {
                  const prof = itemFieldProfile(selSection);
                  if (!prof.length) return null;
                  const existing = new Set(merged.map(function (e) { return e.key; }));
                  return React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
                    React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Быстрые поля · ' + classifyItem(selSection)),
                    React.createElement('div', { className: 'flex flex-wrap gap-1' },
                      prof.map(function (k) {
                        const present = existing.has(k);
                        return React.createElement('button', { key: k, className: 'text-[11px] px-2 py-0.5 rounded ' + (present ? 'bg-green-500/20 text-green-600' : btn), title: present ? 'Поле есть' : 'Добавить ' + k, onClick: function () { if (!present) { mutateItems(addFieldToSection(project.itemsLtx, selSection.name, k, '')); say('Поле ' + k + ' добавлено'); } } }, k + (present ? ' ✓' : ' ＋'));
                      })
                    )
                  );
                })(),
                locked && React.createElement('div', { className: 'rounded-xl border p-3 flex items-center justify-between ' + (dark ? 'border-amber-700 bg-amber-900/10' : 'border-amber-300 bg-amber-50') },
                  React.createElement('div', { className: 'text-[11px] text-amber-600' }, '🔒 Базовая секция (только чтение) — она является родителем для других секций.'),
                  React.createElement('button', { className: btn + ' text-[11px]', onClick: function () { unlockRootSection(selSection.name); } }, 'Разблокировать')
                ),
                merged.map(function (en) {
                  const gridField = ['inv_grid_x', 'inv_grid_y'].indexOf(en.key) !== -1;
                  const whField = ['inv_grid_width', 'inv_grid_height'].indexOf(en.key) !== -1;
                  const ok = gridField ? isValidGridValue(en.value) : (whField ? isValidGridWH(en.value) : true);
                  const overridden = !en.inherited && (selSection.parents || []).some(function (p) { const ps = itemsByName[p.toLowerCase()]; return ps && ps.entries.some(function (x) { return x.key === en.key && x.value !== en.value; }); });
                  return React.createElement('div', { key: en.key + (en.inherited ? '_inh' : '_own'), className: 'grid grid-cols-[180px_1fr_auto] gap-2 items-center' },
                    React.createElement('div', { className: 'text-xs font-mono ' + ui.muted + (en.inherited ? ' opacity-60' : ''), title: en.inherited && en.from ? 'Наследуется от ' + en.from : (overridden ? 'Переопределено относительно родителя' : '') }, (en.inherited ? '🔒 ' : (overridden ? '✏️ ' : '')) + en.key + (en.inherited && en.from ? ' ← ' + en.from : '')),
                    en.inherited || locked
                      ? React.createElement('input', { className: field + ' opacity-60', value: en.value, readOnly: true })
                      : React.createElement(React.Fragment, null,
                          React.createElement('input', { className: field + (ok ? '' : ' border-red-500') + (overridden ? ' border-amber-400' : ''), value: en.value, onChange: function (e) { setItemField(en.line, en.key, e.target.value); }, list: 'dl_' + en.key.replace(/\W/g, '_') }),
                          React.createElement('datalist', { id: 'dl_' + en.key.replace(/\W/g, '_') },
                            (function () { const vals = {}; parsedItems.sections.forEach(function (sec) { sec.entries.forEach(function (e) { if (e.key === en.key && e.value) vals[e.value] = true; }); }); return Object.keys(vals).slice(0, 30).map(function (v) { return React.createElement('option', { key: v, value: v }); }); })()
                          )
                        ),
                    en.inherited
                      ? React.createElement('button', { className: btn + ' text-[11px]', onClick: function () { if (window.confirm('Разблокировать поле «' + en.key + '» и переопределить значение из родителя?')) { mutateItems(addFieldToSection(project.itemsLtx, selSection.name, en.key, en.value)); say('Поле переопределено'); } } }, 'Разблокировать')
                      : (locked ? React.createElement('span', { className: 'text-[11px] ' + ui.muted }, '🔒') : React.createElement('button', { className: 'text-red-400 text-xs', onClick: function () { mutateItems(removeFieldByLine(project.itemsLtx, en.line)); } }, '×'))
                  );
                }),
                (function () {
                  const children = parsedItems.sections.filter(function (s) { return s.name !== selSection.name && (s.parents || []).some(function (p) { return p.toLowerCase() === selSection.name.toLowerCase(); }); });
                  if (!children.length) return null;
                  return React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
                    React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Дочерние секции (' + children.length + ')'),
                    children.map(function (c) { return React.createElement('button', { key: c.name, className: btn + ' text-left text-xs', onClick: function () { setSelItem(c.name); } }, c.name); })
                  );
                })(),
                (function () {
                  const refs = parsedItems.sections.filter(function (s) { return s.name !== selSection.name && (s.entries || []).some(function (e) { return ['immunities_sect', 'bones_koeff_protection', 'hit_fraction'].indexOf(e.key) !== -1 && String(e.value).trim().toLowerCase() === selSection.name.toLowerCase(); }); });
                  if (!refs.length) return null;
                  return React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
                    React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Ссылаются на эту секцию (' + refs.length + ')'),
                    refs.map(function (s) { return React.createElement('button', { key: s.name, className: btn + ' text-left text-xs', onClick: function () { setSelItem(s.name); } }, s.name); })
                  );
                })(),
                React.createElement('button', { className: btn, onClick: function () { mutateItems(addFieldToSection(project.itemsLtx, selSection.name, 'new_key', 'value')); say('Поле добавлено'); } }, '＋ Добавить поле')
              );
            })()
          ),
        React.createElement('div', { className: 'rounded-xl border p-4 grid gap-3 ' + ui.card },
          React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Слоты экипировки'),
          React.createElement('div', { className: 'grid grid-cols-2 md:grid-cols-3 gap-3' },
            [ { key: 'weapon', label: 'Оружие' }, { key: 'armor', label: 'Броня' }, project.series === 'cop' ? { key: 'helmet', label: 'Шлем' } : null ].filter(Boolean).map(function (slot) {
              const name = (project.equipSlots || {})[slot.key];
              const sec = name ? parsedItems.sections.find(function (s) { return s.name === name; }) : null;
              const gx = sec ? sec.entries.find(function (e) { return e.key === 'inv_grid_x'; }) : null;
              const gy = sec ? sec.entries.find(function (e) { return e.key === 'inv_grid_y'; }) : null;
              const iconOk = atlasTex && gx && gy && isValidGridValue(gx.value) && isValidGridValue(gy.value);
              const cs = project.atlas.cellSize || 50;
              return React.createElement('div', { key: slot.key, className: 'rounded-xl border p-3 grid gap-2 ' + (dark ? 'border-zinc-800' : 'border-slate-200') },
                React.createElement('div', { className: 'flex items-center justify-between' },
                  React.createElement('b', { className: 'text-xs' }, slot.label),
                  React.createElement('button', { className: btn, disabled: !selSection, onClick: function () { var o = Object.assign({}, project.equipSlots || {}); o[slot.key] = selSection.name; patch({ equipSlots: o }); } }, 'Назначить')
                ),
                React.createElement('div', { className: 'flex items-center gap-3' },
                  iconOk
                    ? React.createElement('div', { style: { width: cs, height: cs, backgroundImage: 'url(' + atlasTex.dataURL + ')', backgroundPosition: '-' + (+gx.value * cs) + 'px -' + (+gy.value * cs) + 'px', backgroundSize: atlasTex.width + 'px ' + atlasTex.height + 'px', imageRendering: 'pixelated' } })
                    : React.createElement('div', { className: 'w-12 h-12 grid place-items-center border rounded ' + (dark ? 'border-zinc-700' : 'border-slate-300') + ' text-xs ' + ui.muted }, '—'),
                  React.createElement('div', { className: 'text-xs ' + (name ? '' : ui.muted) }, name || 'пусто')
                )
              );
            })
          )
        )
        )
      )
    )
  );
}
