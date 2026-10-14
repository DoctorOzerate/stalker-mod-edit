// Общая история отмены/повтора (undo/redo) для любого домена.
// Хранит снапшоты с курсором и обрезает их лимитом. Чистые функции: состояние -> новое состояние.
export function createHistory(limit) {
  const cap = limit || 50;
  return {
    // Добавить снапшот; всё после текущей позиции отбрасывается (новый виток истории).
    push: function (state, snapshot) {
      const hist = state.hist.slice(0, state.pos + 1).concat([snapshot]).slice(-cap);
      return { hist: hist, pos: hist.length - 1 };
    },
    // Сдвинуть курсор на delta, вызвать apply(snapshot) для побочного эффекта и вернуть
    // новое состояние. Если сдвиг выходит за границы — вернуть исходное состояние без apply.
    move: function (state, delta, apply) {
      const pos = state.pos + delta;
      if (pos < 0 || pos >= state.hist.length) return state;
      const snapshot = state.hist[pos];
      if (apply) apply(snapshot);
      return { hist: state.hist, pos: pos };
    },
  };
}
