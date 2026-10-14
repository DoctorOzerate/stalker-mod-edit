import test from 'node:test';
import assert from 'node:assert/strict';
import { tr, LOCALES } from '../src/lib/i18n.js';
import { loadPlugins, runPluginExporter } from '../src/lib/plugins.js';

test('i18n translates and falls back to key', () => {
  assert.equal(tr('en', 'save'), 'Save');
  assert.equal(tr('ru', 'save'), 'Сохранить');
  assert.equal(tr('en', 'tabDialogues'), 'Dialogues');
  assert.equal(tr('en', 'unknown_key'), 'unknown_key');
  assert.equal(LOCALES.en, 'English');
});

test('plugins load empty in node and run exporter', () => {
  assert.deepEqual(loadPlugins(), []);
  const files = runPluginExporter(
    { id: 'p', name: 'test', code: 'function(project){ return [{ name: "out.txt", text: project.x }]; }' },
    { x: 'hello' }
  );
  assert.deepEqual(files, [{ name: 'out.txt', text: 'hello' }]);
});
