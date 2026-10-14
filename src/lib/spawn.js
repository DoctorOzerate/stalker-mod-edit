// Разбор бинарного all.spawn (#10).
// Полный структурный парсинг всех CSE_* (SPAWN_VERSION=128, десятки классов) — огромный объём;
// здесь — надёжная основа: заголовок + извлечение читаемых строк (секции спавна, имена, уровни).

export function parseSpawnHeader(bytes) {
  if (!bytes || bytes.byteLength < 12) return null;
  const dv = new DataView(bytes.buffer, bytes.byteOffset || 0, bytes.byteLength);
  return {
    version: dv.getUint32(0, true),
    spawnCount: dv.getUint32(4, true),
    levelCount: dv.getUint32(8, true)
  };
}

export function readCString(bytes, offset) {
  if (offset == null || offset >= bytes.byteLength) return { text: '', next: bytes.byteLength };
  let end = offset;
  while (end < bytes.byteLength && bytes[end] !== 0) end++;
  return { text: new TextDecoder('windows-1251').decode(bytes.slice(offset, end)), next: end + 1 };
}

// Извлечь все печатные ASCII-строки (>= minLen символов) — для просмотра содержимого спавна.
export function extractSpawnStrings(bytes, minLen) {
  const min = minLen || 3;
  const out = [];
  let start = -1;
  for (let i = 0; i <= bytes.byteLength; i++) {
    const b = i < bytes.byteLength ? bytes[i] : 0;
    const printable = b >= 32 && b < 127;
    if (printable && start === -1) start = i;
    else if (!printable && start !== -1) {
      if (i - start >= min) out.push({ offset: start, text: new TextDecoder('windows-1251').decode(bytes.slice(start, i)) });
      start = -1;
    }
  }
  return out;
}

// Эвристика: вероятные секции спавна — короткие строки из [a-z0-9_].
export function analyzeSpawn(bytes) {
  const header = parseSpawnHeader(bytes);
  const strings = extractSpawnStrings(bytes, 3);
  const sections = [];
  const seen = new Set();
  strings.forEach(function (s) {
    if (/^[a-z0-9_]+$/.test(s.text) && s.text.length >= 3 && s.text.length <= 40 && !seen.has(s.text)) {
      seen.add(s.text);
      sections.push(s.text);
    }
  });
  return { header: header, strings: strings, sections: sections };
}
