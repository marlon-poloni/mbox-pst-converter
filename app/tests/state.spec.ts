import { initialState, reduce, type WizardState } from "../src/state";
import type { CoreEvent, MappingDto, PreviewDto } from "../src/types";

const mapping: MappingDto = {
  storeName: "Todos os e-mails",
  copyPolicy: "eachFolder",
  decisions: { Spam: { kind: "folder", folderPath: ["Lixo Eletrônico"] }, Fornecedores: { kind: "folder", folderPath: ["Fornecedores"] } },
  userPriority: ["Fornecedores"],
};
const preview: PreviewDto = {
  root: { name: "Todos os e-mails", path: [], messages: 0, children: [] },
  sourceMessages: 10, messagesInPst: 12, extraCopies: 2, messagesInMoreThanOneFolder: 1, excluded: 0, estimatedBytes: 1000, folderCount: 2,
};
const scanDone: CoreEvent = {
  event: "scanDone", path: "/a/b.mbox", fileName: "b.mbox", messages: 10, sourceBytes: 5000,
  labels: [{ name: "Spam", kind: "spam", count: 3 }, { name: "Fornecedores", kind: "user", count: 4 }], mapping, preview,
};

const scanned = (): WizardState => reduce(reduce(initialState(), { type: "chooseFile", path: "/a/b.mbox" }), scanDone);
const inFolders = (): WizardState => reduce(scanned(), { type: "continue" });
const converting = (): WizardState => reduce(inFolders(), { type: "startConvert", directory: "/saida", name: "Todos os e-mails" });

