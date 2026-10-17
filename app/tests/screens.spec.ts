import { mount, type Services } from "../src/app";
import type { Core } from "../src/core";
import type { Command, CoreEvent, MappingDto, PreviewDto } from "../src/types";

const mapping: MappingDto = {
  storeName: "Todos os e-mails",
  copyPolicy: "eachFolder",
  decisions: {
    "Caixa de entrada": { kind: "folder", folderPath: ["Caixa de Entrada"] },
    Spam: { kind: "folder", folderPath: ["Lixo Eletrônico"] },
    Lixeira: { kind: "folder", folderPath: ["Itens Excluídos"] },
    Fornecedores: { kind: "folder", folderPath: ["Fornecedores"] },
    Processos: { kind: "folder", folderPath: ["Processos"] },
    "Não lidas": { kind: "mark", folderPath: [] },
    "Categoria: pessoal": { kind: "ignoreLabel", folderPath: [] },
  },
  userPriority: ["Fornecedores", "Processos"],
};
const preview: PreviewDto = {
  root: {
    name: "Todos os e-mails", path: [], messages: 0, children: [
      { name: "Caixa de Entrada", path: ["Caixa de Entrada"], messages: 5773, children: [] },
      { name: "Fornecedores", path: ["Fornecedores"], messages: 46, children: [] },
    ],
  },
  sourceMessages: 6614, messagesInPst: 6954, extraCopies: 340, messagesInMoreThanOneFolder: 328, excluded: 0, estimatedBytes: 5368709120, folderCount: 13,
};
const scanDone: CoreEvent = {
  event: "scanDone", path: "/home/ana/Takeout/todos.mbox", fileName: "todos.mbox", messages: 6614, sourceBytes: 6654983453,
  labels: [
    { name: "Caixa de entrada", kind: "inbox", count: 5773 }, { name: "Spam", kind: "spam", count: 76 }, { name: "Lixeira", kind: "trash", count: 7 },
    { name: "Fornecedores", kind: "user", count: 46 }, { name: "Processos", kind: "user", count: 17 }, { name: "Não lidas", kind: "unread", count: 1068 },
    { name: "Categoria: pessoal", kind: "category", count: 3382 },
  ],
  mapping, preview,
};

class FakeCore implements Core {
  sent: Command[] = [];
  private handler?: (e: CoreEvent) => void;
  async send(command: Command) { this.sent.push(command); }
  onEvent(handler: (e: CoreEvent) => void) { this.handler = handler; return () => undefined; }
  emit(e: CoreEvent) { this.handler!(e); }
  last<T extends Command["cmd"]>(cmd: T) { return [...this.sent].reverse().find((c) => c.cmd === cmd) as Extract<Command, { cmd: T }>; }
}

let core: FakeCore;
let services: Services & { opened: string[]; revealed: string[]; closed: number; closeHandler?: () => boolean };
const $ = <T extends Element = HTMLElement>(sel: string) => document.querySelector<T & HTMLElement>(sel)!;
const $$ = (sel: string) => [...document.querySelectorAll<HTMLElement>(sel)];
const button = (text: string) => $$("button").find((b) => b.textContent?.trim() === text)!;
const flush = async () => { for (let i = 0; i < 5; i++) await Promise.resolve(); };

async function start() {
  document.body.innerHTML = '<div id="app"></div>';
  core = new FakeCore();
  services = {
    opened: [], revealed: [], closed: 0,
    chooseFile: async () => "/home/ana/Takeout/todos.mbox",
    chooseOutput: async (suggested) => ({ directory: "/home/ana/Documentos", name: suggested }),
    revealInFolder: async (p) => { services.revealed.push(p); },
    openFile: async (p) => { services.opened.push(p); },
    onFileDrop: () => undefined,
    onCloseRequested: (handler) => { services.closeHandler = handler; },
    closeWindow: async () => { services.closed++; },
  };
  mount($("#app"), core, services);
}

async function toFolders() {
  await start();
  button("Escolher arquivo").click();
  await flush();
  core.emit(scanDone);
  button("Continuar").click();
}

