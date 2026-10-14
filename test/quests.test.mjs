import test from 'node:test';
import assert from 'node:assert/strict';
import { DOMParser } from '@xmldom/xmldom';
import { questsToXml, parseGameTasksXml, parseTaskManagerLtx, questChainSvg, taskManagerLtx, parseCondlistEntry, condlistEntryToStr } from '../src/lib/quests.js';

test('questsToXml exports reward tag', () => {
  const xml = questsToXml([{ id: 'q1', format: 'game_tasks', title: 't', text: 'x', reward: 'money:5000' }], 'utf-8');
  assert.ok(xml.indexOf('<reward>money:5000</reward>') !== -1);
});

test('questsToXml task_manager reward', () => {
  const xml = questsToXml([{ id: 'q1', format: 'task_manager', title: 't', text: 'x', reward: 'wpn_ak74' }], 'utf-8');
  assert.ok(xml.indexOf('<task id="q1">') !== -1);
  assert.ok(xml.indexOf('<reward>wpn_ak74</reward>') !== -1);
});

test('parseGameTasksXml handles fragment without root and prio attr', () => {
  globalThis.DOMParser = DOMParser;
  const xml = '<game_task id="q1" prio="5"><title>t</title><text>x</text><objective><text>o1</text></objective></game_task><game_task id="q2"><title>t2</title></game_task>';
  const q = parseGameTasksXml(xml);
  assert.equal(q.length, 2);
  assert.equal(q[0].id, 'q1');
  assert.equal(q[0].prior, '5');
});

test('parseGameTasksXml handles game_tasks_data root and includes', () => {
  globalThis.DOMParser = DOMParser;
  const xml = '<game_tasks_data>#include "gameplay\\tasks_escape.xml"\n<game_task id="q1"><title>t</title></game_task></game_tasks_data>';
  const q = parseGameTasksXml(xml);
  assert.equal(q.length, 1);
  assert.equal(q[0].id, 'q1');
});

test('parseTaskManagerLtx reads CoP LTX tasks', () => {
  const text = '[test_task]\nicon = ui_icon\nprior = 5\ntitle = test_name\ndescr = test_text\ntarget = test_target\nreward_money = 10000\ncondlist_0 = {+x} complete\nstoryline = true\n';
  const q = parseTaskManagerLtx(text);
  assert.equal(q.length, 1);
  assert.equal(q[0].id, 'test_task');
  assert.equal(q[0].reward, 'money:10000');
  assert.equal(q[0].objectives[0].text, 'test_target');
  assert.equal(q[0].condlist[0], '{+x} complete');
});

test('questChainSvg renders nodes and edges', () => {
  const quests = [{ id: 'a', category: 'Сюжет' }, { id: 'b', category: 'Сюжет' }, { id: 'c', category: 'Побочный' }];
  const svg = questChainSvg([['a', 'b'], ['a', 'c']], quests);
  assert.ok(svg.indexOf('<svg') !== -1);
  assert.ok(svg.indexOf('>a</text>') !== -1);
  assert.ok(svg.indexOf('<path') !== -1);
});

test('taskManagerLtx round-trips full fields', () => {
  const text = '[task_manager]\ntask = tbt_task_1\n[tbt_task_1]\nicon = ui_mt_image_attack_tower\nprior = 4\ntitle = tbt_task_1_name\ndescr = tbt_task_1_text\ntarget = tbt_snork\nreward_money = 10000\ncondlist_0 = {+tbt_task_1_complete} complete\nstoryline = true\n';
  const parsed = parseTaskManagerLtx(text);
  assert.equal(parsed.length, 1);
  const q = parsed[0];
  assert.equal(q.reward_money, '10000');
  assert.equal(q.storyline, 'true');
  assert.equal(q.condlist[0], '{+tbt_task_1_complete} complete');
  const out = taskManagerLtx(parsed);
  assert.ok(out.indexOf('reward_money = 10000') !== -1);
  assert.ok(out.indexOf('storyline = true') !== -1);
  assert.ok(out.indexOf('condlist_0 = {+tbt_task_1_complete} complete') !== -1);
  assert.ok(out.indexOf('target = tbt_snork') !== -1);
});

test('parseCondlistEntry and condlistEntryToStr round-trip', () => {
  const p = parseCondlistEntry('{+tbt_task_1_complete} complete');
  assert.equal(p.cond, '{+tbt_task_1_complete}');
  assert.equal(p.action, 'complete');
  assert.equal(condlistEntryToStr(p.cond, p.action), '{+tbt_task_1_complete} complete');
  const p2 = parseCondlistEntry('complete');
  assert.equal(p2.cond, '');
  assert.equal(p2.action, 'complete');
});

