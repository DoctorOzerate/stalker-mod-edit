// Простейшая система плагинов: пользовательские экспортёры.
// Плагин — { id, name, code }, где code — тело функции
//   function(project, helpers) { return [{ name, text }]; }
// Возвращаемые файлы скачиваются как текстовые.
const PLUGINS_KEY = 'xray_forge_plugins_v1';

export function loadPlugins() {
  try {
    const v = JSON.parse(localStorage.getItem(PLUGINS_KEY));
    return Array.isArray(v) ? v.filter(function (p) { return p && typeof p === 'object' && p.id && p.name; }) : [];
  } catch (e) { return []; }
}
export function savePlugins(list) {
  try { localStorage.setItem(PLUGINS_KEY, JSON.stringify(list || [])); } catch (e) {}
}
// Выполнить плагин-экспортёр. code — тело функции, получающей (project, helpers).
export function runPluginExporter(plugin, project) {
  try {
    const fn = new Function('project', 'helpers', 'return (' + String(plugin.code || '') + ')(project, helpers);');
    const out = fn(project, { JSON: JSON, String: String });
    if (!Array.isArray(out)) return [];
    return out.filter(function (f) { return f && typeof f === 'object' && f.name; }).map(function (f) { return { name: String(f.name), text: String(f.text == null ? '' : f.text) }; });
  } catch (e) {
    throw new Error('Плагин "' + plugin.name + '": ' + e.message);
  }
}
