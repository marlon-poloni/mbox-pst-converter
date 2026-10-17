import { tag } from "../src/components/tag";
import { folder } from "../src/components/folder";
import { tree } from "../src/components/tree";
import { segmented } from "../src/components/segmented";
import { dialog } from "../src/components/dialog";
import { steps } from "../src/components/steps";
import { formatBytes, formatInt, formatSeconds } from "../src/format";
import type { FolderDto } from "../src/types";

const node = (name: string, path: string[], messages: number, children: FolderDto[] = []): FolderDto => ({ name, path, messages, children });

describe("componentes", () => {
  it("etiqueta mostra o nome do marcador", () => {
    const el = tag("Fornecedores");
    expect(el.className).toBe("tag");
    expect(el.querySelector(".nome")!.textContent).toBe("Fornecedores");
    expect(el.title).toBe("Fornecedores");
  });

  it("pasta tem ícone e nome", () => {
    const el = folder("Itens Enviados");
    expect(el.classList.contains("pasta")).toBe(true);
    expect(el.querySelector("svg")).not.toBeNull();
    expect(el.querySelector(".nome")!.textContent).toBe("Itens Enviados");
  });

  it("árvore mostra raiz, pastas aninhadas, contagens e destaque", () => {
    const root = node("Todos os e-mails", [], 0, [
      node("Caixa de Entrada", ["Caixa de Entrada"], 5773),
      node("Clientes", ["Clientes"], 0, [node("Fornecedores", ["Clientes", "Fornecedores"], 46)]),
    ]);
    const el = tree(root, ["Clientes", "Fornecedores"]);
    expect(el.querySelector(".raiz")!.textContent).toBe("Todos os e-mails");
    const rows = [...el.querySelectorAll(".no")];
    expect(rows.map((r) => r.querySelector("span")!.textContent)).toEqual(["Caixa de Entrada", "Clientes", "Fornecedores"]);
    expect(rows[0].querySelector(".num")!.textContent).toBe("5.773");
    expect(rows[1].querySelector(".num")!.textContent).toBe("");
    expect(el.querySelectorAll(".no.realce")).toHaveLength(1);
    expect(el.querySelector(".no.realce span")!.textContent).toBe("Fornecedores");
    expect(el.querySelectorAll(".filhos .filhos .no")).toHaveLength(1);
  });

  it("controle segmentado marca a opção e avisa a troca", () => {
    const chosen: string[] = [];
    const el = segmented([{ value: "a", label: "Pasta" }, { value: "b", label: "Marcação" }], "a", (v) => chosen.push(v));
    const buttons = [...el.querySelectorAll("button")];
    expect(buttons.map((b) => b.classList.contains("on"))).toEqual([true, false]);
    expect(buttons[0].getAttribute("aria-pressed")).toBe("true");
    buttons[1].click();
    expect(chosen).toEqual(["b"]);
    expect(buttons.map((b) => b.classList.contains("on"))).toEqual([false, true]);
  });

  it("janela modal prende o foco, fecha com Esc e devolve o foco", () => {
    const opener = document.createElement("button");
    document.body.append(opener);
    opener.focus();
    const first = document.createElement("input");
    const ok = document.createElement("button");
    ok.textContent = "Aplicar";
    let closed = 0;
    const d = dialog("Destino do marcador", first, [ok], { onClose: () => closed++ });

    d.open();
    const veu = document.querySelector(".veu")!;
    expect(veu.querySelector("[role=dialog]")!.getAttribute("aria-modal")).toBe("true");
    expect(document.activeElement).toBe(first);

    ok.focus();
    veu.dispatchEvent(new KeyboardEvent("keydown", { key: "Tab", bubbles: true }));
    expect(document.activeElement).toBe(first);

    veu.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    expect(document.querySelector(".veu")).toBeNull();
    expect(document.activeElement).toBe(opener);
    expect(closed).toBe(1);
  });

  it("etapas marcam feitas e atual", () => {
    const el = steps(1);
    const spans = [...el.querySelectorAll("span")];
    expect(spans.map((s) => s.textContent)).toEqual(["Arquivo", "Pastas", "Converter"]);
    expect(spans.map((s) => s.className)).toEqual(["feita", "atual", ""]);
    expect(el.getAttribute("aria-label")).toBe("Etapas");
  });

  it("formata números, tamanhos e tempo em pt-BR", () => {
    expect(formatInt(6614)).toBe("6.614");
    expect(formatInt(0)).toBe("0");
    expect(formatBytes(6654983453)).toBe("6,2 GB");
    expect(formatBytes(5023695872)).toBe("4,7 GB");
    expect(formatBytes(600 * 1024)).toBe("600 KB");
    expect(formatBytes(12 * 1024 * 1024)).toBe("12 MB");
    expect(formatSeconds(23)).toBe("23 s");
    expect(formatSeconds(0.4)).toBe("menos de 1 s");
    expect(formatSeconds(125)).toBe("2 min 5 s");
    expect(formatSeconds(3600 + 120)).toBe("1 h 2 min");
  });
});
