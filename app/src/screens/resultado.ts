import { h, icon } from "../dom";
import { formatBytes, formatInt, formatSeconds } from "../format";
import type { Ui } from "../app";
import { actions, frame } from "./frame";

const fileName = (path: string) => path.split(/[\\/]/).pop() ?? path;

export function renderResultado(ui: Ui): HTMLElement {
  const r = ui.state.result!;
  const inPst = r.checks.reduce((sum, c) => sum + c.found, 0);
  const ok = r.verified;
  const head = h("div", { class: "resultado-topo" },
    h("span", { class: ok ? "selo ok" : "selo atencao" }, icon(ok ? "ok" : "atencao", 20)),
    h("div", {},
      h("h1", { class: "titulo" }, ok ? "Conversão concluída" : "Conversão concluída com diferenças"),
      h("p", { class: "sub" }, ok
        ? "Conferimos o arquivo pasta por pasta: tudo bate com a prévia."
        : r.skipped
          ? `${formatInt(r.skipped)} ${r.skipped === 1 ? "e-mail não pôde ser lido e ficou" : "e-mails não puderam ser lidos e ficaram"} fora. Veja o motivo ao lado.`
          : "Há pastas com quantidade diferente da prévia. Veja a tabela e o relatório.")));

  const numbers = h("div", { class: "numeros", style: "margin-top:22px" },
    h("div", {}, h("b", {}, formatInt(r.converted)), "e-mails convertidos"),
    h("div", {}, h("b", {}, formatInt(inPst)), "itens no PST, contando cópias"),
    r.excluded ? h("div", {}, h("b", {}, formatInt(r.excluded)), "deixados de fora") : null,
    h("div", {}, h("b", {}, formatInt(r.skipped)), "pulados"),
    h("div", {}, h("b", {}, formatSeconds(r.elapsedSeconds)), "de conversão"));

  const table = h("table", { class: "tabela" },
    h("thead", {}, h("tr", {}, h("th", {}, "Pasta"), h("th", { class: "n" }, "Esperado"), h("th", { class: "n" }, "Encontrado"), h("th", {}, h("span", { class: "sr" }, "Situação")))),
    h("tbody", {}, ...r.checks.filter((c) => c.expected > 0 || c.found > 0).map((c) => {
      const same = c.expected === c.found;
      return h("tr", { class: same ? "" : "diferente" },
        h("td", {}, c.path.join(" / ")), h("td", { class: "n" }, formatInt(c.expected)), h("td", { class: "n" }, formatInt(c.found)),
        h("td", {}, same ? h("span", { class: "ok", "aria-label": "confere" }, icon("ok", 20)) : "diferença"));
    })));

  const side = h("div", { class: "bloco" },
    r.skippedItems.length ? h("h3", {}, "E-mails pulados") : null,
    r.skippedItems.length ? h("ul", { class: "avisos" }, ...r.skippedItems.map((s) => h("li", {}, `${s.identifier}: ${s.reason}`))) : null,
    h("h3", {}, r.outputFiles.length > 1 ? "Arquivos gerados" : "Arquivo gerado"),
    h("div", { class: "lista-arquivos" }, ...r.outputFiles.map((f, i) =>
      h("div", {}, h("span", { title: f }, fileName(f)), r.outputSizes?.[i] != null ? h("span", {}, formatBytes(r.outputSizes[i])) : null))),
    r.warnings.length ? h("h3", {}, "Avisos") : null,
    r.warnings.length ? h("ul", { class: "avisos" }, ...r.warnings.slice(0, 20).map((w) => h("li", {}, w))) : null,
    r.warnings.length > 20 ? h("p", { class: "sub" }, `Mais ${formatInt(r.warnings.length - 20)} avisos no relatório.`) : null);

  return frame(ui, 2,
    h("main", { class: "corpo" }, head, numbers, h("div", { class: "grade-resultado" }, h("div", {}, table), side)),
    actions(
      h("button", { class: "btn", type: "button", onclick: () => ui.dispatch({ type: "reset" }) }, "Converter outro arquivo"),
      h("button", { class: "btn", type: "button", onclick: () => ui.services.openFile(r.reportPath.replace(/\.json$/, ".txt")) }, "Ver relatório"),
      h("button", { class: "btn btn-primario", type: "button", onclick: () => ui.services.revealInFolder(r.outputFiles[0]) }, "Abrir pasta do arquivo")));
}
