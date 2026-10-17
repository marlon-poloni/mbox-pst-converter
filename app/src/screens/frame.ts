import { h } from "../dom";
import { steps } from "../components/steps";
import { formatBytes } from "../format";
import type { Ui } from "../app";

export function frame(ui: Ui, step: number, body: HTMLElement, ...footer: (HTMLElement | null)[]): HTMLElement {
  const scan = ui.state.scan;
  const fileLabel = scan && step > 0 ? `${scan.fileName} · ${formatBytes(scan.sourceBytes)}` : null;
  return h("div", { class: "app" },
    h("header", { class: "barra-app" },
      h("span", { class: "marca" }, "Conversor de e-mails"),
      steps(step),
      fileLabel ? h("span", { class: "arquivo", title: scan!.path }, fileLabel) : null),
    body,
    footer.some(Boolean) ? h("footer", { class: "rodape" }, ...footer) : null);
}

export function errorBanner(ui: Ui): HTMLElement | null {
  return ui.state.error ? h("p", { class: "aviso-erro", role: "alert" }, ui.state.error) : null;
}

export function actions(...buttons: (HTMLElement | null)[]): HTMLElement {
  return h("div", { class: "acoes" }, ...buttons);
}
