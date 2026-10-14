// Декодирование и кодирование DDS-текстур (DXT1/DXT3/DXT5 и несжатые).
// Самодостаточный модуль: не зависит от остального кода.

export function rgb565(v) {
  return [
    Math.round(((v >> 11) & 31) * 255 / 31),
    Math.round(((v >> 5) & 63) * 255 / 63),
    Math.round((v & 31) * 255 / 31),
    255
  ];
}

export function maskInfo(mask) {
  if (!mask) return { shift: 0, bits: 0 };
  let shift = 0; while (((mask >> shift) & 1) === 0) shift++;
  let m = mask >> shift; let bits = 0;
  while (m & 1) { bits++; m >>= 1; }
  return { shift: shift, bits: bits };
}

export function decodeDxt1(dv, off, w, h) {
  const out = new Uint8Array(w * h * 4);
  for (let by = 0; by < h; by += 4) for (let bx = 0; bx < w; bx += 4) {
    const c0 = dv.getUint16(off, true); const c1 = dv.getUint16(off + 2, true);
    const bits = dv.getUint32(off + 4, true); off += 8;
    const col0 = rgb565(c0), col1 = rgb565(c1);
    const cols = [col0, col1, [0, 0, 0, 255], [255, 255, 255, 255]];
    if (c0 > c1) {
      cols[2] = [Math.round((2 * col0[0] + col1[0]) / 3), Math.round((2 * col0[1] + col1[1]) / 3), Math.round((2 * col0[2] + col1[2]) / 3), 255];
      cols[3] = [Math.round((col0[0] + 2 * col1[0]) / 3), Math.round((col0[1] + 2 * col1[1]) / 3), Math.round((col0[2] + 2 * col1[2]) / 3), 255];
    } else {
      cols[2] = [Math.round((col0[0] + col1[0]) / 2), Math.round((col0[1] + col1[1]) / 2), Math.round((col0[2] + col1[2]) / 2), 255];
      cols[3] = [0, 0, 0, 0];
    }
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) {
      const px = bx + x, py = by + y;
      if (px >= w || py >= h) continue;
      const idx = (bits >> (2 * (y * 4 + x))) & 3;
      const o = (py * w + px) * 4;
      out[o] = cols[idx][0]; out[o + 1] = cols[idx][1]; out[o + 2] = cols[idx][2]; out[o + 3] = cols[idx][3];
    }
  }
  return out;
}

export function decodeDxt3(dv, off, w, h) {
  const out = new Uint8Array(w * h * 4);
  for (let by = 0; by < h; by += 4) for (let bx = 0; bx < w; bx += 4) {
    const alpha = new Uint8Array(8);
    for (let i = 0; i < 8; i++) alpha[i] = dv.getUint8(off + i);
    const c0 = dv.getUint16(off + 8, true); const c1 = dv.getUint16(off + 10, true);
    const bits = dv.getUint32(off + 12, true); off += 16;
    const col0 = rgb565(c0), col1 = rgb565(c1);
    const cols = [col0, col1,
      [Math.round((2 * col0[0] + col1[0]) / 3), Math.round((2 * col0[1] + col1[1]) / 3), Math.round((2 * col0[2] + col1[2]) / 3), 255],
      [Math.round((col0[0] + 2 * col1[0]) / 3), Math.round((col0[1] + 2 * col1[1]) / 3), Math.round((col0[2] + 2 * col1[2]) / 3), 255]
    ];
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) {
      const px = bx + x, py = by + y;
      if (px >= w || py >= h) continue;
      const i = y * 4 + x;
      const nibble = (alpha[i >> 1] >> ((i & 1) * 4)) & 15;
      const idx = (bits >> (2 * i)) & 3;
      const o = (py * w + px) * 4;
      out[o] = cols[idx][0]; out[o + 1] = cols[idx][1]; out[o + 2] = cols[idx][2];
      out[o + 3] = Math.round(nibble * 255 / 15);
    }
  }
  return out;
}

