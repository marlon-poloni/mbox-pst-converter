import type { Command, CoreEvent } from "./types";

type Handler = (event: CoreEvent) => void;

export interface Core {
  send(command: Command): Promise<void>;
  onEvent(handler: Handler): () => void;
}

declare global {
  interface Window {
    __TAURI_INTERNALS__?: unknown;
  }
}

async function tauriCore(): Promise<Core> {
  const { invoke } = await import("@tauri-apps/api/core");
  const { listen } = await import("@tauri-apps/api/event");
  const handlers = new Set<Handler>();
  await listen<string>("core-event", (e) => {
    const parsed = JSON.parse(e.payload) as CoreEvent;
    handlers.forEach((h) => h(parsed));
  });
  await listen("core-exit", () => {
    const parsed: CoreEvent = { event: "error", message: "O conversor parou de responder. Feche e abra o app de novo." };
    handlers.forEach((h) => h(parsed));
  });
  return {
    send: (command) => invoke("core_send", { line: JSON.stringify(command) }),
    onEvent(handler) {
      handlers.add(handler);
      return () => handlers.delete(handler);
    },
  };
}

export async function connect(): Promise<Core> {
  if (window.__TAURI_INTERNALS__) return tauriCore();
  if (!import.meta.env.DEV) throw new Error("O conversor só funciona dentro do app.");
  const { mockCore } = await import("./mock-core");
  return mockCore();
}
