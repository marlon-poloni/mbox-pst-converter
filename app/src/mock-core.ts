import type { Command, CoreEvent, FolderDto, LabelDto, LabelKind, MappingDto, PreviewDto } from "./types";
import type { Core } from "./core";

const SOURCE_BYTES = 6654983453;
const MESSAGES = 6614;
const BYTES_PER_COPY = 5023695872 / 6954;

const LABELS: LabelDto[] = [
  ["Caixa de entrada", "inbox", 5773], ["Aberto", "opened", 4075], ["Categoria: pessoal", "category", 3382],
  ["Importante", "important", 2747], ["Categoria: atualizações", "category", 2141], ["Não lidas", "unread", 1068],
  ["Enviado", "sent", 703], ["Categoria: promoções", "category", 337], ["Arquivada", "archived", 163],
  ["Faturas", "user", 124], ["Spam", "spam", 76], ["Categoria: compras", "category", 64], ["Fornecedores", "user", 46],
  ["Rascunhos", "drafts", 24], ["Processos 2025", "user", 17], ["IMAP_$Forwarded", "forwarded", 14], ["Notes", "user", 12],
  ["Categoria: viagens", "category", 11], ["Categoria: fóruns", "category", 10], ["Categoria: redes sociais", "category", 8],
  ["Lixeira", "trash", 7], ["Com estrela", "starred", 6], ["Marketing", "user", 6], ["Compras", "user", 3],
].map(([name, kind, count]) => ({ name: name as string, kind: kind as LabelKind, count: count as number }));

const STANDARD: Partial<Record<LabelKind, string>> = {
  inbox: "Caixa de Entrada", sent: "Itens Enviados", drafts: "Rascunhos", archived: "Arquivo Morto", spam: "Lixo Eletrônico", trash: "Itens Excluídos",
};
const ORDER = Object.values(STANDARD);

function defaultMapping(): MappingDto {
  const decisions: MappingDto["decisions"] = {};
  for (const l of LABELS) {
    if (STANDARD[l.kind]) decisions[l.name] = { kind: "folder", folderPath: [STANDARD[l.kind]!] };
    else if (l.kind === "user") decisions[l.name] = { kind: "folder", folderPath: l.name.split("/") };
    else if (["unread", "starred", "forwarded"].includes(l.kind)) decisions[l.name] = { kind: "mark", folderPath: [] };
    else decisions[l.name] = { kind: "ignoreLabel", folderPath: [] };
  }
  return {
    storeName: "Todos os e-mails",
    copyPolicy: "eachFolder",
    decisions,
    userPriority: LABELS.filter((l) => l.kind === "user").map((l) => l.name),
  };
}

function preview(mapping: MappingDto): PreviewDto {
  const root: FolderDto = { name: mapping.storeName, path: [], messages: 0, children: [] };
  let excluded = 0;
  let inFolders = 0;
  for (const l of LABELS) {
    const d = mapping.decisions[l.name];
    if (!d) continue;
    if (d.kind === "exclude") excluded += l.count;
    if (d.kind !== "folder" || !d.folderPath.length) continue;
    let node = root;
    d.folderPath.forEach((segment, i) => {
      let child = node.children.find((c) => c.name.toLowerCase() === segment.toLowerCase());
      if (!child) {
        child = { name: segment, path: d.folderPath.slice(0, i + 1), messages: 0, children: [] };
        node.children.push(child);
      }
      node = child;
    });
    node.messages += l.count;
    inFolders += l.count;
  }
  const sort = (nodes: FolderDto[], top: boolean) => {
    nodes.sort((a, b) => {
      const ia = top ? ORDER.indexOf(a.name) : -1;
      const ib = top ? ORDER.indexOf(b.name) : -1;
      if (ia !== ib) return (ia < 0 ? 99 : ia) - (ib < 0 ? 99 : ib);
      return a.name.localeCompare(b.name, "pt-BR");
    });
    nodes.forEach((n) => sort(n.children, false));
  };
  sort(root.children, true);
  const count = (n: FolderDto): number => n.children.reduce((s, c) => s + 1 + count(c), 0);
  const kept = MESSAGES - excluded;
  const inPst = mapping.copyPolicy === "firstFolderOnly" ? kept : Math.max(kept, inFolders);
  return {
    root, sourceMessages: MESSAGES, messagesInPst: inPst, extraCopies: inPst - kept,
    messagesInMoreThanOneFolder: mapping.copyPolicy === "firstFolderOnly" ? 0 : 328, excluded,
    estimatedBytes: Math.round(inPst * BYTES_PER_COPY), folderCount: count(root),
  };
}

