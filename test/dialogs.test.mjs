import test from 'node:test';
import assert from 'node:assert/strict';
import { dialogueToXml, dialogsLtx, validateTextIds, gameFilesFromDialogues, findTextIdIssues, resolveTextId, textIdCoverage } from '../src/lib/dialogs.js';

function makeDialog(id, data) {
  return {
    id: id,
    nodes: [
      { id: 'start', type: 'START' },
      Object.assign({ id: 'p1', type: 'PHRASE', data: { phraseId: '0', text_id: 'st_1', use_id: true } }, data ? { data: data.data } : {}),
      { id: 'end', type: 'END' }
    ],
    edges: [
      { id: 'e1', source: 'start', target: 'p1' },
      { id: 'e2', source: 'p1', target: 'end' }
    ]
  };
}

test('dialogueToXml round-trips sound and metadata', () => {
  const xml = dialogueToXml(makeDialog('bar', { data: { phraseId: '0', text_id: 'st_1', use_id: true, voice_id: 'chars\\bar\\bar.ogg', metadata: { has_info: ['a'], give_info: ['b'] } } }));
  assert.ok(xml.indexOf('<dialog id="bar">') !== -1);
  assert.ok(xml.indexOf('<sound>chars\\bar\\bar.ogg</sound>') !== -1);
  assert.ok(xml.indexOf('<has_info>a</has_info>') !== -1);
  assert.ok(xml.indexOf('<give_info>b</give_info>') !== -1);
});

test('dialogsLtx lists ids', () => {
  assert.equal(dialogsLtx(['a', 'b']), '[dialogs]\ndialogs = a, b\n');
});

test('validateTextIds reports missing translations', () => {
  const ok = validateTextIds([makeDialog('d')], [{ locale: 'rus', entries: [{ id: 'st_1', text: 'x' }] }]);
  assert.deepEqual(ok, []);
  const bad = validateTextIds([{ id: 'd', nodes: [{ type: 'PHRASE', data: { text_id: 'missing' } }] }], []);
  assert.equal(bad.length, 1);
  assert.equal(bad[0].text_id, 'missing');
});

test('gameFilesFromDialogues builds multi-dialog xml and dialogs.ltx', () => {
  const files = gameFilesFromDialogues(
    [{ fileName: 'bar', dialogues: [makeDialog('bar'), makeDialog('baz')] }],
    'config', 'windows-1251',
    [{ locale: 'rus', name: 'Русский', entries: [{ id: 'st_1', text: 'Привет' }] }],
    'bar_mod'
  );
  assert.ok(files['gamedata/config/gameplay/dialogs_bar.xml']);
  assert.ok(files['gamedata/config/gameplay/dialogs_bar.xml'].indexOf('bar') !== -1);
  assert.ok(files['gamedata/config/gameplay/dialogs.ltx'].indexOf('bar') !== -1);
  assert.ok(files['gamedata/config/gameplay/dialogs.ltx'].indexOf('baz') !== -1);
  assert.ok(files['gamedata/config/text/rus/st_bar_mod.xml']);
});

test('findTextIdIssues reports duplicates, empty, missing', () => {
  const languages = [{ locale: 'rus', entries: [{ id: 'a', text: 'x' }, { id: 'a', text: 'y' }, { id: 'b', text: '' }] }];
  const dialogues = [{ nodes: [{ type: 'PHRASE', data: { text_id: 'a' } }, { type: 'PHRASE', data: { text_id: 'missing' } }] }];
  const issues = findTextIdIssues(languages, dialogues);
  assert.ok(issues.some((i) => i.msg.indexOf('Дубликат') !== -1));
  assert.ok(issues.some((i) => i.msg.indexOf('Пустой') !== -1));
  assert.ok(issues.some((i) => i.msg.indexOf('missing') !== -1));
});

test('resolveTextId finds translation across locales', () => {
  const languages = [{ locale: 'eng', entries: [{ id: 'a', text: '' }] }, { locale: 'rus', entries: [{ id: 'a', text: 'Привет' }] }];
  assert.equal(resolveTextId(languages, 'a').text, 'Привет');
  assert.equal(resolveTextId(languages, 'a').locale, 'rus');
  assert.equal(resolveTextId(languages, 'nope'), null);
});

test('textIdCoverage computes translation coverage', () => {
  const dialogues = [{ nodes: [{ type: 'PHRASE', data: { text_id: 'a' } }, { type: 'PHRASE', data: { text_id: 'b' } }, { type: 'PHRASE', data: { text_id: 'c' } }] }];
  const languages = [{ locale: 'rus', entries: [{ id: 'a', text: 'x' }, { id: 'b', text: 'y' }] }];
  const cov = textIdCoverage(dialogues, languages);
  assert.equal(cov.total, 3);
  assert.equal(cov.translated, 2);
  assert.equal(cov.pct, 67);
});

