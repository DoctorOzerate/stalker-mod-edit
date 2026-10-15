import React from 'react';

// Вкладка «Квесты» — редактор game_tasks / task_manager.
// Состав: список с поиском/категориями/шаблонами, редактор (цели, infoportion,
// награды, requires), дерево зависимостей, валидация, diff, экспорт XML/LTX.
// Принимает единый контекст ctx, сформированный в App.js.
export function renderQuestsTab(ctx) {
  const { project, selected, setSelected, tab, setTab, welcomeOpen, setWelcomeOpen, settingsOpen, setSettingsOpen, programSettingsOpen, setProgramSettingsOpen, programSettings, setProgramSettings, newName, setNewName, newSeries, setNewSeries, newTheme, setNewTheme, metaOpen, setMetaOpen, lastAutosave, charSearch, setCharSearch, itemSearch, setItemSearch, scriptSearch, setScriptSearch, scriptReplace, setScriptReplace, scriptDiff, setScriptDiff, includePreview, setIncludePreview, sectionSearch, setSectionSearch, expandedSys, setExpandedSys, itemsHist, duplicateCells, iconClipboard, pinColName, setPinColName, charXml, setCharXml, paletteCollapsed, setPaletteCollapsed, propsCollapsed, setPropsCollapsed, canvasFull, setCanvasFull, langTableMode, setLangTableMode, multiSelect, setMultiSelect, marquee, setMarquee, scriptViewMode, setScriptViewMode, collapsedBlocks, paletteFilter, setPaletteFilter, graphSearch, setGraphSearch, colorByRole, setColorByRole, errors, selfResults, setSelfResults, pan, setPan, zoom, setZoom, connectFrom, setConnectFrom, selItem, setSelItem, itemCategory, setItemCategory, selCell, setSelCell, selSize, setSelSize, atlasZoom, setAtlasZoom, hoverCell, setHoverCell, histState, compareOpacity, setCompareOpacity, selScript, setSelScript, selChar, setSelChar, selQuest, setSelQuest, questSearch, setQuestSearch, questCategory, setQuestCategory, questView, setQuestView, questPreviewLocale, setQuestPreviewLocale, questsHist, graphHist, appErrors, setAppErrors, helpOpen, setHelpOpen, activePhraseLocale, setActivePhraseLocale, projects, toast, canvasRef, dragRef, importRef, gamedataRef, scriptRef, ddsRef, compareRef, scriptTextRef, dark, nodes, edges, connectedSet, selectedNode, includes, parsedItems, textures, atlasTex, compareTex, selSection, itemsByName, ui, field, btn, accentBtn, filteredTypes, treeLines, luaColor, TABS, patch, patchNode, say, addNode, createDialogue, deleteNode, deleteEdge, portClick, validate, saveProject, exportProject, exportXml, exportGameZip, openProjectItem, deleteProjectItem, renameProjectItem, createProject, loadDemo, centerView, autoLayout, loadDialogueIntoGraph, resetStorage, pickGamedataFolder, pickResourcesFolder, scanScripts, updateScript, insertSnippet, formatCurrentScript, replaceAllInScripts, jumpToDefinition, toggleBlock, deleteScript, renameScript, loadDds, loadCompareDds, removeCompare, pushHist, undoHist, redoHist, pinIcon, unpinIcon, addPinCollection, findDuplicateIcons, exportDds, copyIcon, pasteIcon, newAtlas, exportSpritesheet, autofillCharDialogs, exportItemsCsv, exportItemsLtx, setItemField, setItemGrid, setSysEntry, removeSysEntry, addSysEntry, addSysSection, mutateItems, undoItems, redoItems, addItem, deleteItem, setQuestField, setQuestObjectiveField, setQuestRequires, addQuestObjective, removeQuestObjective, mutateQuests, undoQuests, redoQuests, addQuest, addQuestFromTemplate, deleteQuest, bindQuestToLocales, relatedDialoguesForQuest, exportQuests, undoGraph, redoGraph, graphToSvg, NODE_TYPES, FALLBACK_NODE, SERIES, ITEM_CATEGORIES, COMMUNITY_ICONS, QUEST_CATEGORIES, QUEST_TEMPLATES, ATLAS_PRESETS, NL, Q, BS, META_KEYS, PALETTE_HIDDEN, AtlasViewer, nodeLabel, roleColor, dialogueStats, parseLtx, parseGameTasksXml, questsToXml, validateQuests, computeQuestChains, taskManagerLtx, includeTreeRows, includeGraphSvg, isFileLoaded, findIncludeSuggestion, tokenizeLua, tokenizeLuaLines, countLuaBrackets, formatLua, luaFunctionBlocks, classifyItem, itemFileCategory, extractCharacterXml, normalizeQuestObjectives, mergedItemEntries, isValidGridValue, isValidGridWH, download, downloadBytes, downloadDataUrl, textBytes, escapeXml, csvCell, splitLines, updateLtxLine, addSection, removeSection, addFieldToSection, removeFieldByLine, parseStringTableXml, parseGeneralCharactersXml, questChainSvg, parseCondlistEntry, condlistEntryToStr, listSort, setListSort } = ctx;
  return (
    React.createElement('div', { className: 'flex-1 flex min-h-0' },
      React.createElement('aside', { className: 'w-80 border-r p-4 overflow-auto ' + ui.soft },
        React.createElement('button', { className: accentBtn + ' w-full', onClick: addQuest }, '＋ Создать квест'),
        React.createElement('select', { className: field + ' w-full mt-2', value: '', onChange: function (e) { const t = QUEST_TEMPLATES.find(function (x) { return x.label === e.target.value; }); if (t) { addQuestFromTemplate(t); e.target.value = ''; } } },
          React.createElement('option', { value: '' }, 'Из шаблона…'),
          QUEST_TEMPLATES.map(function (t) { return React.createElement('option', { key: t.label, value: t.label }, t.label); })),
        React.createElement('div', { className: 'flex gap-2 mt-2' },
          React.createElement('button', { className: btn + ' flex-1', onClick: exportQuests }, 'Экспорт XML'),
          React.createElement('button', { className: btn + ' flex-1', onClick: function () { const txt = taskManagerLtx(project.quests || []); if (!txt) { say('Нет квестов формата task_manager'); return; } downloadBytes(textBytes(txt, 'windows-1251'), 'task_manager.ltx', 'text/plain'); say('task_manager.ltx экспортирован'); } }, 'task_manager.ltx'),
          React.createElement('button', { className: btn, disabled: questsHist.pos <= 0, onClick: undoQuests, title: 'Отменить' }, '↶'),
          React.createElement('button', { className: btn, disabled: questsHist.pos >= questsHist.hist.length - 1, onClick: redoQuests, title: 'Повторить' }, '↷')
        ),
        React.createElement('div', { className: 'flex gap-1 mt-2' },
          React.createElement('button', { className: (questView === 'edit' ? accentBtn : btn) + ' flex-1', onClick: function () { setQuestView('edit'); } }, 'Редактор'),
          React.createElement('button', { className: (questView === 'chains' ? accentBtn : btn) + ' flex-1', onClick: function () { setQuestView('chains'); } }, 'Цепочки')
        ),
        React.createElement('input', { className: field + ' mt-3', value: questSearch, onChange: function (e) { setQuestSearch(e.target.value); }, placeholder: 'Поиск (id, текст, цели)…' }),
        React.createElement('select', { className: field + ' mt-2', value: listSort, onChange: function (e) { setListSort(e.target.value); }, title: 'Сортировка' },
          React.createElement('option', { value: 'name' }, 'Сортировка: по id'),
          React.createElement('option', { value: 'count' }, 'Сортировка: по приоритету')),
        React.createElement('div', { className: 'flex flex-wrap gap-1 mt-2' },
          ['Все'].concat(QUEST_CATEGORIES).map(function (c) { return React.createElement('button', { key: c, className: 'text-[11px] px-2 py-0.5 rounded ' + (questCategory === c ? accentBtn : btn), onClick: function () { setQuestCategory(c); } }, c); })
        ),
        React.createElement('div', { className: 'mt-3 grid gap-2' },
          (project.quests || []).filter(function (q) {
            const s = questSearch.toLowerCase();
            const cat = questCategory === 'Все' || (q.category || '') === questCategory;
            const text = [q.id, q.title, q.text].concat((q.objectives || []).map(function (o) { return typeof o === 'string' ? o : o.text; })).join(' ').toLowerCase();
            return cat && (!s || text.indexOf(s) !== -1);
          }).sort(function (a, b) {
            if (listSort === 'count') { const pa = +a.prior || 0, pb = +b.prior || 0; return pa - pb; }
            return String(a.id).localeCompare(String(b.id));
          }).map(function (q) {
            const dup = (project.quests || []).filter(function (x) { return x.id === q.id; }).length > 1;
            return React.createElement('button', { key: q.id, onClick: function () { setSelQuest(q.id); setQuestView('edit'); }, className: 'text-left rounded-xl border p-3 w-full ' + ui.card + ' ' + ui.hover + (dup ? ' border-red-400' : '') },
              React.createElement('div', { className: 'flex items-center justify-between' },
                React.createElement('b', { className: 'text-xs' }, q.id),
                q.category && React.createElement('span', { className: 'text-[10px] px-1.5 py-0.5 rounded bg-zinc-700 text-zinc-200' }, q.category)
              ),
              React.createElement('div', { className: 'text-[11px] ' + ui.muted }, (q.objectives || []).length + ' целей' + (q.prior ? ' · prior ' + q.prior : '') + (q.format === 'task_manager' ? ' · CoP' : '') + (dup ? ' · дубликат' : '') + (validateQuests([q], project.languages || []).length ? ' · ⚠ ' + validateQuests([q], project.languages || []).length : ''))
            );
          })
        )
      ),
      React.createElement('main', { className: 'flex-1 p-6 overflow-auto' },
        (project.quests || []).length === 0 ? React.createElement('p', { className: ui.muted }, 'Квестов нет. Создайте новый, выберите шаблон или просканируйте gamedata (game_tasks*.xml / task_manager*.xml).')
          : questView === 'chains'
            ? React.createElement('div', { className: 'max-w-3xl grid gap-4' },
                React.createElement('h2', { className: 'font-bold' }, 'Цепочки квестов · ' + computeQuestChains(project.quests || []).length + ' цепочек'),
                (function () {
                  const list = project.quests || [];
                  const completeTo = {};
                  list.forEach(function (q) { if (q.infoportion_complete) completeTo[q.infoportion_complete] = q.id; });
                  const parentOf = {};
                  list.forEach(function (q) { (q.requires || []).forEach(function (r) { const p = completeTo[r]; if (p && p !== q.id) parentOf[q.id] = p; }); });
                  const children = {};
                  Object.keys(parentOf).forEach(function (cid) { (children[parentOf[cid]] = children[parentOf[cid]] || []).push(cid); });
                  const roots = list.filter(function (q) { return !parentOf[q.id]; }).map(function (q) { return q.id; });
                  if (!roots.length) return React.createElement('p', { className: ui.muted }, 'Квестов нет.');
                  const renderNode = function (id, depth) {
                    const kids = children[id] || [];
                    const qq = list.find(function (x) { return x.id === id; });
                    return React.createElement('div', { key: id },
                      React.createElement('div', { style: { marginLeft: depth * 18 }, className: 'flex items-center gap-2 py-0.5' },
                        React.createElement('button', { className: 'text-xs px-2 py-0.5 rounded ' + btn, onClick: function () { setSelQuest(id); setQuestView('edit'); } }, id),
                        qq && qq.infoportion_complete ? React.createElement('span', { className: 'text-[10px] font-mono ' + ui.muted }, '→ ' + qq.infoportion_complete) : null
                      ),
                      kids.map(function (k) { return renderNode(k, depth + 1); })
                    );
                  };
                  const svg = questChainSvg(computeQuestChains(list), list);
                  return React.createElement('div', { className: 'rounded-xl border p-3 grid gap-3 ' + ui.card },
                    svg ? React.createElement('div', {},
                      React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted + ' mb-2' }, 'Граф цепочек'),
                      React.createElement('div', { className: 'overflow-auto max-h-96 rounded border ' + (dark ? 'border-zinc-800' : 'border-slate-200') }, React.createElement('div', { dangerouslySetInnerHTML: { __html: svg } }))
                    ) : null,
                    React.createElement('div', { className: 'grid gap-1' },
                      React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Дерево зависимостей'),
                      roots.map(function (r) { return renderNode(r, 0); }),
                      React.createElement('p', { className: 'text-xs ' + ui.muted + ' mt-2' }, 'Связи: «infoportion set_complete» одного квеста → «Требует» другого.')
                    )
                  );
                })()
              )
            : (function () {
              const q = (project.quests || []).find(function (x) { return x.id === selQuest; }) || (project.quests || [])[0];
              const qTransIn = function (id, locale) {
                if (!id) return '';
                const langs = Array.isArray(project.languages) ? project.languages : [];
                const l = locale ? langs.find(function (x) { return x.locale === locale; }) : null;
                const src = l ? [l] : langs;
                for (let i = 0; i < src.length; i++) { const e = (src[i].entries || []).find(function (x) { return x.id === id; }); if (e && e.text) return e.text; }
                return '';
              };
              const qTrans = function (id) { return qTransIn(id, questPreviewLocale); };
              const oTxt = function (o) { return typeof o === 'string' ? o : (o && o.text) || ''; };
              const issues = validateQuests([q], project.languages);
              const related = relatedDialoguesForQuest(q);
              const diffRows = q.original ? (function () {
                const fields = [['id', 'ID'], ['prior', 'Приоритет'], ['title', 'Заголовок'], ['text', 'Описание'], ['infoportion_complete', 'Завершение'], ['infoportion_fail', 'Провал'], ['icon', 'Иконка'], ['location', 'Локация'], ['article', 'Статья']];
                const rows = fields.map(function (f) { return { label: f[1], changed: String(q.original[f[0]] || '') !== String(q[f[0]] || ''), a: String(q.original[f[0]] || ''), b: String(q[f[0]] || '') }; });
                const objStr = function (arr) { return (arr || []).map(function (o) { return typeof o === 'string' ? o : ((o.text || '') + '|' + (o.icon || '') + '|' + (o.infoportion || '') + '|' + (o.article || '')); }).join('; '); };
                const origObj = objStr(q.original.objectives), curObj = objStr(q.objectives);
                if (origObj !== curObj) rows.push({ label: 'Цели', changed: true, a: origObj || '—', b: curObj || '—' });
                return rows.filter(function (r) { return r.changed; });
              })() : [];
              return React.createElement('div', { className: 'max-w-3xl grid gap-3' },
                React.createElement('div', { className: 'flex items-center justify-between' },
                  React.createElement('h2', { className: 'font-bold' }, 'Редактор квеста'),
                  React.createElement('div', { className: 'flex gap-2' },
                    React.createElement('span', { className: 'text-[11px] px-2 py-0.5 rounded ' + (q.format === 'task_manager' ? 'bg-purple-800 text-purple-100' : 'bg-zinc-800 text-zinc-200') }, q.format === 'task_manager' ? 'CoP · task_manager' : 'SoC/CS · game_tasks'),
                    React.createElement('button', { className: btn, onClick: function () { bindQuestToLocales(q); } }, 'Привязать к локалям')
                  )
                ),
                React.createElement('div', { className: 'grid md:grid-cols-3 gap-3' },
                  React.createElement('label', { className: 'grid gap-1 text-xs' }, 'ID', React.createElement('input', { className: field, value: q.id, onChange: function (e) { setQuestField(q.id, 'id', e.target.value); } })),
                  React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Приоритет', React.createElement('input', { className: field, value: q.prior || '', onChange: function (e) { setQuestField(q.id, 'prior', e.target.value); } })),
                  React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Категория', React.createElement('select', { className: field, value: q.category || '', onChange: function (e) { setQuestField(q.id, 'category', e.target.value); } },
                    React.createElement('option', { value: '' }, '—'),
                    QUEST_CATEGORIES.map(function (c) { return React.createElement('option', { key: c, value: c }, c); })))
                ),
                React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Заголовок (title)', React.createElement('input', { className: field, value: q.title || '', onChange: function (e) { setQuestField(q.id, 'title', e.target.value); } }), qTrans(q.title) ? React.createElement('span', { className: 'text-[11px] ' + ui.muted }, '↳ ' + qTrans(q.title)) : null),
                React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Описание (text)', React.createElement('textarea', { rows: 3, className: field, value: q.text || '', onChange: function (e) { setQuestField(q.id, 'text', e.target.value); } }), qTrans(q.text) ? React.createElement('span', { className: 'text-[11px] ' + ui.muted }, '↳ ' + qTrans(q.text)) : null),
                React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Требует infoportion (через запятую)', React.createElement('input', { className: field, value: (q.requires || []).join(', '), onChange: function (e) { setQuestRequires(q.id, e.target.value); }, placeholder: 'esc_prev_complete' })),
                React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Награда (метаданные)', React.createElement('input', { className: field, value: q.reward || '', onChange: function (e) { setQuestField(q.id, 'reward', e.target.value); }, placeholder: 'напр. af_medusa, 1000 руб' })),
                q.format === 'task_manager' && React.createElement(React.Fragment, null,
                  React.createElement('div', { className: 'grid md:grid-cols-2 gap-3' },
                    React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Сюжетный (storyline)', React.createElement('select', { className: field, value: q.storyline || '', onChange: function (e) { setQuestField(q.id, 'storyline', e.target.value); } },
                      React.createElement('option', { value: '' }, '—'),
                      React.createElement('option', { value: 'true' }, 'true'),
                      React.createElement('option', { value: 'false' }, 'false'))),
                    React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Награда деньгами (reward_money)', React.createElement('input', { className: field, value: q.reward_money || '', onChange: function (e) { setQuestField(q.id, 'reward_money', e.target.value); } }))
                  ),
                  React.createElement('div', { className: 'grid md:grid-cols-2 gap-3' },
                    React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Награда предметом (reward_item)', React.createElement('input', { className: field, value: q.reward_item || '', onChange: function (e) { setQuestField(q.id, 'reward_item', e.target.value); } })),
                    React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Награда тайником (reward_treasure)', React.createElement('input', { className: field, value: q.reward_treasure || '', onChange: function (e) { setQuestField(q.id, 'reward_treasure', e.target.value); } }))
                  ),
                  React.createElement('div', { className: 'grid gap-1 text-xs' },
                    React.createElement('div', { className: 'flex items-center justify-between' },
                      React.createElement('b', {}, 'Condlist (условия)'),
                      React.createElement('button', { className: btn + ' text-[11px]', onClick: function () { setQuestField(q.id, 'condlist', (q.condlist || []).concat([''])); } }, '＋ Условие')
                    ),
                    (q.condlist || []).map(function (c, i) {
                      const p = parseCondlistEntry(c);
                      return React.createElement('div', { key: i, className: 'grid grid-cols-[1fr_1fr_auto] gap-1' },
                        React.createElement('input', { className: field + ' font-mono text-[11px]', value: p.cond, onChange: function (e) { const next = (q.condlist || []).slice(); next[i] = condlistEntryToStr(e.target.value, p.action); setQuestField(q.id, 'condlist', next); }, placeholder: '{+info}' }),
                        React.createElement('input', { className: field + ' font-mono text-[11px]', value: p.action, onChange: function (e) { const next = (q.condlist || []).slice(); next[i] = condlistEntryToStr(p.cond, e.target.value); setQuestField(q.id, 'condlist', next); }, placeholder: 'complete' }),
                        React.createElement('button', { className: 'text-red-400 text-xs', onClick: function () { const next = (q.condlist || []).slice(); next.splice(i, 1); setQuestField(q.id, 'condlist', next); } }, '×')
                      );
                    }),
                    (q.condlist || []).length === 0 && React.createElement('div', { className: 'text-[11px] ' + ui.muted }, 'Условий нет.')
                  ),
                ),
                React.createElement('div', { className: 'grid gap-2' },
                  React.createElement('div', { className: 'flex items-center justify-between' },
                    React.createElement('b', { className: 'text-xs' }, 'Цели (objectives)'),
                    React.createElement('button', { className: btn, onClick: function () { addQuestObjective(q.id); } }, '＋ Цель')
                  ),
                  (q.objectives || []).map(function (o, i) {
                    return React.createElement('div', { key: i, className: 'rounded-xl border p-3 grid gap-2 ' + ui.card },
                      React.createElement('div', { className: 'flex gap-2' },
                        React.createElement('input', { className: field + ' flex-1', value: oTxt(o), onChange: function (e) { setQuestObjectiveField(q.id, i, 'text', e.target.value); }, placeholder: 'text_id' }),
                        React.createElement('button', { className: 'text-red-400 text-xs', onClick: function () { removeQuestObjective(q.id, i); } }, '×')
                      ),
                      qTrans(oTxt(o)) ? React.createElement('span', { className: 'text-[11px] ' + ui.muted }, '↳ ' + qTrans(oTxt(o))) : null,
                      React.createElement('div', { className: 'grid grid-cols-3 gap-2' },
                        React.createElement('input', { className: field, value: (o && o.icon) || '', onChange: function (e) { setQuestObjectiveField(q.id, i, 'icon', e.target.value); }, placeholder: 'icon' }),
                        React.createElement('input', { className: field, value: (o && o.infoportion) || '', onChange: function (e) { setQuestObjectiveField(q.id, i, 'infoportion', e.target.value); }, placeholder: 'infoportion' }),
                        React.createElement('input', { className: field, value: (o && o.article) || '', onChange: function (e) { setQuestObjectiveField(q.id, i, 'article', e.target.value); }, placeholder: 'article' })
                      )
                    );
                  }),
                  (q.objectives || []).length === 0 && React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Целей нет.')
                ),
                React.createElement('div', { className: 'grid md:grid-cols-2 gap-3' },
                  React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Завершение (infoportion set_complete)', React.createElement('input', { className: field, value: q.infoportion_complete || '', onChange: function (e) { setQuestField(q.id, 'infoportion_complete', e.target.value); } })),
                  React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Провал (infoportion set_fail)', React.createElement('input', { className: field, value: q.infoportion_fail || '', onChange: function (e) { setQuestField(q.id, 'infoportion_fail', e.target.value); } })),
                  React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Иконка (icon)', React.createElement('input', { className: field, value: q.icon || '', onChange: function (e) { setQuestField(q.id, 'icon', e.target.value); } })),
                  React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Локация (location)', React.createElement('input', { className: field, value: q.location || '', onChange: function (e) { setQuestField(q.id, 'location', e.target.value); } }))
                ),
                React.createElement('label', { className: 'grid gap-1 text-xs' }, 'Статья энциклопедии (article)', React.createElement('input', { className: field, value: q.article || '', onChange: function (e) { setQuestField(q.id, 'article', e.target.value); } })),
                React.createElement('div', { className: 'rounded-xl border p-3 grid gap-2 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Связь с диалогами (' + related.length + ')'),
                  related.length ? related.map(function (r) { return React.createElement('button', { key: r.dlg.id, className: btn + ' text-left text-xs', onClick: function () { loadDialogueIntoGraph(r.dlg); setTab('dialogues'); } }, (r.dlg.name || r.dlg.id) + ' · ' + r.hits + ' совп.'); })
                    : React.createElement('div', { className: 'text-xs ' + ui.muted }, 'Нет диалогов, выдающих infoportion этого квеста.')
                ),
                issues.length > 0 && React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Проверка'),
                  issues.map(function (iss, i) { return React.createElement('div', { key: i, className: 'text-xs ' + (iss.severity === 'error' ? 'text-red-400' : 'text-amber-400') }, (iss.severity === 'error' ? '⛔ ' : '⚠ ') + iss.msg); })
                ),
                diffRows.length > 0 && React.createElement('div', { className: 'rounded-xl border p-3 grid gap-1 ' + ui.card },
                  React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Изменения от оригинала'),
                  diffRows.map(function (r, i) { return React.createElement('div', { key: i, className: 'text-xs grid grid-cols-[90px_1fr_1fr] gap-2' },
                    React.createElement('span', { className: ui.muted }, r.label),
                    React.createElement('span', { className: 'text-red-400 line-through truncate' }, r.a || '—'),
                    React.createElement('span', { className: 'text-green-400 truncate' }, r.b || '—')
                  ); })
                ),
                React.createElement('div', { className: 'rounded-xl border p-3 ' + ui.card },
                  React.createElement('div', { className: 'flex items-center justify-between' },
                    React.createElement('div', { className: 'text-[10px] uppercase tracking-widest font-bold ' + ui.muted }, 'Предпросмотр'),
                    React.createElement('select', { className: field + ' w-32', value: questPreviewLocale, onChange: function (e) { setQuestPreviewLocale(e.target.value); } },
                      React.createElement('option', { value: '' }, 'Первая локаль'),
                      (project.languages || []).map(function (l) { return React.createElement('option', { key: l.locale, value: l.locale }, l.locale); }))
                  ),
                  React.createElement('div', { className: 'mt-2 grid gap-1 text-sm' },
                    React.createElement('b', {}, qTrans(q.title) || q.title),
                    React.createElement('div', { className: 'text-xs ' + ui.muted }, qTrans(q.text) || q.text),
                    (q.objectives || []).map(function (o, i) { return React.createElement('div', { key: i, className: 'text-xs' }, '• ' + (qTrans(oTxt(o)) || oTxt(o))); })
                  ),
                  React.createElement('pre', { className: 'mt-2 text-[11px] font-mono whitespace-pre-wrap ' + ui.muted }, questsToXml([q], 'windows-1251'))
                ),
                React.createElement('div', { className: 'flex gap-2' },
                  React.createElement('button', { className: 'text-red-500 text-xs', onClick: function () { deleteQuest(q.id); } }, 'Удалить квест')
                )
              );
            })()
      )
    )
  );
}
