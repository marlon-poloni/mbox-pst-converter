import { h, icon } from "../dom";
import { folder } from "../components/folder";
import { tag } from "../components/tag";
import { tree } from "../components/tree";
import { segmented } from "../components/segmented";
import { dialog } from "../components/dialog";
import { formatBytes, formatInt } from "../format";
import type { Ui } from "../app";
import type { DecisionDto, LabelDto, LabelKind } from "../types";
import { openEditor } from "./editor";
import { actions, errorBanner, frame } from "./frame";

const STANDARD: LabelKind[] = ["inbox", "sent", "drafts", "archived", "spam", "trash"];
const STATE_TEXT: Partial<Record<LabelKind, string>> = { unread: "Fica como não lido", starred: "Fica sinalizado", forwarded: "Fica como encaminhado" };

export function destination(label: LabelDto, decision: DecisionDto | undefined): HTMLElement {
  if (!decision) return h("span", { class: "sem-pasta" }, "Sem decisão");
  switch (decision.kind) {
    case "folder": return folder(decision.folderPath.join(" / "));
    case "mark": return h("span", { class: "marcacao" }, STATE_TEXT[label.kind] ?? `Categoria "${label.name}"`);
    case "ignoreLabel": return h("span", { class: "sem-pasta" }, "Não vira pasta");
    case "exclude": return h("span", { class: "fora" }, "Fica fora do arquivo");
  }
}

export function renderPastas(ui: Ui): HTMLElement {
  const { scan, mapping, preview, selected } = ui.state;
  if (!scan || !mapping || !preview) return h("div");

  const item = (label: LabelDto) => {
    const decision = mapping.decisions[label.name];
    const quick = label.kind === "spam" || label.kind === "trash"
      ? decision?.kind === "exclude"
        ? h("button", { class: "btn btn-peq", type: "button", onclick: (e: Event) => { e.stopPropagation(); ui.dispatch({ type: "restoreDecision", label: label.name }); } }, "Converter de novo")
        : h("button", { class: "btn btn-peq", type: "button", onclick: (e: Event) => { e.stopPropagation(); ui.dispatch({ type: "setDecision", label: label.name, decision: { kind: "exclude", folderPath: [] } }); } }, "Não converter")
      : null;
    const open = () => {
      ui.dispatch({ type: "selectLabel", label: label.name });
      openEditor(ui, label);
    };
    return h("div", { class: selected === label.name ? "item ativo" : "item" },
      h("button", { type: "button", class: "item-principal", "data-key": label.name, "aria-label": `Mudar o destino de ${label.name}`, onclick: open },
        tag(label.name), h("span", { class: "num" }, formatInt(label.count)), h("span", { class: "destino" }, destination(label, decision))),
      quick);
  };

  const groups: [string, LabelDto[]][] = [
    ["Pastas padrão", scan.labels.filter((l) => STANDARD.includes(l.kind)).sort((a, b) => STANDARD.indexOf(a.kind) - STANDARD.indexOf(b.kind))],
    ["Seus marcadores", scan.labels.filter((l) => l.kind === "user")],
    ["Situação e categorias", scan.labels.filter((l) => !STANDARD.includes(l.kind) && l.kind !== "user")],
  ];

  const selectedDecision = selected ? mapping.decisions[selected] : undefined;
  const highlight = selectedDecision?.kind === "folder" ? selectedDecision.folderPath : undefined;

  const left = h("section", { "data-scroll": "esquerda" },
    h("h2", {}, "No Gmail"),
    h("p", { class: "sub" }, `${formatInt(scan.labels.length)} marcadores. Clique num marcador para mudar o destino.`),
    errorBanner(ui),
    h("div", { class: "lista" }, ...groups.filter(([, ls]) => ls.length).flatMap(([title, ls]) => [h("div", { class: "grupo-titulo" }, title), ...ls.map(item)])));

  const multi = preview.messagesInMoreThanOneFolder;
  const showCopies = multi > 0 || mapping.copyPolicy === "firstFolderOnly";
  const right = h("section", { "data-scroll": "direita" },
    h("h2", {}, "No Outlook"),
    h("p", { class: "sub" }, "Como o arquivo vai aparecer depois de importado."),
    tree(preview.root, highlight),
    showCopies
      ? h("div", { class: "aviso" }, icon("copia"),
          h("div", { class: "texto" },
            h("p", {}, mapping.copyPolicy === "eachFolder"
              ? `${formatInt(multi)} e-mails têm mais de um marcador que vira pasta, somando ${formatInt(preview.extraCopies)} cópias. Escolha como eles entram no arquivo:`
              : "E-mails com mais de um marcador que vira pasta entram só na primeira pasta. Escolha como eles entram no arquivo:"),
            segmented([{ value: "eachFolder", label: "Uma cópia em cada pasta" }, { value: "firstFolderOnly", label: "Só na primeira pasta" }],
              mapping.copyPolicy, (policy) => ui.dispatch({ type: "setCopyPolicy", policy }), "Cópias de e-mails com vários marcadores")))
      : null);

  const summary = h("div", { class: ui.state.previewPending ? "resumo pendente" : "resumo", "aria-busy": String(ui.state.previewPending) },
    h("span", {}, h("b", {}, formatInt(preview.folderCount)), "pastas no Outlook"),
    h("span", {}, h("b", {}, formatInt(preview.messagesInPst)), "e-mails no arquivo PST"),
    h("span", {}, h("b", {}, `≈ ${formatBytes(preview.estimatedBytes)}`), "tamanho estimado"),
    preview.excluded ? h("span", {}, h("b", {}, formatInt(preview.excluded)), "ficam fora") : null);

  return frame(ui, 1, h("div", { class: "dois" }, left, right), summary,
    actions(
      h("button", { class: "btn", type: "button", onclick: () => openSize(ui) }, `Dividir a cada ${ui.state.maxPartGB} GB`),
      h("button", { class: "btn", type: "button", onclick: () => ui.requestChangeFile() }, "Trocar arquivo"),
      h("button", { class: "btn btn-primario", type: "button", onclick: () => ui.convert() }, "Converter")));
}

function openSize(ui: Ui) {
  const input = h("input", { id: "tamanho", value: String(ui.state.maxPartGB), inputmode: "numeric", "aria-describedby": "tamanho-ajuda" }) as HTMLInputElement;
  const error = h("span", { class: "erro", role: "alert" });
  const field = h("div", { class: "campo" },
    h("label", { for: "tamanho" }, "Dividir em arquivos de até"),
    h("div", { class: "campo-linha" }, input, h("span", {}, "GB")),
    error,
    h("span", { class: "ajuda", id: "tamanho-ajuda" }, `Quando passar desse tamanho, o conversor cria "${ui.state.mapping?.storeName}-1.pst", "${ui.state.mapping?.storeName}-2.pst" e assim por diante.`));
  input.addEventListener("input", () => { error.textContent = ""; field.classList.remove("invalido"); });
  input.addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); apply.click(); } });
  const apply = h("button", { class: "btn btn-primario", type: "button" }, "Aplicar");
  const d = dialog("Tamanho de cada arquivo", field, [h("button", { class: "btn", type: "button", onclick: () => d.close() }, "Cancelar"), apply]);
  apply.addEventListener("click", () => {
    const value = Number(input.value.trim());
    if (!Number.isInteger(value) || value < 1 || value > 50) {
      error.textContent = "Use um número inteiro entre 1 e 50.";
      field.classList.add("invalido");
      input.focus();
      return;
    }
    d.close();
    ui.dispatch({ type: "setMaxPartGB", gb: value });
  });
  d.open();
}