export function decodeDxt5(dv, off, w, h) {
  const out = new Uint8Array(w * h * 4);
  for (let by = 0; by < h; by += 4) for (let bx = 0; bx < w; bx += 4) {
    const a0 = dv.getUint8(off); const a1 = dv.getUint8(off + 1);
    let alphaBits = 0n;
    for (let i = 0; i < 6; i++) alphaBits = (alphaBits << 8n) | BigInt(dv.getUint8(off + 2 + i));
    const alphaVals = [a0, a1];
    if (a0 > a1) { for (let i = 1; i <= 6; i++) alphaVals.push(Math.round(a0 + (a1 - a0) * i / 7)); }
    else { for (let i = 1; i <= 4; i++) alphaVals.push(Math.round(a0 + (a1 - a0) * i / 5)); alphaVals.push(0, 255); }
    const c0 = dv.getUint16(off + 8, true); const c1 = dv.getUint16(off + 10, true);
    const bits = dv.getUint32(off + 12, true); off += 16;
    const col0 = rgb565(c0), col1 = rgb565(c1);
    const cols = [col0, col1,
      [Math.round((2 * col0[0] + col1[0]) / 3), Math.round((2 * col0[1] + col1[1]) / 3), Math.round((2 * col0[2] + col1[2]) / 3), 255],
      [Math.round((col0[0] + 2 * col1[0]) / 3), Math.round((col0[1] + 2 * col1[1]) / 3), Math.round((col0[2] + 2 * col1[2]) / 3), 255]
    ];
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) {
      const px = bx + x, py = by + y;
      if (px >= w || py >= h) continue;
      const i = y * 4 + x;
      const ai = Number((alphaBits >> BigInt(45 - i * 3)) & 7n);
      const idx = (bits >> (2 * i)) & 3;
      const o = (py * w + px) * 4;
      out[o] = cols[idx][0]; out[o + 1] = cols[idx][1]; out[o + 2] = cols[idx][2];
      out[o + 3] = alphaVals[ai] == null ? 255 : alphaVals[ai];
    }
  }
  return out;
}

export function decodeUncompressed(dv, off, w, h, bitCount, rMask, gMask, bMask, aMask) {
  const out = new Uint8Array(w * h * 4);
  const ri = maskInfo(rMask), gi = maskInfo(gMask), bi = maskInfo(bMask), ai = maskInfo(aMask);
  const scale = function (v, info) { if (!info.bits) return 255; if (info.bits === 8) return v; return Math.round(v * 255 / ((1 << info.bits) - 1)); };
  if (bitCount === 32) {
    for (let i = 0; i < w * h; i++) {
      const c = dv.getUint32(off + i * 4, true);
      out[i * 4] = scale((c & rMask) >> ri.shift, ri);
      out[i * 4 + 1] = scale((c & gMask) >> gi.shift, gi);
      out[i * 4 + 2] = scale((c & bMask) >> bi.shift, bi);
      out[i * 4 + 3] = scale((c & aMask) >> ai.shift, ai);
    }
    return out;
  }
  if (bitCount === 24) {
    for (let i = 0; i < w * h; i++) {
      const o = off + i * 3;
      const c = dv.getUint8(o) | (dv.getUint8(o + 1) << 8) | (dv.getUint8(o + 2) << 16);
      out[i * 4] = scale((c & rMask) >> ri.shift, ri);
      out[i * 4 + 1] = scale((c & gMask) >> gi.shift, gi);
      out[i * 4 + 2] = scale((c & bMask) >> bi.shift, bi);
      out[i * 4 + 3] = 255;
    }
    return out;
  }
  throw new Error('Неподдерживаемая битность: ' + bitCount);
}

function decodeBc4Values(dv, off) {
  const a0 = dv.getUint8(off), a1 = dv.getUint8(off + 1);
  let bits = 0n;
  for (let i = 0; i < 6; i++) bits = (bits << 8n) | BigInt(dv.getUint8(off + 2 + i));
  const vals = [a0, a1];
  if (a0 > a1) { for (let i = 1; i <= 6; i++) vals.push(Math.round(a0 + (a1 - a0) * i / 7)); }
  else { for (let i = 1; i <= 4; i++) vals.push(Math.round(a0 + (a1 - a0) * i / 5)); vals.push(0, 255); }
  const out = new Uint8Array(16);
  for (let i = 0; i < 16; i++) {
    const idx = Number((bits >> BigInt(45 - i * 3)) & 7n);
    out[i] = vals[idx] == null ? 255 : vals[idx];
  }
  return out;
}

