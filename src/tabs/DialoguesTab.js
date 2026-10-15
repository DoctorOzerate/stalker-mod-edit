import React from 'react';

// Сниппеты для метаданных реплики (действия и условия) (#9).
const META_SNIPPETS = [
  { key: 'has_info', label: '+info', value: '%+quest_done%' },
  { key: 'dont_has_info', label: '-info', value: '%+quest_done%' },
  { key: 'give_info', label: 'выдать info', value: 'quest_given' },
  { key: 'disable_info', label: 'снять info', value: 'quest_given' },
  { key: 'precondition', label: 'has_info()', value: 'has_info("%+quest_done%")' },
  { key: 'action', label: 'give_item', value: 'give_item:medkit' }
];

// Вкладка «Диалоги» — визуальный редактор графа диалога (game_dialogs).
// Состав: палитра нод (создание), холст со SVG-связями и рамкой выделения,
// мини-карта, панель свойств реплики (роль, text_id, metadata), панель проверки.
// Принимает единый контекст ctx, сформированный в App.js (см. объект ctx).
export function renderDialoguesTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, suggestTextId, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, updateScript, insertSnippet, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, mutateItems, undoItems, redoItems, addItem, deleteItem, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, dialogueGroup, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml, resolveTextId, textIdCoverage, openCtxMenu, duplicateNode, playthrough, startPlaythrough, stepPlaythrough, exitPlaythrough } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 flex min-h-0' },
      paletteCollapsed || canvasFull
        ? React.createElement('aside', { className: 'w-9 border-r flex flex-col items-center py-3 gap-2 ' + ui.soft },
            React.createElement('button', { className: btn, onClick: function () { setPaletteCollapsed(false); }, title: 'Развернуть палитру' }, '›')
          )
        : React.createElement('aside', { className: 'w-72 border-r p-4 flex flex-col gap-3 overflow-auto ' + ui.soft },
            React.createElement('div', { className: 'flex gap-1' },
              React.createElement('button', { className: btn + ' flex-1', onClick: function () { setPaletteCollapsed(true); }, title: 'Свернуть палитру' }, '‹'),
              React.createElement('button', { className: btn + ' flex-1', onClick: function () { setCanvasFull(!canvasFull); }, title: 'Полноэкранный холст' }, canvasFull ? '🗗' : '⛶')
            ),
        React.createElement('button', { className: accentBtn, onClick: createDialogue }, '＋ Создать диалог'),
        React.createElement('input', { value: paletteFilter, onChange: function (e) { setPaletteFilter(e.target.value); }, placeholder: 'Поиск нод...', className: field }),
        React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Палитра'),
        filteredTypes.map(function (kv) {
          const k = kv[0], v = kv[1];
          return React.createElement('button', {
            key: k, onClick: function () { addNode(k); },
            className: 'text-left rounded-xl border p-3 ' + ui.card + ' ' + ui.hover
          },
            React.createElement('div', { className: 'flex items-center gap-2' },
              React.createElement('span', { className: 'w-7 h-7 rounded-lg text-white grid place-items-center', style: { background: v.color } }, v.icon),
              React.createElement('b', { className: 'text-xs' }, v.label)
            ),
            React.createElement('div', { className: 'text-[11px] mt-1 ' + ui.muted }, v.desc)
          );
        }),
        React.createElement('div', { className: 'mt-2 flex flex-col gap-2 border-t pt-2 border-inherit' },
          React.createElement('input', { value: graphSearch, onChange: function (e) { setGraphSearch(e.target.value); }, placeholder: 'Поиск по графу…', className: field }),
          (function () { const st = dialogueStats(nodes, edges); return React.createElement('div', { className: 'text-[11px] ' + ui.muted }, 'Нод ' + st.nodes + ' · связей ' + st.edges + ' · реплик ' + st.phrases + ' · веток ' + st.branches + ' · глубина ' + st.maxDepth + ' · ср.текст ' + st.avgText + ' · покрытие ' + textIdCoverage(project.dialogues || [], project.languages || []).pct + '%'); })(),
          React.createElement('div', { className: 'flex gap-1' },
            React.createElement('button', { className: (colorByRole ? accentBtn : btn) + ' flex-1 text-[11px]', onClick: function () { setColorByRole(!colorByRole); } }, 'Цвет по роли'),
            React.createElement('button', { className: btn + ' flex-1 text-[11px]', onClick: validate }, 'Валидировать')
          ),
          React.createElement('div', { className: 'flex gap-1' },
            React.createElement('button', { className: btn + ' flex-1 text-[11px]', disabled: graphHist.pos <= 0, onClick: undoGraph }, '↶ Отменить'),
            React.createElement('button', { className: btn + ' flex-1 text-[11px]', disabled: graphHist.pos >= graphHist.hist.length - 1, onClick: redoGraph }, '↷ Повторить')
          ),
          React.createElement('button', { className: btn, onClick: autoLayout }, 'Авто-раскладка'),
          React.createElement('button', { className: (playthrough ? accentBtn : btn), title: 'Пошаговый прогон диалога', onClick: function () { if (playthrough) exitPlaythrough(); else startPlaythrough(); } }, playthrough ? '■ Завершить прогон' : '▶ Прогон диалога'),
          React.createElement('button', { className: btn, onClick: function () { const svg = graphToSvg(nodes, edges); if (!svg) { say('Нет нод для экспорта'); return; } download(svg, (project.dialogueId || project.projectName || 'dialogue') + '.svg', 'image/svg+xml', false); say('Граф экспортирован в SVG'); } }, 'Экспорт SVG'),
          React.createElement('button', { className: btn, onClick: loadDemo }, 'Seed-демограф')
        ),
        React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted + ' mt-2' }, 'Загруженные диалоги · ' + (project.dialogues || []).length),
        (function () {
          const groups = {};
          (project.dialogues || []).forEach(function (d) { const g = dialogueGroup(d.id || d.name); (groups[g] = groups[g] || []).push(d); });
          return Object.keys(groups).sort().map(function (g) {
            return React.createElement('div', { key: g },
              React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted + ' mt-2 mb-1' }, g + ' · ' + groups[g].length),
              groups[g].map(function (d) {
                return React.createElement('button', { key: d.id, onClick: function () { loadDialogueIntoGraph(d); }, className: 'text-left rounded-xl border p-2 w-full ' + ui.card + ' ' + ui.hover },
                  React.createElement('b', { className: 'text-xs' }, d.name || d.id || 'Диалог'),
                  React.createElement('div', { className: 'text-[10px] ' + ui.muted }, (Array.isArray(d.nodes) ? d.nodes.length : 0) + ' нод · ' + (Array.isArray(d.edges) ? d.edges.length : 0) + ' связей')
                );
              })
            );
          });
        })()
      ),
      React.createElement('main', {
        ref: canvasRef, className: 'flex-1 relative overflow-hidden',
        style: { backgroundColor: ui.canvas, backgroundImage: 'radial-gradient(' + ui.grid + ' 1px, transparent 1px)', backgroundSize: (20 * zoom) + 'px ' + (20 * zoom) + 'px', backgroundPosition: pan.x + 'px ' + pan.y + 'px' },
        onMouseDown: function (e) { if (e.target === canvasRef.current) {
          if (e.shiftKey) {
            const r = canvasRef.current.getBoundingClientRect();
            const gx = (e.clientX - r.left - pan.x) / zoom, gy = (e.clientY - r.top - pan.y) / zoom;
            dragRef.current = { kind: 'marquee', cx: e.clientX, cy: e.clientY, panX: pan.x, panY: pan.y, zoom: zoom, rectLeft: r.left, rectTop: r.top };
            setMarquee({ x1: gx, y1: gy, x2: gx, y2: gy });
          } else {
            setMultiSelect([]);
            dragRef.current = { kind: 'pan', cx: e.clientX, cy: e.clientY, x: pan.x, y: pan.y };
          }
        } }
      },
        React.createElement('svg', { className: 'absolute inset-0 w-full h-full', style: { pointerEvents: 'none' } },
          React.createElement('g', { transform: 'translate(' + pan.x + ',' + pan.y + ') scale(' + zoom + ')' },
            edges.map(function (e) {
              const s = nodes.find(function (n) { return n.id === e.source; }), t = nodes.find(function (n) { return n.id === e.target; });
              if (!s || !t) return null;
              const x1 = s.x + 210, y1 = s.y + 44, x2 = t.x, y2 = t.y + 44, dx = Math.max(80, Math.abs(x2 - x1) / 2);
              const d = 'M ' + x1 + ' ' + y1 + ' C ' + (x1 + dx) + ' ' + y1 + ', ' + (x2 - dx) + ' ' + y2 + ', ' + x2 + ' ' + y2;
              return React.createElement('g', { key: e.id },
                React.createElement('path', { d: d, stroke: (selected && (e.source === selected || e.target === selected)) ? '#22c55e' : ui.line, strokeWidth: '2.5', fill: 'none', opacity: selected ? ((e.source === selected || e.target === selected) ? 1 : 0.2) : 1 }),
                React.createElement('path', { d: d, stroke: 'transparent', strokeWidth: '14', fill: 'none', style: { pointerEvents: 'stroke', cursor: 'pointer' }, onClick: function () { deleteEdge(e.id); } })
              );
            })
          )
        ),
        React.createElement('div', { className: 'absolute inset-0 pointer-events-none', style: { transform: 'translate(' + pan.x + 'px,' + pan.y + 'px) scale(' + zoom + ')', transformOrigin: '0 0' } },
          marquee && React.createElement('div', { className: 'absolute border border-dashed border-blue-400 bg-blue-400/10', style: { left: marquee.x1, top: marquee.y1, width: Math.max(1, marquee.x2 - marquee.x1), height: Math.max(1, marquee.y2 - marquee.y1) } }),
          nodes.map(function (n) {
            const cfg = NODE_TYPES[n.type] || FALLBACK_NODE;
            const active = selected === n.id;
            const multi = multiSelect.indexOf(n.id) !== -1;
            const txt = nodeLabel(n, project.languages, programSettings.showNodeText !== false);
            const match = !graphSearch || (txt + ' ' + n.id).toLowerCase().indexOf(graphSearch.toLowerCase()) !== -1;
            return React.createElement('div', {
              key: n.id, className: 'absolute w-[210px] rounded-xl border-2 select-none pointer-events-auto ' + ui.card + (active ? ' border-green-500 ring-4 ring-green-500/20' : (multi ? ' border-blue-500' : '')) + (graphSearch && match ? ' ring-2 ring-green-400/40' : ''),
              style: { left: n.x, top: n.y, opacity: graphSearch && !match ? 0.12 : ((!selected || (connectedSet && connectedSet.has(n.id))) ? 1 : 0.25) },
              onMouseDown: function (e) { e.stopPropagation();
                if (e.shiftKey) {
                  setMultiSelect(function (prev) { return prev.indexOf(n.id) !== -1 ? prev.filter(function (x) { return x !== n.id; }) : prev.concat([n.id]); });
                  setSelected(n.id); dragRef.current = null;
                } else if (multiSelect.indexOf(n.id) !== -1 && multiSelect.length > 1) {
                  setSelected(n.id);
                  dragRef.current = { kind: 'group', ids: multiSelect, cx: e.clientX, cy: e.clientY, origins: multiSelect.map(function (id) { const nn = nodes.find(function (x) { return x.id === id; }); return { id: id, x: nn ? nn.x : 0, y: nn ? nn.y : 0 }; }) };
                } else {
                  setSelected(n.id);
                  if (multiSelect.length > 1) setMultiSelect([]);
                  dragRef.current = { kind: 'node', id: n.id, cx: e.clientX, cy: e.clientY, x: n.x, y: n.y };
                }
              },
              onContextMenu: function (e) { openCtxMenu(e, [{ label: '✂ Удалить', action: function () { deleteNode(n.id); } }, { label: '⧉ Дублировать', action: function () { duplicateNode(n.id); } }]); }
            },
              React.createElement('div', { className: 'rounded-t-lg px-3 py-2 text-white flex items-center justify-between', style: { background: (colorByRole && roleColor(n)) ? roleColor(n) : cfg.color } },
                React.createElement('b', { className: 'text-xs' }, cfg.icon + ' ' + cfg.label),
                React.createElement('span', { className: 'text-[10px] opacity-70' }, n.id.slice(0, 6))
              ),
              n.type !== 'START' && React.createElement('button', { onClick: function (e) { e.stopPropagation(); portClick(n.id, 'in'); }, className: 'absolute left-[-8px] top-10 w-4 h-4 rounded-full bg-slate-400 border-2 border-white hover:bg-green-400' }),
              n.type !== 'END' && React.createElement('button', { onClick: function (e) { e.stopPropagation(); portClick(n.id, 'out'); }, className: 'absolute right-[-8px] top-10 w-4 h-4 rounded-full bg-slate-400 border-2 border-white hover:bg-green-400' }),
              React.createElement('div', { className: 'p-3 text-xs' },
                React.createElement('div', { className: 'whitespace-pre-wrap break-words' }, txt),
                n.data && n.data.voice_id && React.createElement('div', { className: 'text-[10px] mt-1 ' + ui.muted }, '🔊 ' + n.data.voice_id),
                (function () { const m = (n.data && n.data.metadata) || {}; const conds = [].concat(m.has_info || [], m.dont_has_info || [], m.precondition || []); if (!conds.length) return null; return React.createElement('div', { className: 'text-[10px] mt-1 text-sky-500', title: 'Условия' }, '❓ ' + conds.join(', ').slice(0, 40)); })(),
                (function () { const m = (n.data && n.data.metadata) || {}; const acts = [].concat(m.action || [], m.give_info || []); if (!acts.length) return null; return React.createElement('div', { className: 'text-[10px] mt-1 text-amber-500', title: 'Действия' }, '⚙ ' + acts.join(', ').slice(0, 40)); })()
              )
            );
          })
        ),
        React.createElement('div', { className: 'absolute left-4 bottom-4 rounded-xl border p-2 flex items-center gap-2 ' + ui.panel },
          React.createElement('button', { className: btn, onClick: centerView, title: 'В центр' }, '⌖'),
          React.createElement('button', { className: btn, onClick: function () { setZoom(function (z) { return Math.max(0.5, +(z - 0.1).toFixed(2)); }); } }, '−'),
          React.createElement('span', { className: 'text-xs ' + ui.muted }, Math.round(zoom * 100) + '%'),
          React.createElement('button', { className: btn, onClick: function () { setZoom(function (z) { return Math.min(2, +(z + 0.1).toFixed(2)); }); } }, '+')
        ),
        connectFrom && React.createElement('div', { className: 'absolute top-4 left-4 px-3 py-2 rounded-lg accent text-white text-xs' }, 'Выберите второй порт. Esc — отмена.'),
        canvasFull && React.createElement('button', { className: 'absolute top-4 right-4 z-10 ' + accentBtn, onClick: function () { setCanvasFull(false); } }, '🗗 Выйти из полноэкранного'),
        (function () {
          if (!nodes.length) return null;
          const xs = nodes.map(function (n) { return n.x; }), ys = nodes.map(function (n) { return n.y; });
          const minX = Math.min.apply(null, xs) - 20, minY = Math.min.apply(null, ys) - 20;
          const maxX = Math.max.apply(null, nodes.map(function (n) { return n.x + 210; })) + 20;
          const maxY = Math.max.apply(null, nodes.map(function (n) { return n.y + 100; })) + 20;
          return React.createElement('div', { className: 'absolute right-4 bottom-4 w-44 h-28 rounded-xl border overflow-hidden pointer-events-auto ' + ui.panel, title: 'Мини-карта (клик — переход)' },
            React.createElement('svg', {
              className: 'w-full h-full', viewBox: minX + ' ' + minY + ' ' + Math.max(1, maxX - minX) + ' ' + Math.max(1, maxY - minY),
              onClick: function (e) {
                const r = e.currentTarget.getBoundingClientRect();
                const gx = minX + (e.clientX - r.left) / r.width * (maxX - minX);
                const gy = minY + (e.clientY - r.top) / r.height * (maxY - minY);
                const cv = canvasRef.current;
                setPan({ x: (cv ? cv.clientWidth : 800) / 2 - gx, y: (cv ? cv.clientHeight : 600) / 2 - gy });
              }
            },
              (function () {
                const cv = canvasRef.current;
                const vw = (cv ? cv.clientWidth : 800) / zoom, vh = (cv ? cv.clientHeight : 600) / zoom;
                return React.createElement('rect', { x: -pan.x / zoom, y: -pan.y / zoom, width: vw, height: vh, fill: 'none', stroke: '#22c55e', strokeWidth: 1.5, opacity: 0.85 });
              })(),
              nodes.map(function (n) { return React.createElement('rect', { key: n.id, x: n.x, y: n.y, width: 210, height: 90, rx: 6, fill: (NODE_TYPES[n.type] || FALLBACK_NODE).color, opacity: 0.75, stroke: n.id === selected ? '#fff' : 'none', strokeWidth: n.id === selected ? 1.5 : 0 }); })
            )
          );
        })(),
        playthrough && (function () {
          const cur = nodes.find(function (n) { return n.id === playthrough.current; });
          const choices = edges.filter(function (e) { return e.source === playthrough.current; }).map(function (e) { return nodes.find(function (n) { return n.id === e.target; }); }).filter(Boolean);
          return React.createElement('div', { className: 'absolute left-4 top-4 w-72 max-h-[70%] overflow-auto rounded-xl border p-3 grid gap-2 ' + ui.panel },
            React.createElement('div', { className: 'flex items-center justify-between' },
              React.createElement('b', { className: 'text-xs' }, '▶ Прогон диалога'),
              React.createElement('button', { className: 'text-red-400 text-xs', onClick: exitPlaythrough }, '✕')
            ),
            React.createElement('div', { className: 'text-xs' }, cur ? ((NODE_TYPES[cur.type] || FALLBACK_NODE).label + ' · ' + cur.id) : '—'),
            cur && React.createElement('div', { className: 'text-xs ' + ui.muted }, String(nodeLabel(cur, project.languages, true) || '').slice(0, 160)),
            React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, choices.length ? 'Следующие реплики' : (cur && cur.type === 'END' ? 'Диалог завершён' : 'Тупик')),
            choices.map(function (c) { return React.createElement('button', { key: c.id, className: btn + ' text-left', onClick: function () { stepPlaythrough(c.id); } }, '→ ' + (NODE_TYPES[c.type] || FALLBACK_NODE).label + ': ' + String(nodeLabel(c, project.languages, true) || c.id).slice(0, 80)); }),
            React.createElement('div', { className: 'text-[10px] ' + ui.muted }, 'Пройдено нод: ' + playthrough.path.length)
          );
        })(),
      ),
      propsCollapsed || canvasFull
        ? React.createElement('aside', { className: 'w-9 border-l flex flex-col items-center py-3 ' + ui.soft },
            React.createElement('button', { className: btn, onClick: function () { setPropsCollapsed(false); }, title: 'Развернуть свойства' }, '‹')
          )
        : React.createElement('aside', { className: 'w-80 border-l flex flex-col ' + ui.soft },
            React.createElement('div', { className: 'p-4 border-b border-inherit flex items-center justify-between' },
              React.createElement('div', { className: 'flex items-center gap-2' },
                React.createElement('button', { className: btn, onClick: function () { setPropsCollapsed(true); }, title: 'Свернуть свойства' }, '›'),
                React.createElement('b', { className: 'text-xs uppercase tracking-wider' }, 'Свойства')
              ),
              selectedNode && React.createElement('button', { className: 'text-red-500 text-xs', onClick: function () { deleteNode(selectedNode.id); } }, 'Удалить')
            ),
        React.createElement('div', { className: 'flex-1 overflow-auto p-4' },
          !selectedNode ? React.createElement('div', { className: ui.muted + ' text-sm text-center mt-20' }, 'Выберите ноду')
            : React.createElement('div', { className: 'flex flex-col gap-3' },
              React.createElement('div', { className: 'text-[11px] font-mono ' + ui.muted }, selectedNode.id + ' · ' + (NODE_TYPES[selectedNode.type] || FALLBACK_NODE).label),
              
              selectedNode.type === 'PHRASE' && React.createElement(React.Fragment, null,
                React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Роль (не угадывается при импорте)',
                  React.createElement('select', { className: field, value: selectedNode.data.role || 'unknown', onChange: function(e){patchNode(selectedNode.id,{role:e.target.value,roleSource:'manual'});} },
                    React.createElement('option',{value:'unknown'},'Не определено'),React.createElement('option',{value:'npc'},'NPC'),React.createElement('option',{value:'player'},'ГГ'))),
                React.createElement('input', { className: field, value: selectedNode.data.phraseId || '', onChange: function(e){patchNode(selectedNode.id,{phraseId:e.target.value});}, placeholder: 'phrase id' }),
                React.createElement('label', { className: 'grid gap-1 text-xs' }, 'text_id',
                  React.createElement('div', { className: 'flex gap-1' },
                    React.createElement('input', { className: field + ' flex-1', value: selectedNode.data.text_id || '', onChange: function (e) { patchNode(selectedNode.id, { text_id: e.target.value, use_id:true }); }, placeholder: 'string_id' }),
                    React.createElement('button', { type: 'button', className: btn + ' text-[11px]', title: 'Автогенерация text_id из phraseId', onClick: function () { patchNode(selectedNode.id, { text_id: suggestTextId(selectedNode, project.dialogueId || project.projectName), use_id: true }); } }, '⤵')
                  )
                ),
                (function () {
                  const resolved = selectedNode.data.text_id ? resolveTextId(project.languages, selectedNode.data.text_id) : null;
                  if (!resolved) return null;
                  return React.createElement('div', { className: 'rounded border px-2 py-1 text-[11px] italic ' + ui.muted }, '«' + resolved.text + '» · ' + resolved.locale);
                })(),
                (function () {
                  const langs = Array.isArray(project.languages) ? project.languages : [];
                  const active = langs.find(function (l) { return l.locale === activePhraseLocale; }) || langs[0];
                  const textId = selectedNode.data.text_id || '';
                  const bindId = function () {
                    const generated = textId || ('st_' + selectedNode.id.replace(/[^a-z0-9_]/gi, '_'));
                    patchNode(selectedNode.id, { text_id: generated, use_id: true });
                    if (!langs.length) patch({ languages: [{ locale: 'rus', name: 'Русский', entries: [{ id: generated, text: selectedNode.data.text || '' }] }] });
                    else if (!langs.some(function (l) { return (l.entries || []).some(function (e) { return e.id === generated; }); })) {
                      patch({ languages: langs.map(function (l) { return Object.assign({}, l, { entries: (l.entries || []).concat([{ id: generated, text: l.locale === 'rus' ? (selectedNode.data.text || '') : '' }]) }); }) });
                    }
                  };
                  const setTranslation = function (value) {
                    if (!active || !textId) return;
                    patch({ languages: langs.map(function (l) {
                      if (l.locale !== active.locale) return l;
                      const found = (l.entries || []).some(function (e) { return e.id === textId; });
                      return Object.assign({}, l, { entries: found ? (l.entries || []).map(function (e) { return e.id === textId ? Object.assign({}, e, { text: value }) : e; }) : (l.entries || []).concat([{ id: textId, text: value }]) });
                    }) });
                  };
                  const entry = active && (active.entries || []).find(function (e) { return e.id === textId; });
                  return React.createElement('div', { className: 'grid gap-2' },
                    React.createElement('div', { className: 'flex gap-1 flex-wrap' }, langs.map(function (lang) { return React.createElement('button', { key: lang.locale, type: 'button', className: (active && lang.locale === active.locale ? accentBtn : btn), onClick: function () { setActivePhraseLocale(lang.locale); } }, lang.locale || '?'); })),
                    !textId && React.createElement('button', { className: btn, type: 'button', onClick: bindId }, 'Создать и привязать text_id'),
                    textId && !langs.length && React.createElement('button', { className: btn, type: 'button', onClick: bindId }, 'Создать таблицу языка'),
                    textId && active && React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Перевод · ' + active.locale, React.createElement('textarea', { rows: 3, className: field, value: entry ? entry.text : '', onChange: function (e) { setTranslation(e.target.value); }, placeholder: 'Перевод для ' + textId })),
                    React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Текст fallback', React.createElement('textarea', { rows: 2, className: field, value: selectedNode.data.text || '', onChange: function (e) { patchNode(selectedNode.id, { text: e.target.value }); }, placeholder: 'Текст реплики' }))
                  );
                })(),
                React.createElement('div', { className: 'grid gap-1' },
                  React.createElement('button', { type: 'button', className: btn, onClick: function () { setMetaOpen(!metaOpen); } }, (metaOpen ? '▾' : '▸') + ' Действия и условия'),
                  metaOpen && React.createElement('div', { className: 'grid gap-2' },
                    React.createElement('div', { className: 'flex flex-wrap gap-1' },
                      META_SNIPPETS.map(function (s) { return React.createElement('button', { key: s.key + '_' + s.label, type: 'button', className: btn + ' text-[10px]', title: s.key + ' → ' + s.value, onClick: function () { var m = Object.assign({}, selectedNode.data.metadata || {}); m[s.key] = (m[s.key] || []).concat([s.value]); patchNode(selectedNode.id, { metadata: m }); } }, s.label); })
                    ),
                    ['has_info','dont_has_info','precondition','give_info','disable_info','action'].map(function(k){return React.createElement('label',{key:k,className:'grid gap-1 text-xs'},k,React.createElement('textarea',{rows:2,className:field,value:((selectedNode.data.metadata||{})[k]||[]).join('\n'),onChange:function(e){var m=Object.assign({},selectedNode.data.metadata||{});m[k]=e.target.value.split(/\r?\n/);patchNode(selectedNode.id,{metadata:m});}}));})
                  )
                )
              ),
              selectedNode.type === 'PHRASE' && React.createElement('input', { className: field, value: selectedNode.data.voice_id || '', onChange: function (e) { patchNode(selectedNode.id, { voice_id: e.target.value }); }, placeholder: 'voice_id необязательно' }),
              ['CONDITION', 'QUEST_CHECK', 'ITEM_CHECK'].indexOf(selectedNode.type) !== -1 && React.createElement('input', { className: field, value: selectedNode.data.expression || '', onChange: function (e) { patchNode(selectedNode.id, { expression: e.target.value }); }, placeholder: 'expression' }),
              selectedNode.type === 'ACTION' && React.createElement(React.Fragment, null,
                React.createElement('select', { className: field, value: selectedNode.data.action_type || '', onChange: function (e) { patchNode(selectedNode.id, { action_type: e.target.value }); } },
                  React.createElement('option', { value: '' }, 'Тип действия'),
                  ['give_info', 'give_item', 'take_item', 'script_call'].map(function (a) { return React.createElement('option', { key: a, value: a }, a); })
                ),
                React.createElement('input', { className: field, value: selectedNode.data.params || '', onChange: function (e) { patchNode(selectedNode.id, { params: e.target.value }); }, placeholder: 'params' })
              )
            )
        ),
        React.createElement('div', { className: 'h-48 border-t border-inherit p-3 overflow-auto' },
          React.createElement('b', { className: 'text-xs' }, 'Проверка'),
          errors.length ? errors.map(function (e, i) { return React.createElement('div', { key: i, className: 'text-xs mt-1 ' + (e.severity === 'warning' ? 'text-amber-500' : 'text-red-500') }, (e.severity === 'warning' ? '⚠ ' : '⛔ ') + (e.msg || e.code)); })
            : React.createElement('div', { className: 'text-xs mt-2 ' + ui.muted }, 'Ошибок нет или проверка ещё не запускалась.'),
          React.createElement('pre', { className: 'mt-3 text-[11px] whitespace-pre-wrap ' + ui.muted }, treeLines.join(NL))
        )
      )
    )
  );
}
