import { h, icon } from "../dom";
import { dialog } from "../components/dialog";
import { formatBytesPair, formatInt, formatSeconds } from "../format";
import type { Ui } from "../app";
import { actions, frame } from "./frame";

export function renderConverter(ui: Ui): HTMLElement {
  const { progress, failure, output, scan } = ui.state;

  if (failure) {
    return frame(ui, 2,
      h("main", { class: "corpo" },
        h("div", { class: "falha" },
          h("div", { class: "resultado-topo" }, h("span", { class: "selo erro" }, icon("x", 20)),
            h("div", {}, h("h1", { class: "titulo" }, "A conversão não terminou"), h("p", { class: "sub" }, "Nenhum arquivo PST foi deixado pela metade."))),
          h("p", { class: "mensagem", role: "alert" }, failure),
          h("p", {}, guidance(failure)))),
      actions(
        h("button", { class: "btn", type: "button", onclick: () => ui.dispatch({ type: "backToFolders" }) }, "Voltar às pastas"),
        h("button", { class: "btn btn-primario", type: "button", onclick: () => ui.convert() }, "Converter de novo")));
  }

  const total = progress?.total ?? scan?.messages ?? 0;
  const done = progress?.done ?? 0;
  const pct = total ? Math.min(100, Math.round((done / total) * 100)) : 0;
  const speed = progress && progress.megabytesPerSecond > 0 ? ` · ${formatInt(Math.round(progress.megabytesPerSecond))} MB/s` : "";
  const remaining = progress?.remainingSeconds != null && progress.done < progress.total ? `faltam cerca de ${formatSeconds(progress.remainingSeconds)}` : "";

  return frame(ui, 2,
    h("main", { class: "corpo" },
      h("div", { class: "conversao" },
        h("h1", { class: "titulo" }, "Convertendo"),
        h("div", { class: "contagem", "aria-live": "polite" }, formatInt(done), h("span", {}, ` de ${formatInt(total)} e-mails`)),
        h("div", { class: "progresso", role: "progressbar", "aria-valuenow": pct, "aria-valuemin": 0, "aria-valuemax": 100 }, h("i", { style: `width:${pct}%` })),
        h("div", { class: "progresso-texto" },
          h("span", {}, progress ? `${formatBytesPair(progress.bytesRead, progress.sourceBytes)} lidos${speed}` : "Preparando a conversão"),
          h("span", {}, remaining)),
        output ? h("p", { class: "destino-arquivo" }, `Gravando em ${joinPath(output.directory, `${output.name}.pst`)}`) : null)),
    actions(h("button", { class: "btn btn-perigo", type: "button", onclick: () => confirmCancel(ui) }, "Cancelar conversão")));
}

function guidance(message: string): string {
  if (message.startsWith("Espaço em disco")) return "Libere espaço nessa pasta ou escolha outra pasta, e converta de novo. As escolhas de pastas continuam salvas.";
  if (message.startsWith("Sem permissão")) return "Escolha uma pasta em que você possa gravar, e converta de novo. As escolhas de pastas continuam salvas.";
  if (message.startsWith("Já existe")) return "Escolha outro nome ou outra pasta, e converta de novo. As escolhas de pastas continuam salvas.";
  return "Confira a mensagem acima e converta de novo. As escolhas de pastas continuam salvas.";
}

function joinPath(directory: string, file: string): string {
  const separator = directory.includes("\\") && !directory.includes("/") ? "\\" : "/";
  return directory.endsWith(separator) ? directory + file : directory + separator + file;
}

function confirmCancel(ui: Ui) {
  const d = dialog("Cancelar a conversão?", h("p", {}, "O arquivo PST parcial será apagado. O arquivo do Gmail continua como está."), [
    h("button", { class: "btn", type: "button", onclick: () => d.close() }, "Continuar convertendo"),
    h("button", { class: "btn btn-perigo-cheio", type: "button", onclick: () => { d.close(); ui.cancel(); } }, "Cancelar conversão"),
  ]);
  d.open();
}
