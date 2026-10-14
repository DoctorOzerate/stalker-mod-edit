import test from 'node:test';
import assert from 'node:assert/strict';
import { suggestTextId, validateMetadataSyntax, validateGraph, dialogueGroup } from '../src/lib/graph.js';

test('suggestTextId derives from phraseId and dialogueId', () => {
  const n = { id: 'x__phrase_5__0', data: { phraseId: 'esc_dlg_5' } };
  assert.equal(suggestTextId(n, 'esc_dlg'), 'esc_dlg_5');
  assert.equal(suggestTextId({ id: 'abc', data: { phraseId: '' } }, 'dialogue'), 'dialogue_abc');
  assert.equal(suggestTextId({ id: 'a', data: { phraseId: 'dialogue_7' } }, 'dialogue'), 'dialogue_7');
});

test('validateMetadataSyntax flags bad info and unbalanced precondition', () => {
  assert.deepEqual(validateMetadataSyntax({ has_info: ['+quest_done'], precondition: ['has_info("%x%")'] }), []);
  const bad = validateMetadataSyntax({ has_info: ['a b<c>'], precondition: ['has_info("x"'] });
  assert.ok(bad.some((i) => i.code === 'metadata_syntax'));
  assert.ok(bad.some((i) => i.code === 'metadata_balance'));
});

test('validateGraph reports dead_end and metadata issues', () => {
  const nodes = [
    { id: 's', type: 'START' },
    { id: 'p', type: 'PHRASE', data: { text: 'hi', metadata: { precondition: ['('] } } }
  ];
  const edges = [{ id: 'e', source: 's', target: 'p' }];
  const issues = validateGraph(nodes, edges);
  assert.ok(issues.some((i) => i.code === 'dead_end'));
  assert.ok(issues.some((i) => i.code === 'metadata_balance'));
});

test('dialogueGroup groups by id prefix', () => {
  assert.equal(dialogueGroup('esc_wolf_start'), 'Кордон');
  assert.equal(dialogueGroup('bar_bar_visitors'), 'Бар');
  assert.equal(dialogueGroup('tm_trader_dialog'), 'Торговля');
  assert.equal(dialogueGroup('dm_hello_dialog'), 'Служебные');
  assert.equal(dialogueGroup('oso_who_are_you'), 'Осознание');
  assert.equal(dialogueGroup('some_unknown_id'), 'Some');
});
