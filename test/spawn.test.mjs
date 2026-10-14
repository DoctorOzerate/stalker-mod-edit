import test from 'node:test';
import assert from 'node:assert/strict';
import { parseSpawnHeader, readCString, extractSpawnStrings, analyzeSpawn } from '../src/lib/spawn.js';

test('parseSpawnHeader reads version/count/levels', () => {
  const buf = new Uint8Array(12);
  const dv = new DataView(buf.buffer);
  dv.setUint32(0, 0, true);
  dv.setUint32(4, 44, true);
  dv.setUint32(8, 8, true);
  const h = parseSpawnHeader(buf);
  assert.deepEqual(h, { version: 0, spawnCount: 44, levelCount: 8 });
});

test('parseSpawnHeader rejects short buffer', () => {
  assert.equal(parseSpawnHeader(new Uint8Array(4)), null);
});

test('readCString reads null-terminated string', () => {
  const b = new Uint8Array([0x61, 0x63, 0x74, 0x6F, 0x72, 0, 0x78, 0x79]);
  const r = readCString(b, 0);
  assert.equal(r.text, 'actor');
  assert.equal(r.next, 6);
});

test('extractSpawnStrings finds printable runs', () => {
  const b = new Uint8Array([0, 0, 0x61, 0x63, 0x74, 0x6F, 0x72, 0, 0x73, 0x74, 0x61, 0x6C, 0x6B, 0x65, 0x72, 0]);
  const s = extractSpawnStrings(b, 3);
  assert.ok(s.some((x) => x.text === 'actor'));
  assert.ok(s.some((x) => x.text === 'stalker'));
});

test('analyzeSpawn extracts sections', () => {
  const text = new TextEncoder().encode('\0actor\0stalker\0wpn_ak74\0');
  const b = new Uint8Array(12 + text.length);
  const dv = new DataView(b.buffer);
  dv.setUint32(0, 0, true); dv.setUint32(4, 2, true); dv.setUint32(8, 1, true);
  b.set(text, 12);
  const a = analyzeSpawn(b);
  assert.equal(a.header.spawnCount, 2);
  assert.ok(a.sections.indexOf('actor') !== -1);
  assert.ok(a.sections.indexOf('wpn_ak74') !== -1);
});
