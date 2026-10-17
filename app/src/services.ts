import type { DropEvent, Services } from "./app";

function split(path: string): { directory: string; name: string } {
  const index = Math.max(path.lastIndexOf("/"), path.lastIndexOf("\\"));
  const file = path.slice(index + 1);
  return { directory: path.slice(0, index), name: file.replace(/\.pst$/i, "") };
}

async function tauriServices(): Promise<Services> {
  const dialog = await import("@tauri-apps/plugin-dialog");
  const opener = await import("@tauri-apps/plugin-opener");
  const { getCurrentWebview } = await import("@tauri-apps/api/webview");
  const { getCurrentWindow } = await import("@tauri-apps/api/window");
  return {
    async chooseFile() {
      const path = await dialog.open({ multiple: false, directory: false, title: "Escolha o arquivo do Gmail", filters: [{ name: "Arquivo do Gmail", extensions: ["mbox"] }] });
      return typeof path === "string" ? path : null;
    },
    async chooseOutput(suggestedName) {
      const path = await dialog.save({ title: "Salvar o arquivo PST", defaultPath: `${suggestedName}.pst`, filters: [{ name: "Arquivo do Outlook", extensions: ["pst"] }] });
      return path ? split(path) : null;
    },
    revealInFolder: (path) => opener.revealItemInDir(path),
    openFile: (path) => opener.openPath(path),
    onCloseRequested(handler) {
      void getCurrentWindow().onCloseRequested((event) => {
        if (!handler()) event.preventDefault();
      });
    },
    closeWindow: () => getCurrentWindow().destroy(),
    onFileDrop(handler: (e: DropEvent) => void) {
      void getCurrentWebview().onDragDropEvent((event) => {
        const p = event.payload;
        if (p.type === "over" || p.type === "enter") handler({ type: "over" });
        else if (p.type === "leave") handler({ type: "leave" });
        else if (p.type === "drop") handler({ type: "drop", path: p.paths.find((f) => f.toLowerCase().endsWith(".mbox")) ?? p.paths[0] });
      });
    },
  };
}

function browserServices(): Services {
  return {
    chooseFile: async () => "/home/ana/Downloads/Takeout/Mail/Todos os e-mails, incluindo Spam e Lixeira.mbox",
    chooseOutput: async (suggestedName) => ({ directory: "/home/ana/Documentos", name: suggestedName }),
    revealInFolder: async () => undefined,
    openFile: async () => undefined,
    onFileDrop: () => undefined,
    onCloseRequested: () => undefined,
    closeWindow: async () => undefined,
  };
}

export async function services(): Promise<Services> {
  return window.__TAURI_INTERNALS__ ? tauriServices() : browserServices();
}
