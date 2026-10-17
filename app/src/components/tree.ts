import { h, icon } from "../dom";
import { formatInt } from "../format";
import type { FolderDto } from "../types";

const samePath = (a: string[], b?: string[]) => !!b && a.length === b.length && a.every((s, i) => s.toLowerCase() === b[i].toLowerCase());

function children(nodes: FolderDto[], highlight?: string[]): HTMLElement {
  const box = h("div", { class: "filhos" });
  for (const n of nodes) {
    box.append(
      h("div", { class: samePath(n.path, highlight) ? "no realce" : "no" }, icon("pasta"), h("span", {}, n.name), h("span", { class: "num" }, n.messages ? formatInt(n.messages) : "")),
    );
    if (n.children.length) box.append(children(n.children, highlight));
  }
  return box;
}

export function tree(root: FolderDto, highlight?: string[]): HTMLElement {
  return h("div", { class: "arvore" }, h("div", { class: "raiz" }, root.name), children(root.children, highlight));
}