function leaves(node: FolderDto): FolderDto[] {
  return node.children.flatMap((c) => [c, ...leaves(c)]);
}

export function mockCore(): Core {
  const handlers = new Set<(e: CoreEvent) => void>();
  const emit = (e: CoreEvent): void => {
    setTimeout(() => handlers.forEach((h) => h(e)), 0);
  };
  const params = new URLSearchParams(location.search);
  let mapping: MappingDto | null = null;
  let timer: ReturnType<typeof setInterval> | undefined;

  (window as unknown as { __mock: unknown }).__mock = { emit: (e: CoreEvent) => handlers.forEach((h) => h(e)) };

  const scanDone = (path: string): CoreEvent => {
    mapping = defaultMapping();
    return { event: "scanDone", path, fileName: path.split(/[\\/]/).pop()!, messages: MESSAGES, sourceBytes: SOURCE_BYTES, labels: LABELS, mapping, preview: preview(mapping) };
  };

  return {
    async send(command: Command) {
      if (command.cmd === "scan") {
        if (params.get("cenario") === "lento") return;
        if (params.get("cenario") === "instantaneo" || params.has("cenario")) {
          emit(scanDone(command.path));
          return;
        }
        let read = 0;
        timer = setInterval(() => {
          read = Math.min(SOURCE_BYTES, read + SOURCE_BYTES / 12);
          emit({ event: "scanProgress", bytesRead: read, sourceBytes: SOURCE_BYTES });
          if (read >= SOURCE_BYTES) {
            clearInterval(timer);
            emit(scanDone(command.path));
          }
        }, 120);
      } else if (command.cmd === "preview") {
        mapping = command.mapping;
        emit({ event: "preview", preview: preview(command.mapping) });
      } else if (command.cmd === "convert") {
        const p = preview(command.mapping);
        if (params.get("cenario") === "disco-cheio") {
          setTimeout(() => emit({ event: "error", message: `Espaço em disco insuficiente em ${command.outputDirectory}.` }), 400);
          return;
        }
        if ((window as unknown as { __hold?: boolean }).__hold) return;
        let done = 0;
        timer = setInterval(() => {
          done = Math.min(MESSAGES, done + 551);
          const bytes = Math.round((SOURCE_BYTES * done) / MESSAGES);
          emit({ event: "convertProgress", done, total: MESSAGES, bytesRead: bytes, sourceBytes: SOURCE_BYTES, megabytesPerSecond: 138, remainingSeconds: (SOURCE_BYTES - bytes) / (138 * 1024 * 1024) });
          if (done < MESSAGES) return;
          clearInterval(timer);
          const differ = params.get("cenario") === "diferenca";
          const checks = leaves(p.root).map((n, i) => ({ path: n.path, expected: n.messages, found: differ && i === 0 ? n.messages - 2 : n.messages }));
          emit({
            event: "convertDone",
            outputFiles: [`${command.outputDirectory}/${command.outputName}.pst`],
            outputSizes: [p.estimatedBytes],
            reportPath: `${command.outputDirectory}/${command.outputName}-relatorio.json`,
            converted: MESSAGES - p.excluded - (differ ? 2 : 0), excluded: p.excluded, skipped: differ ? 2 : 0,
            skippedItems: differ ? [{ identifier: "e-mail 2.431", reason: "a mensagem está corrompida no arquivo de origem." }, { identifier: "e-mail 5.002", reason: "tamanho acima de 1,9 GB." }] : [],
            warnings: ["4 convites de agenda vieram sem conteúdo e não foram anexados."],
            checks, verified: !differ, elapsedSeconds: 47,
          });
        }, 150);
      } else if (command.cmd === "cancel") {
        clearInterval(timer);
        emit({ event: "cancelled" });
      }
    },
    onEvent(handler) {
      handlers.add(handler);
      return () => handlers.delete(handler);
    },
  };
}
