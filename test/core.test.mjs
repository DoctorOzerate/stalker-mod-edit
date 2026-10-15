import test from 'node:test';
import assert from 'node:assert/strict';
import { DOMParser } from '@xmldom/xmldom';
import {
  parseLtx,
  parseDialogueXml,
  parseCharactersXml,
  migrateProject,
  escapeText,
  escapeAttribute,
  cp1251Encode,
  decodeGameBytes,
  validateGraph,
  validateCharacter,
} from '../src/lib/core.js';

test('LTX inheritance', () => {
  const p = parseLtx('[child]:base1, base2\r\nx=1\r\n');
  assert.deepEqual(p.sections[0].parents, ['base1', 'base2']);
  assert.equal(p.sections[0].entries[0].value, '1');
});

test('parseCharactersXml captures supplies', () => {
  globalThis.DOMParser = DOMParser;
  const xml = '<xml><specific_character id="aes_Monolit_rpg"><name>N</name><community>monolith</community><supplies>[spawn] \nwpn_walther \nammo_9x19_fmj</supplies></specific_character></xml>';
  const chars = parseCharactersXml(xml);
  assert.equal(chars.length, 1);
  assert.ok(chars[0].supplies.indexOf('wpn_walther') !== -1);
});

test('LTX include parsing', () => {
  const p = parseLtx('#include "a.ltx"\n#include "b.ltx"\n');
  assert.deepEqual(p.includes.map((x) => x.path), ['a.ltx', 'b.ltx']);
});

test('phrase list and id zero', () => {
  const x = '<game_dialogs><dialog id="d"><phrase_list><phrase id="0"><text>t</text><next>2</next></phrase><phrase id="2"><text>u</text></phrase></phrase_list></dialog></game_dialogs>';
  const d = parseDialogueXml(x, DOMParser)[0];
  assert.equal(d.nodes[1].id, 'd__phrase_0__0');
  assert.ok(d.edges.some((e) => e.target === 'd__phrase_2__0'));
});

test('dialogue phrase sound imports as voice_id', () => {
  const x = '<game_dialogs><dialog id="d"><phrase_list><phrase id="0"><text>st_0</text><sound>characters\\wolf\\wolf_0.ogg</sound></phrase></phrase_list></dialog></game_dialogs>';
  const d = parseDialogueXml(x, DOMParser)[0];
  assert.equal(d.nodes[1].data.voice_id, 'characters\\wolf\\wolf_0.ogg');
});

test('xml entities and characters metadata', () => {
  assert.equal(escapeText('<&>'), '&lt;&amp;&gt;');
  assert.equal(escapeAttribute('"' + "'"), '&quot;&apos;');
  const c = parseCharactersXml('<r><specific_character id="x"><start_dialog>a</start_dialog><start_dialog>b</start_dialog></specific_character></r>', DOMParser)[0];
  assert.deepEqual(c.start_dialog, ['a', 'b']);
});

test('character fields parsed from child elements', () => {
  const xml = '<r><specific_character id="esc_wolf" team="stalker"><name>Волк</name><community>stalker</community><rank>100</rank><reputation>50</reputation><bio><text>Опытный сталкер</text></bio><visual>actors\\stalker_2\\stalker_2</visual><icon>ui_npc_u_wolf</icon><start_dialog>esc_wolf_start</start_dialog></specific_character></r>';
  const c = parseCharactersXml(xml, DOMParser)[0];
  assert.equal(c.id, 'esc_wolf');
  assert.equal(c.name, 'Волк');
  assert.equal(c.community, 'stalker');
  assert.equal(c.rank, '100');
  assert.equal(c.reputation, '50');
  assert.equal(c.bio, 'Опытный сталкер');
  assert.equal(c.visual, 'actors\\stalker_2\\stalker_2');
  assert.equal(c.icon, 'ui_npc_u_wolf');
  assert.deepEqual(c.start_dialog, ['esc_wolf_start']);
});

test('migration maps legacy node types and rejects non-object', () => {
  const q = migrateProject({ nodes: [{ id: 'x', type: 'NPC_REPLY', x: 0, y: 0, data: { speaker: 'foo', role: 'npc' } }] });
  assert.equal(q.nodes[0].type, 'PHRASE');
  assert.equal(q.nodes[0].data.role, 'npc');
  assert.equal('speaker' in q.nodes[0].data, false);
  assert.throws(() => migrateProject(null));
  assert.throws(() => migrateProject([]));
});

test('cp1251 declaration-compatible bytes', () => {
  const a = cp1251Encode('Привет');
  assert.equal(decodeGameBytes(a.buffer, 'windows-1251'), 'Привет');
});

test('large cycle validation iterative', () => {
  const n = [{ id: 's', type: 'START' }];
  const e = [];
  for (let i = 0; i < 10000; i++) {
    n.push({ id: '' + i, type: 'NPC_REPLY' });
    e.push({ source: i ? ' ' + (i - 1) : 's', target: '' + i });
  }
  assert.ok(Array.isArray(validateGraph(n, e)));

test('validateCharacter flags missing fields and unknown community', () => {
  const issues = validateCharacter({ visual: '', icon: 'ui_npc_x', community: 'nope' }, ['stalker', 'bandit'], { ui_npc_known: 1 });
  assert.ok(issues.some((i) => i.indexOf('visual') !== -1));
  assert.ok(issues.some((i) => i.indexOf('группировка') !== -1));
  assert.ok(issues.some((i) => i.indexOf('портрет') !== -1));
  assert.equal(validateCharacter({ visual: 'v', icon: 'i', community: 'stalker' }, ['stalker'], {}).length, 0);
});

});