describe("telas", () => {
  beforeEach(() => vi.useRealTimers());

  it("Arquivo: escolher manda ler e mostra o progresso", async () => {
    await start();
    expect($("h1").textContent).toBe("Escolha o arquivo do Gmail");
    button("Escolher arquivo").click();
    await flush();
    expect(core.last("scan")).toEqual({ cmd: "scan", path: "/home/ana/Takeout/todos.mbox" });
    core.emit({ event: "scanProgress", bytesRead: 3327491726, sourceBytes: 6654983453 });
    expect($(".progresso > i").style.width).toBe("50%");
    expect($(".progresso-texto").textContent).toContain("3,1 de 6,2 GB lidos");
    button("Cancelar leitura").click();
    expect(core.last("cancel")).toEqual({ cmd: "cancel" });
  });

  it("Arquivo pronto mostra os números e segue para Pastas", async () => {
    await start();
    button("Escolher arquivo").click();
    await flush();
    core.emit(scanDone);
    expect($(".numeros").textContent).toContain("6.614");
    expect($(".numeros").textContent).toContain("1.068");
    button("Continuar").click();
    expect($(".etapas .atual").textContent).toBe("Pastas");
    expect($$(".lista .item")).toHaveLength(7);
  });

  it("Pastas: clicar num marcador destaca a pasta e abre o editor", async () => {
    await toFolders();
    $$(".item-principal").find((i) => i.textContent!.includes("Fornecedores"))!.click();
    expect($(".no.realce").textContent).toContain("Fornecedores");
    expect($("[role=dialog] h2").textContent).toBe("Destino do marcador");
    expect(($("[role=dialog] input") as HTMLInputElement).value).toBe("Fornecedores");
  });

  it("Editor: nome vazio mostra erro e não fecha", async () => {
    await toFolders();
    $$(".item-principal").find((i) => i.textContent!.includes("Processos"))!.click();
    const input = $("[role=dialog] input") as HTMLInputElement;
    input.value = "  ";
    input.dispatchEvent(new Event("input"));
    button("Aplicar").click();
    expect($("[role=dialog] .erro").textContent).toBe("Dê um nome para a pasta.");
    expect($$(".veu")).toHaveLength(1);
  });

  it("Editor: mover para dentro de outra pasta manda a prévia", async () => {
    vi.useFakeTimers();
    await toFolders();
    $$(".item-principal").find((i) => i.textContent!.includes("Fornecedores"))!.click();
    const select = $("[role=dialog] select") as HTMLSelectElement;
    select.value = "Caixa de Entrada";
    select.dispatchEvent(new Event("change"));
    button("Aplicar").click();
    expect($$(".veu")).toHaveLength(0);
    vi.advanceTimersByTime(200);
    expect(core.last("preview").mapping.decisions.Fornecedores).toEqual({ kind: "folder", folderPath: ["Caixa de Entrada", "Fornecedores"] });
  });

  it("Editor: Não converter explica o efeito e aplica", async () => {
    vi.useFakeTimers();
    await toFolders();
    $$(".item-principal").find((i) => i.textContent!.includes("Processos"))!.click();
    $$("[role=dialog] button").find((b) => b.textContent === "Não converter")!.click();
    expect($("[role=dialog] .explica").textContent).toContain("Os 17 e-mails com este marcador ficam fora do arquivo PST");
    button("Aplicar").click();
    vi.advanceTimersByTime(200);
    expect(core.last("preview").mapping.decisions.Processos.kind).toBe("exclude");
  });

  it("Spam e Lixeira têm Não converter à vista, e dá para voltar atrás", async () => {
    vi.useFakeTimers();
    await toFolders();
    const spam = () => $$(".lista .item").find((i) => i.textContent!.includes("Spam"))!;
    expect(spam().querySelector<HTMLElement>(".btn-peq")!.textContent).toBe("Não converter");
    expect($$(".lista .item").find((i) => i.textContent!.includes("Lixeira"))!.querySelector<HTMLElement>(".btn-peq")!.textContent).toBe("Não converter");
    expect($$(".lista .item").find((i) => i.textContent!.includes("Fornecedores"))!.querySelector<HTMLElement>(".btn-peq")).toBeNull();

    spam().querySelector<HTMLElement>(".btn-peq")!.click();
    expect($$(".veu")).toHaveLength(0);
    expect(spam().querySelector(".fora")!.textContent).toBe("Fica fora do arquivo");
    vi.advanceTimersByTime(200);
    expect(core.last("preview").mapping.decisions.Spam.kind).toBe("exclude");

    spam().querySelector<HTMLElement>(".btn-peq")!.click();
    expect(spam().textContent).toContain("Lixo Eletrônico");
  });

  it("Aviso de cópias troca a política", async () => {
    vi.useFakeTimers();
    await toFolders();
    expect($(".aviso").textContent).toContain("328 e-mails");
    expect($(".aviso").textContent).toContain("somando 340 cópias");
    button("Só na primeira pasta").click();
    vi.advanceTimersByTime(200);
    expect(core.last("preview").mapping.copyPolicy).toBe("firstFolderOnly");
  });

  it("Tamanho dos arquivos: valida e muda o rótulo do botão", async () => {
    await toFolders();
    button("Dividir a cada 20 GB").click();
    const input = $("[role=dialog] input") as HTMLInputElement;
    input.value = "0";
    input.dispatchEvent(new Event("input"));
    button("Aplicar").click();
    expect($("[role=dialog] .erro").textContent).toBe("Use um número inteiro entre 1 e 50.");
    input.value = "5";
    input.dispatchEvent(new Event("input"));
    button("Aplicar").click();
    expect(button("Dividir a cada 5 GB")).toBeDefined();
  });

  it("Converter pede onde salvar e manda converter", async () => {
    await toFolders();
    button("Converter").click();
    await flush();
    expect(core.last("convert")).toEqual({ cmd: "convert", outputDirectory: "/home/ana/Documentos", outputName: "Todos os e-mails", mapping, maxPartMB: 20480 });
    expect($(".etapas .atual").textContent).toBe("Converter");
  });

  it("Converter: progresso e cancelamento com confirmação", async () => {
    await toFolders();
    button("Converter").click();
    await flush();
    core.emit({ event: "convertProgress", done: 3412, total: 6614, bytesRead: 3435973837, sourceBytes: 6654983453, megabytesPerSecond: 138, remainingSeconds: 23 });
    expect($(".contagem").textContent).toBe("3.412 de 6.614 e-mails");
    expect($(".progresso-texto").textContent).toContain("faltam cerca de 23 s");
    button("Cancelar conversão").click();
    expect($("[role=dialog] h2").textContent).toBe("Cancelar a conversão?");
    expect(core.sent.some((c) => c.cmd === "cancel")).toBe(false);
    $$("[role=dialog] button").find((b) => b.textContent === "Cancelar conversão")!.click();
    expect(core.last("cancel")).toEqual({ cmd: "cancel" });
    core.emit({ event: "cancelled" });
    expect($(".etapas .atual").textContent).toBe("Pastas");
  });

  it("Resultado: tabela por pasta, diferença destacada e ações", async () => {
    await toFolders();
    button("Converter").click();
    await flush();
    core.emit({
      event: "convertDone", outputFiles: ["/home/ana/Documentos/Todos os e-mails.pst"], outputSizes: [5261334938], reportPath: "/home/ana/Documentos/Todos os e-mails-relatorio.json",
      converted: 6612, excluded: 0, skipped: 2, skippedItems: [{ identifier: "e-mail 2.431", reason: "corrompido" }], warnings: [],
      checks: [{ path: ["Caixa de Entrada"], expected: 5773, found: 5771 }, { path: ["Clientes"], expected: 0, found: 0 }, { path: ["Fornecedores"], expected: 46, found: 46 }], verified: false, elapsedSeconds: 48,
    });
    expect($("h1").textContent).toBe("Conversão concluída com diferenças");
    expect($$(".tabela tbody tr")).toHaveLength(2);
    expect($(".tabela tr.diferente").textContent).toContain("Caixa de Entrada");
    expect($(".bloco").textContent).toContain("e-mail 2.431: corrompido");
    expect($(".lista-arquivos").textContent).toContain("4,9 GB");
    button("Abrir pasta do arquivo").click();
    button("Ver relatório").click();
    expect(services.revealed).toEqual(["/home/ana/Documentos/Todos os e-mails.pst"]);
    expect(services.opened).toEqual(["/home/ana/Documentos/Todos os e-mails-relatorio.txt"]);
    button("Converter outro arquivo").click();
    expect($("h1").textContent).toBe("Escolha o arquivo do Gmail");
  });

  it("Falha na conversão: mensagem, voltar e tentar de novo", async () => {
    await toFolders();
    button("Converter").click();
    await flush();
    core.emit({ event: "error", message: "Espaço em disco insuficiente em /home/ana/Documentos." });
    expect($("h1").textContent).toBe("A conversão não terminou");
    expect($(".falha .mensagem").textContent).toBe("Espaço em disco insuficiente em /home/ana/Documentos.");
    button("Converter de novo").click();
    await flush();
    expect(core.sent.filter((c) => c.cmd === "convert")).toHaveLength(2);
  });

  it("Erro na leitura aparece uma vez na etapa Arquivo", async () => {
    await start();
    button("Escolher arquivo").click();
    await flush();
    core.emit({ event: "error", message: "Arquivo não encontrado: /x.mbox" });
    expect($$(".aviso-erro")).toHaveLength(1);
    expect($(".aviso-erro").textContent).toBe("Arquivo não encontrado: /x.mbox");
    expect(button("Escolher arquivo")).toBeDefined();
  });

  it("Converter duas vezes seguidas manda uma conversão só", async () => {
    await toFolders();
    button("Converter").click();
    button("Converter").click();
    await flush();
    expect(core.sent.filter((c) => c.cmd === "convert")).toHaveLength(1);
    expect($("h1").textContent).toBe("Convertendo");
  });

  it("Falha ao falar com o conversor vira mensagem na tela", async () => {
    await start();
    core.send = async () => { throw "O conversor parou de responder. Feche e abra o app de novo."; };
    button("Escolher arquivo").click();
    await flush();
    expect($(".aviso-erro").textContent).toBe("O conversor parou de responder. Feche e abra o app de novo.");
    expect(button("Escolher arquivo")).toBeDefined();
  });

  it("Fechar a janela durante a conversão pede confirmação, cancela e só então fecha", async () => {
    await toFolders();
    expect(services.closeHandler!()).toBe(true);
    button("Converter").click();
    await flush();
    expect(services.closeHandler!()).toBe(false);
    expect($("[role=dialog] h2").textContent).toBe("Cancelar a conversão e fechar?");
    $$("[role=dialog] button").find((b) => b.textContent === "Cancelar e fechar")!.click();
    expect(core.last("cancel")).toEqual({ cmd: "cancel" });
    expect(services.closed).toBe(0);
    core.emit({ event: "cancelled" });
    await flush();
    expect(services.closed).toBe(1);
  });

  it("O foco volta ao marcador depois de aplicar no editor", async () => {
    await toFolders();
    const item = () => $$(".item-principal").find((i) => i.textContent!.includes("Fornecedores"))!;
    item().focus();
    item().click();
    button("Aplicar").click();
    expect((document.activeElement as HTMLElement).dataset.key).toBe("Fornecedores");
  });

  it("O botão Não converter é um botão próprio, fora do botão do marcador", async () => {
    await toFolders();
    const quick = $$(".lista .item").find((i) => i.textContent!.includes("Spam"))!.querySelector<HTMLElement>(".btn-peq")!;
    expect(quick.closest("[role=button]")).toBeNull();
    expect(quick.closest(".item-principal")).toBeNull();
    expect($$(".item-principal")[0].tagName).toBe("BUTTON");
  });

  it("Mudar a política atualiza o resumo quando a prévia chega", async () => {
    vi.useFakeTimers();
    await toFolders();
    button("Só na primeira pasta").click();
    vi.advanceTimersByTime(200);
    expect($(".resumo").classList.contains("pendente")).toBe(true);
    core.emit({ event: "preview", preview: { ...preview, messagesInPst: 6614, extraCopies: 0, messagesInMoreThanOneFolder: 0 } });
    expect($(".resumo").textContent).toContain("6.614");
    expect($(".resumo").classList.contains("pendente")).toBe(false);
  });

  it("Dois marcadores com o mesmo nome de pasta mandam o mesmo caminho", async () => {
    vi.useFakeTimers();
    await toFolders();
    for (const label of ["Fornecedores", "Processos"]) {
      $$(".item-principal").find((i) => i.textContent!.includes(label))!.click();
      const input = $("[role=dialog] input") as HTMLInputElement;
      input.value = "Parceiros";
      input.dispatchEvent(new Event("input"));
      button("Aplicar").click();
    }
    vi.advanceTimersByTime(200);
    const decisions = core.last("preview").mapping.decisions;
    expect(decisions.Fornecedores.folderPath).toEqual(["Parceiros"]);
    expect(decisions.Processos.folderPath).toEqual(["Parceiros"]);
  });

  it("Trocar de arquivo em Pastas pede confirmação", async () => {
    await toFolders();
    button("Trocar arquivo").click();
    expect($("[role=dialog] h2").textContent).toBe("Trocar de arquivo?");
    expect(core.sent.filter((c) => c.cmd === "scan")).toHaveLength(1);
    $$("[role=dialog] button").find((b) => b.textContent === "Trocar arquivo")!.click();
    await flush();
    expect(core.sent.filter((c) => c.cmd === "scan")).toHaveLength(2);
  });

  it("Falha por disco cheio orienta o que fazer", async () => {
    await toFolders();
    button("Converter").click();
    await flush();
    core.emit({ event: "error", message: "Espaço em disco insuficiente em /home/ana/Documentos." });
    expect($(".falha").textContent).toContain("Libere espaço nessa pasta ou escolha outra pasta");
  });

  it("O nome do arquivo de dados acompanha o nome escolhido ao salvar", async () => {
    await toFolders();
    services.chooseOutput = async () => ({ directory: "/home/ana/Documentos", name: "Caixa da Ana" });
    button("Converter").click();
    await flush();
    const convert = core.last("convert");
    expect(convert.outputName).toBe("Caixa da Ana");
    expect(convert.mapping.storeName).toBe("Caixa da Ana");
  });
});
