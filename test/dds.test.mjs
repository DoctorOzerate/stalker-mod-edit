import test from 'node:test';
import assert from 'node:assert/strict';
import { decodeBc4, decodeBc5, downsampleRgba, encodeDds } from '../src/lib/dds.js';

test('decodeBc4 decodes white block', () => {
  const buf = new Uint8Array([255, 0, 0, 0, 0, 0, 0, 0]);
  const dv = new DataView(buf.buffer);
  const px = decodeBc4(dv, 0, 4, 4);
  assert.equal(px.length, 16 * 4);
  assert.equal(px[0], 255);
  assert.equal(px[3], 255);
});

test('decodeBc5 decodes red and green channels', () => {
  const buf = new Uint8Array(16);
  buf[0] = 255; buf[1] = 0; // red: a0=255
  buf[8] = 0; buf[9] = 0;   // green: a0=0
  const dv = new DataView(buf.buffer);
  const px = decodeBc5(dv, 0, 4, 4);
  assert.equal(px[0], 255); // R
  assert.equal(px[1], 0);   // G
  assert.equal(px[2], 255); // B
  assert.equal(px[3], 255); // A
});

test('downsampleRgba averages 2x2 block', () => {
  const px = new Uint8Array([255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255]);
  const out = downsampleRgba(px, 2, 2);
  assert.equal(out.length, 4);
  assert.equal(out[0], 128); // (255+0+0+255)/4 = 127.5 → 128
  assert.equal(out[1], 128);
  assert.equal(out[3], 255);
});

test('encodeDds with mipmaps writes mip chain and flags', () => {
  const w = 8, h = 8;
  const px = new Uint8Array(w * h * 4);
  for (let i = 0; i < px.length; i += 4) { px[i] = 255; px[i + 3] = 255; }
  const out = encodeDds(px, w, h, 'DXT1', true);
  const dv = new DataView(out.buffer);
  assert.equal(dv.getUint32(0, true), 0x20534444);
  assert.ok((dv.getUint32(8, true) & 0x20000) !== 0);
  assert.equal(dv.getUint32(28, true), 4); // 8,4,2,1
  assert.equal(out.length, 128 + 32 + 8 + 8 + 8);
});

test('encodeDds without mipmaps keeps single level', () => {
  const out = encodeDds(new Uint8Array(16 * 4), 4, 4, 'DXT1', false);
  const dv = new DataView(out.buffer);
  assert.equal(dv.getUint32(28, true), 1);
  assert.equal(out.length, 128 + 8);
});
