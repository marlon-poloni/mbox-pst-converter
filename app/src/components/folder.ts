import { h, icon } from "../dom";

export function folder(name: string): HTMLElement {
  return h("span", { class: "pasta" }, icon("pasta"), h("span", { class: "nome" }, name));
}
