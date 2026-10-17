import { h } from "../dom";

const focusable = "button, input, select, textarea, [tabindex]:not([tabindex='-1'])";

export interface DialogOptions {
  top?: HTMLElement;
  onClose?: () => void;
}

export function dialog(title: string, body: HTMLElement, actions: HTMLElement[], options: DialogOptions = {}) {
  const titleId = `t${Math.random().toString(36).slice(2)}`;
  const modal = h("div", { class: "modal", role: "dialog", "aria-modal": "true", "aria-labelledby": titleId },
    h("header", {}, options.top ? h("div", { class: "marcador" }, options.top) : null, h("h2", { id: titleId }, title)),
    h("div", { class: "conteudo" }, body),
    h("footer", {}, h("div", { class: "acoes" }, ...actions)));
  const veu = h("div", { class: "veu" }, modal);
  let previous: HTMLElement | null = null;

  const close = () => {
    if (!veu.isConnected) return;
    veu.remove();
    previous?.focus();
    options.onClose?.();
  };

  veu.addEventListener("keydown", (event) => {
    const e = event as KeyboardEvent;
    if (e.key === "Escape") {
      e.preventDefault();
      close();
    } else if (e.key === "Tab") {
      const items = [...modal.querySelectorAll<HTMLElement>(focusable)].filter((el) => !el.hasAttribute("disabled"));
      if (!items.length) return;
      const first = items[0];
      const last = items[items.length - 1];
      const active = document.activeElement;
      if (!e.shiftKey && active === last) { e.preventDefault(); first.focus(); }
      else if (e.shiftKey && active === first) { e.preventDefault(); last.focus(); }
    }
  });

  return {
    element: veu,
    open() {
      previous = document.activeElement as HTMLElement | null;
      document.body.append(veu);
      (modal.querySelector<HTMLElement>(focusable) ?? modal).focus();
    },
    close,
  };
}
