import React from 'react';

// Вкладка «Иконки» — работа с атласом ui_icon_equipment (DDS).
// Состав: загрузка DDS, просмотр с выделением/зумом/панорамой, лупа, пресеты,
// коллекции, поиск дубликатов, экспорт DDS (DXT1/3/5), PNG и спрайт-листа.
// Принимает единый контекст ctx, сформированный в App.js.
export function renderIconsTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, updateScript, insertSnippet, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, cropAtlas, fillSelection, alphaSelection, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, mutateItems, undoItems, redoItems, addItem, deleteItem, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml, flipSelection, rotateSelection } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 flex min-h-0' },
      React.createElement('aside', { className: 'w-72 border-r p-4 flex flex-col gap-3 overflow-auto ' + ui.soft },
        React.createElement('button', { className: accentBtn, onClick: function () { ddsRef.current.click(); } }, 'Загрузить DDS'),
        atlasTex && React.createElement('button', { className: btn, onClick: function () { compareRef.current.click(); } }, 'Загрузить для сравнения'),
        React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Пресет атласа',
          React.createElement('select', { className: field, value: '', onChange: function (e) { const p = ATLAS_PRESETS[+e.target.value]; if (p) { patch({ atlas: Object.assign({}, project.atlas, { columns: p.columns, cellSize: p.cellW, cellHeight: p.cellH }) }); pushHist(); } } },
            React.createElement('option', { value: '' }, 'Выбрать…'),
            ATLAS_PRESETS.map(function (p, i) { return React.createElement('option', { key: i, value: i }, p.label); }))),
        React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Колонок в атласе',
          React.createElement('input', { type: 'number', min: 1, className: field, value: project.atlas.columns, onChange: function (e) { patch({ atlas: Object.assign({}, project.atlas, { columns: +e.target.value || 1 }) }); } })
        ),
        React.createElement('div', { className: 'grid grid-cols-2 gap-2' },
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Ячейка W (px)', React.createElement('input', { type: 'number', min: 1, className: field, value: project.atlas.cellSize, onChange: function (e) { patch({ atlas: Object.assign({}, project.atlas, { cellSize: +e.target.value || 50 }) }); } })),
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Ячейка H (px)', React.createElement('input', { type: 'number', min: 1, className: field, value: project.atlas.cellHeight || project.atlas.cellSize, onChange: function (e) { patch({ atlas: Object.assign({}, project.atlas, { cellHeight: +e.target.value || 50 }) }); } }))
        ),
        React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Масштаб (Ctrl+колесо)', React.createElement('input', { type: 'range', min: 0.25, max: 3, step: 0.25, className: field, value: atlasZoom, onChange: function (e) { setAtlasZoom(+e.target.value); } })),
        React.createElement('div', { className: 'flex gap-2' },
          React.createElement('button', { className: btn, disabled: histState.pos <= 0, onClick: undoHist }, '↶'),
          React.createElement('button', { className: btn, disabled: histState.pos >= histState.hist.length - 1, onClick: redoHist }, '↷')
        ),
        hoverCell && React.createElement('div', { className: 'text-[11px] ' + ui.muted }, 'Курсор: ' + hoverCell.x + '×' + hoverCell.y),
        React.createElement('div', { className: 'text-[11px] ' + ui.muted }, 'Выделено: ' + selCell.x + '×' + selCell.y + ' · ' + selSize.w + '×' + selSize.h + ' ячеек (' + (selCell.x * (project.atlas.cellSize || 50)) + ',' + (selCell.y * (project.atlas.cellHeight || project.atlas.cellSize || 50)) + ' px)'),
        atlasTex && React.createElement(React.Fragment, null,
          React.createElement('button', { className: btn, onClick: function () { downloadDataUrl(atlasTex.dataURL, 'ui_icon_equipment.png'); } }, 'Экспорт атласа (PNG)'),
          React.createElement('button', { className: btn, onClick: function () {
            if (!atlasTex) return;
            const cw = project.atlas.cellSize || 50, ch = project.atlas.cellHeight || project.atlas.cellSize || 50;
            const w = (selSize.w || 1) * cw, h = (selSize.h || 1) * ch;
            const cv = document.createElement('canvas'); cv.width = w; cv.height = h;
            const ctx = cv.getContext('2d'); const img = new Image();
            img.onload = function () { ctx.drawImage(img, selCell.x * cw, selCell.y * ch, w, h, 0, 0, w, h); downloadDataUrl(cv.toDataURL('image/png'), 'icon_' + selCell.x + '_' + selCell.y + '.png'); };
            img.src = atlasTex.dataURL;
          } }, 'Экспорт выделенного'),
          React.createElement('button', { className: btn, onClick: function () { exportSpritesheet((project.pinCollections || {})[pinColName] || [], atlasTex, project.atlas.cellSize || 50, project.atlas.cellHeight || project.atlas.cellSize || 50); } }, 'Экспорт спрайт-листа'),
          React.createElement('button', { className: btn, onClick: function () { exportDds('DXT1'); } }, 'DDS DXT1'),
          React.createElement('button', { className: btn, onClick: function () { exportDds('DXT3'); } }, 'DDS DXT3'),
          React.createElement('button', { className: btn, onClick: function () { exportDds('DXT5'); } }, 'DDS DXT5'),
          React.createElement('button', { className: btn, onClick: function () { exportDds('DXT1', true); } }, 'DDS DXT1+mip'),
          React.createElement('button', { className: btn, onClick: copyIcon }, 'Копировать иконку'),
          React.createElement('button', { className: btn, onClick: pasteIcon }, 'Вставить иконку'),
          React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold mt-2 ' + ui.muted }, 'Редактор выделения'),
          React.createElement('button', { className: btn, onClick: cropAtlas }, '✂ Обрезать'),
          React.createElement('button', { className: btn, onClick: fillSelection }, '🖌 Заливка'),
          React.createElement('button', { className: btn, onClick: alphaSelection }, '◐ Прозрачность'),
          React.createElement('button', { className: btn, onClick: function () { flipSelection(true); } }, '↔ Отразить по X'),
          React.createElement('button', { className: btn, onClick: function () { flipSelection(false); } }, '↕ Отразить по Y'),
          React.createElement('button', { className: btn, onClick: function () { rotateSelection(true); } }, '↻ Поворот 90°'),
          React.createElement('button', { className: btn, onClick: function () { rotateSelection(false); } }, '↺ Против 90°'),
          React.createElement('button', { className: btn, onClick: newAtlas }, 'Создать пустой атлас'),
          React.createElement('button', { className: btn, onClick: pinIcon }, '★ Закрепить иконку')
        ),
        compareTex && React.createElement(React.Fragment, null,
          React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Прозрачность сравнения', React.createElement('input', { type: 'range', min: 0, max: 1, step: 0.05, className: field, value: compareOpacity, onChange: function (e) { setCompareOpacity(+e.target.value); } })),
          React.createElement('button', { className: btn, onClick: removeCompare }, 'Убрать сравнение')
        ),
        React.createElement('div', { className: 'flex gap-2' },
          React.createElement('select', { className: field + ' flex-1', value: pinColName, onChange: function (e) { setPinColName(e.target.value); } },
            Object.keys(project.pinCollections || { default: [] }).map(function (name) { return React.createElement('option', { key: name, value: name }, name); })),
          React.createElement('button', { className: btn, onClick: addPinCollection }, '＋')
        ),
        React.createElement('button', { className: btn, onClick: findDuplicateIcons }, 'Найти дубликаты'),
        ((project.pinCollections || {})[pinColName] || []).length > 0 && React.createElement('div', { className: 'grid gap-1' },
          React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Закреплённые · ' + pinColName + ' (' + ((project.pinCollections || {})[pinColName] || []).length + ')'),
          ((project.pinCollections || {})[pinColName] || []).map(function (p, i) {
            return React.createElement('div', { key: i, className: 'flex items-center justify-between text-xs rounded border px-2 py-1 ' + ui.card },
              React.createElement('button', { className: 'text-left', onClick: function () { setSelCell({ x: p.x, y: p.y }); setSelSize({ w: p.w, h: p.h }); } }, p.name + ' · ' + p.x + '×' + p.y + ' (' + p.w + '×' + p.h + ')'),
              React.createElement('button', { className: 'text-red-400', onClick: function () { unpinIcon(i); } }, '×')
            );
          })
        ),
        duplicateCells.length > 0 && React.createElement('div', { className: 'grid gap-1' },
          React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Дубликаты (' + duplicateCells.length + ')'),
          React.createElement('div', { className: 'flex flex-wrap gap-1' }, duplicateCells.map(function (c, i) { return React.createElement('button', { key: i, className: 'text-[11px] px-2 py-0.5 rounded ' + btn, onClick: function () { setSelCell({ x: c.x, y: c.y }); setSelSize({ w: 1, h: 1 }); } }, c.x + '×' + c.y); }))
        ),
        atlasTex && React.createElement('div', { className: 'text-[11px] ' + ui.muted }, atlasTex.name + ' · ' + atlasTex.width + '×' + atlasTex.height + ' · сетка ' + Math.floor(atlasTex.width / (project.atlas.cellSize || 50)) + '×' + Math.floor(atlasTex.height / (project.atlas.cellHeight || project.atlas.cellSize || 50)) + ' ячеек')
      ),
      React.createElement('main', { className: 'flex-1 p-6 overflow-auto' },
        !atlasTex ? React.createElement('div', { className: ui.muted + ' text-sm' }, 'Загрузите ui_icon_equipment.dds через кнопку слева или сканированием gamedata.')
          : atlasTex.error ? React.createElement('div', { className: 'text-sm text-red-500' }, 'Ошибка: ' + atlasTex.error)
          : React.createElement('div', { className: 'grid gap-4' },
              React.createElement(AtlasViewer, { texture: atlasTex, overlay: compareTex, overlayOpacity: compareOpacity, columns: project.atlas.columns, cellW: project.atlas.cellSize, cellH: project.atlas.cellHeight || project.atlas.cellSize, sel: selCell, selW: selSize.w, selH: selSize.h, zoom: atlasZoom, hover: hoverCell, onSelect: setSelCell, onSelectRegion: function (r) { setSelCell({ x: r.x, y: r.y }); setSelSize({ w: r.w, h: r.h }); }, onSelectEnd: pushHist, onHover: setHoverCell, onZoom: function (d) { setAtlasZoom(function (z) { return Math.max(0.25, Math.min(3, +(z + d).toFixed(2))); }); } }),
              hoverCell && (function () { const mag = 4; const cw = project.atlas.cellSize || 50; const ch = project.atlas.cellHeight || project.atlas.cellSize || 50; return React.createElement('div', { className: 'rounded-xl border p-3 ' + ui.card },
                React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Лупа · ' + hoverCell.x + '×' + hoverCell.y),
                React.createElement('div', { className: 'mt-2 rounded border ' + (dark ? 'border-zinc-700' : 'border-slate-300'), style: { width: cw * mag, height: ch * mag, backgroundImage: 'url(' + atlasTex.dataURL + ')', backgroundPosition: '-' + (hoverCell.x * cw * mag) + 'px -' + (hoverCell.y * ch * mag) + 'px', backgroundSize: (atlasTex.width * mag) + 'px ' + (atlasTex.height * mag) + 'px', imageRendering: 'pixelated' } })
              ); })()
            )
      )
    )
  );
}
