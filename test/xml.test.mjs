import test from 'node:test';
import assert from 'node:assert/strict';
import { DOMParser } from '@xmldom/xmldom';
import { parseCharacterDescGeneral, characterDescGeneralToXml, parseStringTableXml, parseInfoPortions, parseEncyclopedia, parseNpcProfile } from '../src/lib/xml.js';

test('parseCharacterDescGeneral reads communities and relations', () => {
  globalThis.DOMParser = DOMParser;
  const xml = '<character_desc_general><communities><community id="stalker"><name>st_stalker</name><icon>ui_inGame2_Stalker</icon><relation community="bandit">-100</relation></community><community id="bandit"/></communities></character_desc_general>';
  const r = parseCharacterDescGeneral(xml);
  assert.equal(r.communities.length, 2);
  assert.equal(r.communities[0].name, 'st_stalker');
  assert.equal(r.communities[0].relations[0].community, 'bandit');
  assert.equal(r.communities[0].relations[0].value, '-100');
});

test('characterDescGeneralToXml round-trips', () => {
  globalThis.DOMParser = DOMParser;
  const data = { communities: [{ id: 'a', name: 'na', icon: 'ic', relations: [{ community: 'b', value: '1' }] }] };
  const xml = characterDescGeneralToXml(data);
  assert.ok(xml.indexOf('<community id="a">') !== -1);
  assert.ok(xml.indexOf('<relation community="b">1</relation>') !== -1);
  const r = parseCharacterDescGeneral(xml);
  assert.equal(r.communities[0].relations[0].value, '1');
});

test('parseStringTableXml handles fragment without string_table root', () => {
  globalThis.DOMParser = DOMParser;
  const lang = parseStringTableXml('<string id="a"><text>Привет</text></string><string id="b"><text>Пока</text></string>', 'gamedata/config/text/rus/stable.xml');
  assert.equal(lang.locale, 'rus');
  assert.equal(lang.entries.length, 2);
  assert.equal(lang.entries[0].text, 'Привет');
});

test('parseStringTableXml returns null for non-string-table xml', () => {
  globalThis.DOMParser = DOMParser;
  assert.equal(parseStringTableXml('<xml><specific_character id="x"><name>n</name></specific_character></xml>', 'gamedata/config/gameplay/character_desc.xml'), null);
});

test('parseInfoPortions reads info portions', () => {
  globalThis.DOMParser = DOMParser;
  const xml = '<game_information_portions><info_portion id="p1"><task>t1</task><dialog>d1</dialog><disable>p2</disable></info_portion></game_information_portions>';
  const r = parseInfoPortions(xml);
  assert.equal(r.length, 1);
  assert.equal(r[0].tasks[0], 't1');
  assert.equal(r[0].dialogs[0], 'd1');
  assert.equal(r[0].disables[0], 'p2');
});

test('parseEncyclopedia reads articles', () => {
  globalThis.DOMParser = DOMParser;
  const xml = '<encyclopedia><article id="a1" name="Bread" group="Equipment/Food"><ltx>bread</ltx><text>enc_equipment_food_bread1</text></article></encyclopedia>';
  const r = parseEncyclopedia(xml);
  assert.equal(r.length, 1);
  assert.equal(r[0].name, 'Bread');
  assert.equal(r[0].ltx, 'bread');
});

test('parseNpcProfile reads characters', () => {
  globalThis.DOMParser = DOMParser;
  const xml = '<xml><character id="escape_trader"><class>Trader</class><specific_character>escape_trader</specific_character></character><character id="default"><class>Stalker_general</class></character></xml>';
  const r = parseNpcProfile(xml);
  assert.equal(r.length, 2);
  assert.equal(r[0].class, 'Trader');
  assert.equal(r[0].specific_character, 'escape_trader');
  assert.equal(r[1].specific_character, '');
});
