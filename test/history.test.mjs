import test from 'node:test';
import assert from 'node:assert/strict';
import { createHistory } from '../src/lib/history.js';

test('history push truncates future and caps limit', () => {
  const h = createHistory(3);
  let s = { hist: [], pos: -1 };
  s = h.push(s, 'a');
  s = h.push(s, 'b');
  s = h.push(s, 'c');
  s = h.push(s, 'd'); // превышает лимит 3
  assert.deepEqual(s.hist, ['b', 'c', 'd']);
  assert.equal(s.pos, 2);
});

test('history move changes cursor and returns unchanged on bounds', () => {
  const h = createHistory(10);
  let s = { hist: [], pos: -1 };
  s = h.push(s, 'a');
  s = h.push(s, 'b');
  const applied = [];
  s = h.move(s, -1, (t) => applied.push(t));
  assert.equal(s.pos, 0);
  assert.deepEqual(applied, ['a']);
  const before = s;
  s = h.move(s, -1, () => { throw new Error('не должен вызываться'); });
  assert.equal(s, before); // за границей — тот же объект, apply не вызван
  s = h.move(s, +1, (t) => applied.push(t));
  assert.equal(s.pos, 1);
  assert.deepEqual(applied, ['a', 'b']);
});

test('history push after undo discards redo branch', () => {
  const h = createHistory(10);
  let s = { hist: [], pos: -1 };
  s = h.push(s, 'a');
  s = h.push(s, 'b');
  s = h.move(s, -1, null); // pos = 0
  s = h.push(s, 'c'); // отбрасывает 'b'
  assert.deepEqual(s.hist, ['a', 'c']);
  assert.equal(s.pos, 1);
});
