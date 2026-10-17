import { h, icon } from "../dom";
import { formatBytes, formatBytesPair, formatInt } from "../format";
import type { Ui } from "../app";
import { actions, errorBanner, frame } from "./frame";

export function renderArquivo(ui: Ui): HTMLElement {
  const { scanning, scan, file } = ui.state;

  if (scanning && file) {
    const pct = scanning.sourceBytes ? Math.round((scanning.bytesRead / scanning.sourceBytes) * 100) : 0;
    return frame(ui, 0,
      h("main", { class: "corpo" },
        h("h1", {}, "Lendo o arquivo"),
        h("p", { class: "sub" }, "Contando e-mails e marcadores. Nada é enviado para a internet."),
        h("div", { class: "cartao-arquivo" },
          fileHead(file.name, file.path),
          h("div", { class: "progresso", role: "progressbar", "aria-valuenow": pct, "aria-valuemin": 0, "aria-valuemax": 100 }, h("i", { style: `width:${pct}%` })),
          h("div", { class: "progresso-texto" },
            h("span", {}, scanning.sourceBytes ? `${formatBytesPair(scanning.bytesRead, scanning.sourceBytes)} lidos` : "Começando a leitura"),
            h("span", {}, `${pct}%`)))),
      actions(h("button", { class: "btn", type: "button", onclick: () => ui.cancel() }, "Cancelar leitura")));
  }

  if (scan) {
    const unread = scan.labels.filter((l) => l.kind === "unread").reduce((sum, l) => sum + l.count, 0);
    return frame(ui, 0,
      h("main", { class: "corpo" },
        h("h1", {}, "Arquivo pronto para configurar"),
        h("p", { class: "sub" }, "Confira os números e siga para escolher as pastas."),
        errorBanner(ui),
        h("div", { class: "cartao-arquivo" },
          fileHead(scan.fileName, scan.path),
          h("div", { class: "numeros" },
            h("div", {}, h("b", {}, formatInt(scan.messages)), "e-mails"),
            h("div", {}, h("b", {}, formatInt(scan.labels.length)), "marcadores"),
            h("div", {}, h("b", {}, formatBytes(scan.sourceBytes)), "tamanho"),
            h("div", {}, h("b", {}, formatInt(unread)), "não lidos")))),
      actions(
        h("button", { class: "btn", type: "button", onclick: () => ui.chooseFile() }, "Trocar arquivo"),
        h("button", { class: "btn btn-primario", type: "button", onclick: () => ui.dispatch({ type: "continue" }) }, "Continuar")));
  }

  return frame(ui, 0,
    h("main", { class: "corpo" },
      h("h1", {}, "Escolha o arquivo do Gmail"),
      h("p", { class: "sub" }, "O arquivo .mbox que vem do Google Takeout. Ele só é lido, nunca alterado."),
      errorBanner(ui),
      h("div", { class: ui.dragging ? "area-arquivo arrastando" : "area-arquivo" },
        icon("mbox", 44),
        h("button", { class: "btn btn-primario", type: "button", onclick: () => ui.chooseFile() }, "Escolher arquivo"),
        h("p", {}, ui.dragging ? "Solte o arquivo para ler" : "ou arraste o arquivo para esta área"))));
}

function fileHead(name: string, path: string): HTMLElement {
  return h("div", { class: "topo" }, icon("mbox", 44),
    h("div", {}, h("div", { class: "nome-arquivo", title: name }, name), h("div", { class: "caminho", title: path }, path)));
}
