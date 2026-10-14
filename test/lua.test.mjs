import test from 'node:test';
import assert from 'node:assert/strict';
import { luaCheck, luaUndeclaredGlobals, LUA_SNIPPETS, LUA_API, luaCompletions, luaCallGraph, luaUnused } from '../src/lib/lua.js';

test('luaCheck detects unclosed block and extra end', () => {
  assert.equal(luaCheck('function f()\n').length, 1);
  assert.equal(luaCheck('end').length, 1);
  assert.deepEqual(luaCheck('function f()\nend'), []);
});

test('luaUndeclaredGlobals flags global assignment but not local', () => {
  const code = 'local x = 1\nx = 2\ny = 3\n';
  const issues = luaUndeclaredGlobals(code);
  assert.ok(issues.some((i) => i.msg.indexOf('y') !== -1));
  assert.ok(!issues.some((i) => i.msg.indexOf('x') !== -1));
});

test('stalker lua snippets and api present', () => {
  assert.ok(LUA_SNIPPETS.length > 0);
  assert.ok(LUA_API.indexOf('db.actor:give_item') !== -1);
});

test('luaCompletions filters by prefix', () => {
  assert.ok(luaCompletions('db.actor').length > 0);
  assert.ok(luaCompletions('').length > 10);
  assert.ok(luaCompletions('zzz_nothing').length === 0);
});

test('luaCallGraph builds call edges', () => {
  const g = luaCallGraph('function a()\n  b()\nend\nfunction b()\nend\n');
  assert.ok(g.functions.indexOf('a') !== -1);
  assert.ok(g.edges.some((e) => e.from === 'a' && e.to === 'b'));
});

test('luaUnused finds uncalled functions', () => {
  assert.deepEqual(luaUnused('function a()\nend\nfunction b()\n  a()\nend\n'), ['b']);
});
