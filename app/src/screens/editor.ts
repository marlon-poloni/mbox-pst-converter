import { h } from "../dom";
import { tag } from "../components/tag";
import { segmented } from "../components/segmented";
import { dialog } from "../components/dialog";
import { formatInt } from "../format";
import type { Ui } from "../app";
import type { DecisionKind, FolderDto, LabelDto, LabelKind } from "../types";

const STATE_EFFECT: Partial<Record<LabelKind, string>> = {
  unread: "Os e-mails com este marcador ficam como não lidos no Outlook.",
  starred: "Os e-mails com este marcador ficam sinalizados no Outlook.",
  forwarded: "Os e-mails com este marcador ficam marcados como encaminhados no Outlook.",
};

function folderPaths(node: FolderDto, own: string[]): string[][] {
  const isOwn = (p: string[]) => p.length >= own.length && own.every((s, i) => s.toLowerCase() === p[i].toLowerCase());
  return node.children.flatMap((c) => (own.length && isOwn(c.path) ? [] : [c.path, ...folderPaths(c, own)]));
}

function explanation(kind: DecisionKind, label: LabelDto): string | null {
  switch (kind) {
    case "folder": return null;
    case "mark": return STATE_EFFECT[label.kind] ?? `O marcador vira a categoria "${label.name}" no Outlook. Os e-mails continuam nas outras pastas deles.`;
    case "ignoreLabel": return "O marcador é ignorado. Os e-mails continuam nas outras pastas deles; os que não tiverem outra vão para Arquivo Morto.";
    case "exclude": return `Os ${formatInt(label.count)} e-mails com este marcador ficam fora do arquivo PST, mesmo que tenham outros marcadores. O relatório mostra quantos ficaram de fora.`;
  }
}

export function openEditor(ui: Ui, label: LabelDto) {
  const mapping = ui.state.mapping!;
  const current = mapping.decisions[label.name] ?? { kind: "ignoreLabel", folderPath: [] };
  let kind: DecisionKind = current.kind;
  const own = current.kind === "folder" ? current.folderPath : [];
  const defaultName = current.kind === "folder" ? current.folderPath[current.folderPath.length - 1] : label.name.split("/").pop()!;
  const defaultParent = current.kind === "folder" ? current.folderPath.slice(0, -1) : [];

  const name = h("input", { id: "nome-pasta", value: defaultName, autocomplete: "off" }) as HTMLInputElement;
  const nameError = h("span", { class: "erro", role: "alert" });
  const nameField = h("div", { class: "campo" }, h("label", { for: "nome-pasta" }, "Nome da pasta"), name, nameError);
  const parent = h("select", { id: "pasta-mae" }) as HTMLSelectElement;
  parent.append(h("option", { value: "" }, "Raiz do arquivo"));
  for (const path of folderPaths(ui.state.preview!.root, own))
    parent.append(h("option", { value: path.join("/") }, path.join(" / ")));
  parent.value = defaultParent.join("/");
  if (parent.value !== defaultParent.join("/")) {
    parent.append(h("option", { value: defaultParent.join("/") }, defaultParent.join(" / ")));
    parent.value = defaultParent.join("/");
  }
  const parentField = h("div", { class: "campo" }, h("label", { for: "pasta-mae" }, "Dentro de"), parent,
    h("span", { class: "ajuda" }, 'Escolha "Raiz do arquivo" para deixar no primeiro nível.'));
  const explain = h("p", { class: "explica" });
  name.addEventListener("input", () => { nameError.textContent = ""; nameField.classList.remove("invalido"); });

  const options: { value: DecisionKind; label: string }[] = [
    { value: "folder", label: "Pasta" },
    { value: "mark", label: "Marcação" },
    { value: "ignoreLabel", label: "Ignorar marcador" },
    { value: "exclude", label: "Não converter" },
  ];
  const body = h("div", { class: "conteudo-editor" });
  const show = () => {
    const text = explanation(kind, label);
    explain.textContent = text ?? "";
    body.replaceChildren(choice, ...(kind === "folder" ? [nameField, parentField] : [explain]));
  };
  const choice = segmented(options, kind, (value) => { kind = value; show(); }, "Destino");
  show();

  const apply = h("button", { class: "btn btn-primario", type: "button" }, "Aplicar");
  const d = dialog("Destino do marcador", body, [h("button", { class: "btn", type: "button", onclick: () => d.close() }, "Cancelar"), apply], {
    top: h("span", { class: "marcador-topo" }, tag(label.name), h("span", { class: "num" }, `${formatInt(label.count)} e-mails`)),
  });
  apply.addEventListener("click", () => {
    if (kind === "folder") {
      const folderName = name.value.trim();
      const problem = !folderName ? "Dê um nome para a pasta." : folderName.length > 255 ? "O nome da pasta pode ter no máximo 255 caracteres." : folderName.includes("/") ? "O nome da pasta não pode ter barra (/)." : null;
      if (problem) {
        nameError.textContent = problem;
        nameField.classList.add("invalido");
        name.focus();
        return;
      }
      const parentPath = parent.value ? parent.value.split("/") : [];
      d.close();
      ui.dispatch({ type: "setDecision", label: label.name, decision: { kind, folderPath: [...parentPath, folderName] } });
      return;
    }
    d.close();
    ui.dispatch({ type: "setDecision", label: label.name, decision: { kind, folderPath: [] } });
  });
  d.open();
}