export function decodeBc4(dv, off, w, h) {
  const out = new Uint8Array(w * h * 4);
  for (let by = 0; by < h; by += 4) for (let bx = 0; bx < w; bx += 4) {
    const vals = decodeBc4Values(dv, off); off += 8;
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) {
      const px = bx + x, py = by + y; if (px >= w || py >= h) continue;
      const v = vals[y * 4 + x], o = (py * w + px) * 4;
      out[o] = v; out[o + 1] = v; out[o + 2] = v; out[o + 3] = 255;
    }
  }
  return out;
}

export function decodeBc5(dv, off, w, h) {
  const out = new Uint8Array(w * h * 4);
  for (let by = 0; by < h; by += 4) for (let bx = 0; bx < w; bx += 4) {
    const r = decodeBc4Values(dv, off); off += 8;
    const g = decodeBc4Values(dv, off); off += 8;
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) {
      const px = bx + x, py = by + y; if (px >= w || py >= h) continue;
      const o = (py * w + px) * 4;
      out[o] = r[y * 4 + x]; out[o + 1] = g[y * 4 + x]; out[o + 2] = 255; out[o + 3] = 255;
    }
  }
  return out;
}

export function decodeDDS(buffer) {
  if (buffer.byteLength < 128) throw new Error('Файл слишком мал для DDS.');
  const dv = new DataView(buffer);
  if (dv.getUint32(0, true) !== 0x20534444) throw new Error('Неверная сигнатура DDS.');
  const height = dv.getUint32(12, true);
  const width = dv.getUint32(16, true);
  if (width < 1 || height < 1 || width > 8192 || height > 8192) throw new Error('Некорректные размеры DDS: ' + width + 'x' + height);
  const pfFlags = dv.getUint32(80, true);
  const fourCC = dv.getUint32(84, true);
  const rgbBitCount = dv.getUint32(88, true);
  const rMask = dv.getUint32(92, true);
  const gMask = dv.getUint32(96, true);
  const bMask = dv.getUint32(100, true);
  const aMask = dv.getUint32(104, true);
  const fourCCStr = String.fromCharCode(fourCC & 255, (fourCC >> 8) & 255, (fourCC >> 16) & 255, (fourCC >> 24) & 255);
  const blocks = Math.max(1, Math.ceil(width / 4)) * Math.max(1, Math.ceil(height / 4));
  let pixels;
  if (fourCCStr === 'DXT1') { if (buffer.byteLength < 128 + blocks * 8) throw new Error('Обрезанный DXT1.'); pixels = decodeDxt1(dv, 128, width, height); }
  else if (fourCCStr === 'DXT3') { if (buffer.byteLength < 128 + blocks * 16) throw new Error('Обрезанный DXT3.'); pixels = decodeDxt3(dv, 128, width, height); }
  else if (fourCCStr === 'DXT5') { if (buffer.byteLength < 128 + blocks * 16) throw new Error('Обрезанный DXT5.'); pixels = decodeDxt5(dv, 128, width, height); }
  else if (fourCCStr === 'BC4U' || fourCCStr === 'BC4S' || fourCCStr === 'ATI1') { if (buffer.byteLength < 128 + blocks * 8) throw new Error('Обрезанный BC4.'); pixels = decodeBc4(dv, 128, width, height); }
  else if (fourCCStr === 'BC5U' || fourCCStr === 'BC5S' || fourCCStr === 'ATI2') { if (buffer.byteLength < 128 + blocks * 16) throw new Error('Обрезанный BC5.'); pixels = decodeBc5(dv, 128, width, height); }
  else if (pfFlags & 0x40) { pixels = decodeUncompressed(dv, 128, width, height, rgbBitCount, rMask, gMask, bMask, aMask); }
  else throw new Error('Неподдерживаемый формат DDS: ' + (fourCCStr || 'RGBA без масок'));
  return { width: width, height: height, pixels: pixels };
}

export function pixelsToDataURL(pixels, width, height) {
  const canvas = document.createElement('canvas');
  canvas.width = width; canvas.height = height;
  const ctx = canvas.getContext('2d');
  const img = ctx.createImageData(width, height);
  img.data.set(pixels);
  ctx.putImageData(img, 0, 0);
  return canvas.toDataURL('image/png');
}

