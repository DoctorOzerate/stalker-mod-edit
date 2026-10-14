import test from 'node:test';
import assert from 'node:assert/strict';
import { DOMParser } from '@xmldom/xmldom';
import { questsToXml, parseGameTasksXml, parseTaskManagerLtx } from '../src/lib/quests.js';

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
