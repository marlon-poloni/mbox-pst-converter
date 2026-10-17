import { h } from "../dom";

export function tag(name: string): HTMLElement {
  return h("span", { class: "tag", title: name }, h("span", { class: "nome" }, name));
}
