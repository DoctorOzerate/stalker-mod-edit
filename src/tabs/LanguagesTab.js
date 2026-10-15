import React from 'react';

// Вкладка «Языки» — таблицы локализации (string_table).
// Состав: сводная таблица по локалям, редактирование по языкам, подсветка
// пустых переводов и дубликатов id, контекст использования в репликах.
// Принимает единый контекст ctx, сформированный в App.js.
export function renderLanguagesTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, stringsRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, importStringTables, updateScript, insertSnippet, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, mutateItems, undoItems, redoItems, addItem, deleteItem, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml, findTextIdIssues, textIdCoverage } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 p-6 overflow-auto' },
      React.createElement('div', { className: 'max-w-3xl mx-auto grid gap-4' },
        React.createElement('div', { className: 'flex items-center justify-between' },
          React.createElement('div', {},
            React.createElement('h2', { className: 'font-bold' }, 'Языки (string_table)'),
            React.createElement('p', { className: 'text-xs ' + ui.muted }, 'ID строк · всего переводов: ' + (project.languages || []).reduce(function (n, l) { return n + (l.entries || []).length; }, 0) + ' · покрытие реплик: ' + textIdCoverage(project.dialogues || [], project.languages || []).pct + '%')
          ),
          React.createElement('div', { className: 'flex gap-2' },
            React.createElement('button', { className: btn, onClick: function () { setLangTableMode(!langTableMode); } }, langTableMode ? 'По языкам' : 'Сводная таблица'),
            React.createElement('button', { className: btn, onClick: function () { stringsRef.current.click(); } }, 'Импорт .xml'),
            React.createElement('button', { className: accentBtn, onClick: function () { patch({ languages: project.languages.concat([{ locale: 'rus', name: 'Русский', entries: [{ id: 'st_new', text: '' }] }]) }); say('Таблица создана'); } }, '＋ Таблица')
          )
        ),
        (function () {
          const issues = findTextIdIssues(project.languages || [], project.dialogues || []);
          if (!issues.length) return null;
          const grouped = {};
          issues.forEach(function (i) { (grouped[i.msg] = grouped[i.msg] || []).push(i); });
          return React.createElement('div', { className: 'rounded-xl border p-4 grid gap-2 ' + ui.card, style: { borderColor: '#f87171' } },
            React.createElement('div', { className: 'text-sm font-semibold text-red-400' }, 'Проверка text_id: найдено проблем ' + issues.length),
            Object.keys(grouped).map(function (msg) {
              return React.createElement('div', { key: msg, className: 'grid gap-0.5' },
                React.createElement('div', { className: 'text-xs font-mono ' + ui.muted }, msg + ' (' + grouped[msg].length + ')'),
                React.createElement('div', { className: 'text-[11px] break-all' }, grouped[msg].slice(0, 12).map(function (i) { return i.text_id + (i.locale ? '@' + i.locale : ''); }).join(', ') + (grouped[msg].length > 12 ? ' …' : ''))
              );
            })
          );
        })(),
        langTableMode && React.createElement('div', { className: 'overflow-auto rounded-xl border ' + ui.card },
          React.createElement('table', { className: 'w-full text-xs' },
            React.createElement('thead', {}, React.createElement('tr', {},
              React.createElement('th', { className: 'p-2 text-left border-b border-inherit' }, 'id'),
              (project.languages || []).map(function (l) { return React.createElement('th', { key: l.locale, className: 'p-2 text-left border-b border-inherit' }, l.locale); })
            )),
            React.createElement('tbody', {}, (function () {
              const rows = {};
              (project.languages || []).forEach(function (l) { (l.entries || []).forEach(function (e) { (rows[e.id] = rows[e.id] || {}); rows[e.id][l.locale] = e.text || ''; }); });
              const idList = Object.keys(rows).sort();
              const setCell = function (id, locale, value) { patch({ languages: (project.languages || []).map(function (l) { if (l.locale !== locale) return l; const found = (l.entries || []).some(function (e) { return e.id === id; }); return Object.assign({}, l, { entries: found ? (l.entries || []).map(function (e) { return e.id === id ? Object.assign({}, e, { text: value }) : e; }) : (l.entries || []).concat([{ id: id, text: value }]) }); }) }); };
              return idList.map(function (id) { return React.createElement('tr', { key: id },
                React.createElement('td', { className: 'p-1 border-b border-inherit font-mono ' + ui.muted }, id),
                (project.languages || []).map(function (l) { return React.createElement('td', { key: l.locale, className: 'p-1 border-b border-inherit' }, React.createElement('input', { className: field, value: rows[id][l.locale] || '', onChange: function (e) { setCell(id, l.locale, e.target.value); } })); })
              ); });
            })())
          )
        ),
        !langTableMode && project.languages.map(function (lang, li) {
          const setLang = function (k, v) { patch({ languages: project.languages.map(function (l, i) { return i === li ? Object.assign({}, l, (function () { const o = {}; o[k] = v; return o; })()) : l; }) }); };
          const setEntry = function (ei, k, v) { patch({ languages: project.languages.map(function (l, i) { return i === li ? Object.assign({}, l, { entries: l.entries.map(function (en, j) { return j === ei ? Object.assign({}, en, (function () { const o = {}; o[k] = v; return o; })()) : en; }) }) : l; }) }); };
          const addEntry = function () { patch({ languages: project.languages.map(function (l, i) { return i === li ? Object.assign({}, l, { entries: l.entries.concat([{ id: 'st_' + l.entries.length, text: '' }]) }) : l; }) }); };
          const dupIds = {};
          (lang.entries || []).forEach(function (en) { dupIds[en.id] = (dupIds[en.id] || 0) + 1; });
          return React.createElement('div', { key: li, className: 'rounded-xl border p-4 grid gap-3 ' + ui.card },
            React.createElement('div', { className: 'flex items-center gap-2' },
              React.createElement('input', { className: field + ' w-20', value: lang.locale, onChange: function (e) { setLang('locale', e.target.value); }, title: 'Локаль' }),
              React.createElement('input', { className: field, value: lang.name, onChange: function (e) { setLang('name', e.target.value); } }),
              React.createElement('button', { className: 'text-red-500 text-xs', onClick: function () { patch({ languages: project.languages.filter(function (_, i) { return i !== li; }) }); } }, 'Удалить')
            ),
            lang.entries.map(function (en, ei) {
              const isDup = dupIds[en.id] > 1, isEmpty = !en.text;
              const usage = nodes.filter(function (n) { return n.data && n.data.text_id === en.id; }).length;
              return React.createElement('div', { key: ei, className: 'grid grid-cols-[160px_1fr] gap-2' },
                React.createElement('input', { className: field + (isDup ? ' border-red-400' : ''), value: en.id, onChange: function (e) { setEntry(ei, 'id', e.target.value); }, placeholder: 'id', title: usage ? 'Используется в ' + usage + ' репликах' : 'Не используется' }),
                React.createElement('input', { className: field + (isEmpty ? ' border-yellow-400' : ''), value: en.text, onChange: function (e) { setEntry(ei, 'text', e.target.value); }, placeholder: isEmpty ? 'нет перевода' : 'текст' })
              );
            }),
            React.createElement('button', { className: btn, onClick: addEntry }, '＋ Строка')
          );
        }),
        !langTableMode && project.languages.length === 0 && React.createElement('p', { className: ui.muted }, 'Таблиц нет.')
      )
    )
  );
}