export function rgbTo565(r, g, b) { return ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3); }
export function encodeDxtColorBlock(block, fourColor) {
  let minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0;
  for (let i = 0; i < 16; i++) { const r = block[i * 4], g = block[i * 4 + 1], b = block[i * 4 + 2]; if (r < minR) minR = r; if (g < minG) minG = g; if (b < minB) minB = b; if (r > maxR) maxR = r; if (g > maxG) maxG = g; if (b > maxB) maxB = b; }
  let c0 = rgbTo565(maxR, maxG, maxB), c1 = rgbTo565(minR, minG, minB);
  if (fourColor && c0 <= c1) { const t = c0; c0 = c1; c1 = t; const tr = maxR, tg = maxG, tb = maxB; maxR = minR; maxG = minG; maxB = minB; minR = tr; minG = tg; minB = tb; }
  const cols = [[maxR, maxG, maxB], [minR, minG, minB]];
  if (c0 > c1) {
    cols.push([Math.floor((2 * maxR + minR) / 3), Math.floor((2 * maxG + minG) / 3), Math.floor((2 * maxB + minB) / 3)]);
    cols.push([Math.floor((maxR + 2 * minR) / 3), Math.floor((maxG + 2 * minG) / 3), Math.floor((maxB + 2 * minB) / 3)]);
  } else {
    cols.push([Math.floor((maxR + minR) / 2), Math.floor((maxG + minG) / 2), Math.floor((maxB + minB) / 2)]);
    cols.push([0, 0, 0]);
  }
  let ind = 0;
  for (let i = 0; i < 16; i++) { const r = block[i * 4], g = block[i * 4 + 1], b = block[i * 4 + 2]; let best = 0, bd = 1e9; for (let c = 0; c < 4; c++) { const dr = cols[c][0] - r, dg = cols[c][1] - g, db = cols[c][2] - b; const d = dr * dr + dg * dg + db * db; if (d < bd) { bd = d; best = c; } } ind |= (best << (i * 2)); }
  return [c0 & 255, (c0 >>> 8) & 255, c1 & 255, (c1 >>> 8) & 255, ind & 255, (ind >>> 8) & 255, (ind >>> 16) & 255, (ind >>> 24) & 255];
}
export function sampleBlock(pixels, w, h, bx, by) {
  const block = [];
  for (let py = 0; py < 4; py++) for (let px = 0; px < 4; px++) {
    const x = Math.min(bx * 4 + px, w - 1), y = Math.min(by * 4 + py, h - 1), o = (y * w + x) * 4;
    block.push(pixels[o], pixels[o + 1], pixels[o + 2], pixels[o + 3]);
  }
  return block;
}
export function encodeDxt1(pixels, w, h) {
  const out = [];
  const bw = Math.max(1, Math.ceil(w / 4)), bh = Math.max(1, Math.ceil(h / 4));
  for (let by = 0; by < bh; by++) for (let bx = 0; bx < bw; bx++) out.push.apply(out, encodeDxtColorBlock(sampleBlock(pixels, w, h, bx, by), false));
  return out;
}
export function encodeDxt3(pixels, w, h) {
  const out = [];
  const bw = Math.max(1, Math.ceil(w / 4)), bh = Math.max(1, Math.ceil(h / 4));
  for (let by = 0; by < bh; by++) for (let bx = 0; bx < bw; bx++) {
    const block = sampleBlock(pixels, w, h, bx, by);
    for (let i = 0; i < 16; i += 2) {
      const a0 = (block[i * 4 + 3] >> 4) & 0xF;
      const a1 = (block[(i + 1) * 4 + 3] >> 4) & 0xF;
      out.push((a0 & 0xF) | ((a1 & 0xF) << 4));
    }
    out.push.apply(out, encodeDxtColorBlock(block, true));
  }
  return out;
}
export function encodeDxt5(pixels, w, h) {
  const out = [];
  const bw = Math.max(1, Math.ceil(w / 4)), bh = Math.max(1, Math.ceil(h / 4));
  for (let by = 0; by < bh; by++) for (let bx = 0; bx < bw; bx++) {
    const block = sampleBlock(pixels, w, h, bx, by);
    let minA = 255, maxA = 0;
    for (let i = 0; i < 16; i++) { const a = block[i * 4 + 3]; if (a < minA) minA = a; if (a > maxA) maxA = a; }
    let a0 = maxA, a1 = minA;
    if (a0 <= a1) { a0 = 255; a1 = 0; }
    const alphas = [a0, a1];
    if (a0 > a1) {
      alphas.push(Math.floor((6 * a0 + 1 * a1) / 7), Math.floor((5 * a0 + 2 * a1) / 7), Math.floor((4 * a0 + 3 * a1) / 7), Math.floor((3 * a0 + 4 * a1) / 7), Math.floor((2 * a0 + 5 * a1) / 7), Math.floor((1 * a0 + 6 * a1) / 7));
    } else {
      alphas.push(Math.floor((4 * a0 + 1 * a1) / 5), Math.floor((3 * a0 + 2 * a1) / 5), Math.floor((2 * a0 + 3 * a1) / 5), Math.floor((1 * a0 + 4 * a1) / 5), 0, 255);
    }
    let alphaInd = 0;
    for (let i = 0; i < 16; i++) {
      const a = block[i * 4 + 3];
      let best = 0, bd = 1e9;
      for (let c = 0; c < 8; c++) { const d = Math.abs(alphas[c] - a); if (d < bd) { bd = d; best = c; } }
      alphaInd |= (best << (i * 3));
    }
    out.push(a0, a1);
    for (let b = 0; b < 6; b++) out.push(Math.floor(alphaInd / Math.pow(2, b * 8)) & 255);
    out.push.apply(out, encodeDxtColorBlock(block, true));
  }
  return out;
}
export function downsampleRgba(pixels, w, h) {
  const nw = Math.max(1, Math.floor(w / 2)), nh = Math.max(1, Math.floor(h / 2));
  const out = new Uint8Array(nw * nh * 4);
  for (let y = 0; y < nh; y++) for (let x = 0; x < nw; x++) {
    let r = 0, g = 0, b = 0, a = 0, n = 0;
    for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 2; dx++) {
      const sx = Math.min(x * 2 + dx, w - 1), sy = Math.min(y * 2 + dy, h - 1);
      const o = (sy * w + sx) * 4;
      r += pixels[o]; g += pixels[o + 1]; b += pixels[o + 2]; a += pixels[o + 3]; n++;
    }
    const o = (y * nw + x) * 4;
    out[o] = Math.round(r / n); out[o + 1] = Math.round(g / n); out[o + 2] = Math.round(b / n); out[o + 3] = Math.round(a / n);
  }
  return out;
}