describe("estado do assistente", () => {
  it("começa na etapa Arquivo sem nada escolhido", () => {
    const s = initialState();
    expect(s.step).toBe("arquivo");
    expect(s.scan).toBeUndefined();
    expect(s.maxPartGB).toBe(20);
  });

  it("escolher arquivo começa a leitura e limpa erro anterior", () => {
    const s = reduce({ ...initialState(), error: "x" }, { type: "chooseFile", path: "/a/b.mbox" });
    expect(s.file).toEqual({ path: "/a/b.mbox", name: "b.mbox" });
    expect(s.scanning).toEqual({ bytesRead: 0, sourceBytes: 0 });
    expect(s.error).toBeUndefined();
  });

  it("progresso da leitura atualiza a barra", () => {
    const s = reduce(reduce(initialState(), { type: "chooseFile", path: "/a/b.mbox" }), { event: "scanProgress", bytesRead: 10, sourceBytes: 100 });
    expect(s.scanning).toEqual({ bytesRead: 10, sourceBytes: 100 });
  });

  it("scanDone guarda marcadores, de-para e prévia e espera o Continuar", () => {
    const s = scanned();
    expect(s.step).toBe("arquivo");
    expect(s.scanning).toBeUndefined();
    expect(s.scan?.messages).toBe(10);
    expect(s.mapping).toEqual(mapping);
    expect(s.defaults).toEqual(mapping);
    expect(s.preview).toEqual(preview);
    expect(reduce(s, { type: "continue" }).step).toBe("pastas");
  });

  it("mudar uma decisão altera o de-para e deixa a prévia pendente até o evento preview", () => {
    const s = reduce(inFolders(), { type: "setDecision", label: "Spam", decision: { kind: "exclude", folderPath: [] } });
    expect(s.mapping!.decisions.Spam.kind).toBe("exclude");
    expect(s.mapping!.decisions.Fornecedores.kind).toBe("folder");
    expect(mapping.decisions.Spam.kind).toBe("folder");
    expect(s.previewPending).toBe(true);
    const after = reduce(s, { event: "preview", preview: { ...preview, excluded: 3 } });
    expect(after.previewPending).toBe(false);
    expect(after.preview!.excluded).toBe(3);
  });

  it("restaurar volta à decisão padrão do marcador", () => {
    const changed = reduce(inFolders(), { type: "setDecision", label: "Spam", decision: { kind: "exclude", folderPath: [] } });
    const s = reduce(changed, { type: "restoreDecision", label: "Spam" });
    expect(s.mapping!.decisions.Spam).toEqual(mapping.decisions.Spam);
    expect(s.previewPending).toBe(true);
  });

  it("trocar a política de cópias", () => {
    const s = reduce(inFolders(), { type: "setCopyPolicy", policy: "firstFolderOnly" });
    expect(s.mapping!.copyPolicy).toBe("firstFolderOnly");
    expect(s.previewPending).toBe(true);
  });

  it("selecionar marcador destaca e ajustar tamanho máximo", () => {
    expect(reduce(inFolders(), { type: "selectLabel", label: "Fornecedores" }).selected).toBe("Fornecedores");
    expect(reduce(inFolders(), { type: "setMaxPartGB", gb: 5 }).maxPartGB).toBe(5);
  });

  it("converter leva para a etapa Converter", () => {
    const s = converting();
    expect(s.step).toBe("converter");
    expect(s.output).toEqual({ directory: "/saida", name: "Todos os e-mails" });
    expect(s.progress).toBeUndefined();
    expect(s.failure).toBeUndefined();
  });

  it("progresso e fim da conversão levam ao Resultado", () => {
    const p = reduce(converting(), { event: "convertProgress", done: 5, total: 10, bytesRead: 1, sourceBytes: 2, megabytesPerSecond: 3, remainingSeconds: 4 });
    expect(p.progress!.done).toBe(5);
    const done = reduce(p, {
      event: "convertDone", outputFiles: ["/saida/x.pst"], reportPath: "/saida/x-relatorio.json", converted: 10, excluded: 0, skipped: 0,
      skippedItems: [], warnings: [], checks: [], verified: true, elapsedSeconds: 1,
    });
    expect(done.step).toBe("resultado");
    expect(done.result!.verified).toBe(true);
  });

  it("cancelado volta para Pastas mantendo o de-para", () => {
    const changed = reduce(inFolders(), { type: "setCopyPolicy", policy: "firstFolderOnly" });
    const s = reduce(reduce(changed, { type: "startConvert", directory: "/saida", name: "x" }), { event: "cancelled" });
    expect(s.step).toBe("pastas");
    expect(s.mapping!.copyPolicy).toBe("firstFolderOnly");
    expect(s.progress).toBeUndefined();
  });

  it("cancelar a leitura volta ao início da etapa Arquivo", () => {
    const reading = reduce(initialState(), { type: "chooseFile", path: "/a/b.mbox" });
    const s = reduce(reading, { event: "cancelled" });
    expect(s.step).toBe("arquivo");
    expect(s.file).toBeUndefined();
    expect(s.scanning).toBeUndefined();
  });

  it("erro durante a conversão vira tela de falha; em outra etapa vira aviso sem mudar de etapa", () => {
    const failed = reduce(converting(), { event: "error", message: "Espaço em disco insuficiente em /saida." });
    expect(failed.step).toBe("converter");
    expect(failed.failure).toBe("Espaço em disco insuficiente em /saida.");

    const s = reduce(inFolders(), { event: "error", message: "Escolha e leia um arquivo antes." });
    expect(s.step).toBe("pastas");
    expect(s.error).toBe("Escolha e leia um arquivo antes.");
  });

  it("erro na leitura volta para o início com a mensagem", () => {
    const s = reduce(reduce(initialState(), { type: "chooseFile", path: "/x.mbox" }), { event: "error", message: "Arquivo não encontrado: /x.mbox" });
    expect(s.step).toBe("arquivo");
    expect(s.scanning).toBeUndefined();
    expect(s.file).toBeUndefined();
    expect(s.error).toBe("Arquivo não encontrado: /x.mbox");
  });

  it("voltar às pastas depois de falha e recomeçar", () => {
    const failed = reduce(converting(), { event: "error", message: "x" });
    const back = reduce(failed, { type: "backToFolders" });
    expect(back.step).toBe("pastas");
    expect(back.failure).toBeUndefined();
    expect(reduce(back, { type: "reset" })).toEqual(initialState());
  });
});
