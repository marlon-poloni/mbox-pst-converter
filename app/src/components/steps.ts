import { h } from "../dom";

const names = ["Arquivo", "Pastas", "Converter"];

export function steps(current: number): HTMLElement {
  return h("nav", { class: "etapas", "aria-label": "Etapas" },
    ...names.map((n, i) => h("span", { class: i === current ? "atual" : i < current ? "feita" : "", "aria-current": i === current ? "step" : undefined }, n)));
}
