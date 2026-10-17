import type { Core } from "./core";
import { initialState, reduce, type UiAction, type WizardState } from "./state";
import type { Command, CoreEvent } from "./types";
import { dialog } from "./components/dialog";
import { h } from "./dom";
import { renderArquivo } from "./screens/arquivo";
import { renderPastas } from "./screens/pastas";
import { renderConverter } from "./screens/converter";
import { renderResultado } from "./screens/resultado";

export interface DropEvent {
  type: "over" | "leave" | "drop";
  path?: string;
}

export interface Services {
  chooseFile(): Promise<string | null>;
  chooseOutput(suggestedName: string): Promise<{ directory: string; name: string } | null>;
  revealInFolder(path: string): Promise<void>;
  openFile(path: string): Promise<void>;
  onFileDrop(handler: (e: DropEvent) => void): void;
  onCloseRequested(handler: () => boolean): void;
  closeWindow(): Promise<void>;
}

export interface Ui {
  state: WizardState;
  dragging: boolean;
  dispatch(action: UiAction): void;
  chooseFile(): Promise<void>;
  convert(): Promise<void>;
  cancel(): void;
  requestChangeFile(): void;
  services: Services;
}

const PREVIEW_DELAY_MS = 150;

export function mount(root: HTMLElement, core: Core, services: Services): Ui {
  let previewTimer: ReturnType<typeof setTimeout> | undefined;
  let choosingOutput = false;
  let closeAfterCancel = false;

  const converting = () => ui.state.step === "converter" && !ui.state.failure;

  function send(command: Command) {
    core.send(command).catch((error: unknown) => handle({ event: "error", message: String(error) }));
  }

  function handle(e: CoreEvent) {
    ui.state = reduce(ui.state, e);
    render();
    if (e.event === "cancelled" && closeAfterCancel) void services.closeWindow();
  }

  const ui: Ui = {
    state: initialState(),
    dragging: false,
    services,
    dispatch(action) {
      ui.state = reduce(ui.state, action);
      if (action.type === "setDecision" || action.type === "restoreDecision" || action.type === "setCopyPolicy") schedulePreview();
      render();
    },
    async chooseFile() {
      const path = await services.chooseFile();
      if (path) startScan(path);
    },
    async convert() {
      const mapping = ui.state.mapping;
      if (!mapping || choosingOutput || converting()) return;
      choosingOutput = true;
      let target: { directory: string; name: string } | null;
      try {
        target = await services.chooseOutput(mapping.storeName);
      } finally {
        choosingOutput = false;
      }
      if (!target || converting()) return;
      ui.dispatch({ type: "startConvert", directory: target.directory, name: target.name });
      send({ cmd: "convert", outputDirectory: target.directory, outputName: target.name, mapping: { ...mapping, storeName: target.name }, maxPartMB: ui.state.maxPartGB * 1024 });
    },
    cancel() {
      send({ cmd: "cancel" });
    },
    requestChangeFile() {
      const d = dialog("Trocar de arquivo?", h("p", {}, "As escolhas de pastas deste arquivo serão perdidas."), [
        h("button", { class: "btn", type: "button", onclick: () => d.close() }, "Continuar com este arquivo"),
        h("button", { class: "btn btn-primario", type: "button", onclick: () => { d.close(); void ui.chooseFile(); } }, "Trocar arquivo"),
      ]);
      d.open();
    },
  };

  function startScan(path: string) {
    ui.dispatch({ type: "chooseFile", path });
    send({ cmd: "scan", path });
  }

  function schedulePreview() {
    clearTimeout(previewTimer);
    previewTimer = setTimeout(() => {
      if (ui.state.mapping) send({ cmd: "preview", mapping: ui.state.mapping });
    }, PREVIEW_DELAY_MS);
  }

  function focusKey(): string | null {
    const active = document.activeElement as HTMLElement | null;
    if (!active || !root.contains(active)) return null;
    if (active.dataset.key) return `key:${active.dataset.key}`;
    return active.tagName === "BUTTON" ? `text:${active.textContent}` : null;
  }

  function restoreFocus(key: string | null) {
    if (!key) return;
    const target = key.startsWith("key:")
      ? [...root.querySelectorAll<HTMLElement>("[data-key]")].find((el) => el.dataset.key === key.slice(4))
      : [...root.querySelectorAll<HTMLElement>("button")].find((el) => el.textContent === key.slice(5));
    target?.focus({ preventScroll: true });
  }

  function render() {
    const focused = focusKey();
    const scrolls = [...root.querySelectorAll<HTMLElement>("[data-scroll]")].map((el) => [el.dataset.scroll!, el.scrollTop] as const);
    const view =
      ui.state.step === "arquivo" ? renderArquivo(ui)
      : ui.state.step === "pastas" ? renderPastas(ui)
      : ui.state.step === "converter" ? renderConverter(ui)
      : renderResultado(ui);
    root.replaceChildren(view);
    for (const [key, top] of scrolls) {
      const el = root.querySelector<HTMLElement>(`[data-scroll="${key}"]`);
      if (el) el.scrollTop = top;
    }
    restoreFocus(focused);
  }

  core.onEvent(handle);

  services.onCloseRequested(() => {
    if (!converting()) return true;
    const d = dialog("Cancelar a conversão e fechar?", h("p", {}, "A conversão em andamento será cancelada e o arquivo PST parcial, apagado."), [
      h("button", { class: "btn", type: "button", onclick: () => d.close() }, "Continuar convertendo"),
      h("button", { class: "btn btn-perigo-cheio", type: "button", onclick: () => { d.close(); closeAfterCancel = true; ui.cancel(); } }, "Cancelar e fechar"),
    ]);
    d.open();
    return false;
  });

  services.onFileDrop((e) => {
    if (ui.state.step !== "arquivo" || ui.state.scanning) return;
    if (e.type === "drop" && e.path) {
      ui.dragging = false;
      startScan(e.path);
      return;
    }
    ui.dragging = e.type === "over";
    render();
  });

  render();
  return ui;
}