export function buildMipChain(pixels, w, h) {
  const levels = [{ pixels: pixels, w: w, h: h }];
  let cw = w, ch = h, cur = pixels;
  while (cw > 1 || ch > 1) {
    cur = downsampleRgba(cur, cw, ch);
    cw = Math.max(1, Math.floor(cw / 2)); ch = Math.max(1, Math.floor(ch / 2));
    levels.push({ pixels: cur, w: cw, h: ch });
  }
  return levels;
}

export function encodeDds(pixels, width, height, format, mipmaps) {
  const fmt = format === 'DXT3' ? 0x33545844 : format === 'DXT5' ? 0x35545844 : 0x31545844;
  const encode = function (p, w, h) { return fmt === 0x31545844 ? encodeDxt1(p, w, h) : (fmt === 0x33545844 ? encodeDxt3(p, w, h) : encodeDxt5(p, w, h)); };
  const levels = mipmaps ? buildMipChain(pixels, width, height) : [{ pixels: pixels, w: width, h: height }];
  const payloads = levels.map(function (l) { return encode(l.pixels, l.w, l.h); });
  const topSize = payloads[0].length;
  let total = 0; payloads.forEach(function (p) { total += p.length; });
  const header = new Uint8Array(128);
  const dv = new DataView(header.buffer);
  dv.setUint32(0, 0x20534444, true);
  dv.setUint32(4, 124, true);
  let flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000;
  if (mipmaps) flags |= 0x20000; // DDSD_MIPMAPCOUNT
  dv.setUint32(8, flags, true);
  dv.setUint32(12, height, true);
  dv.setUint32(16, width, true);
  dv.setUint32(20, topSize, true);
  dv.setUint32(28, levels.length, true);
  dv.setUint32(76, 32, true);
  dv.setUint32(80, 0x4, true);
  dv.setUint32(84, fmt, true);
  dv.setUint32(108, 0x1000, true);
  const out = new Uint8Array(128 + total);
  out.set(header, 0);
  let off = 128;
  payloads.forEach(function (p) { out.set(p, off); off += p.length; });
  return out;
}
