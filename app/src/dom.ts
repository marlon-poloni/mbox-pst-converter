type Child = Node | string | null | undefined | false;
type Attrs = Record<string, string | boolean | number | ((event: Event) => void) | undefined>;

export function h<K extends keyof HTMLElementTagNameMap>(tag: K, attrs: Attrs = {}, ...children: Child[]): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (value === undefined || value === false) continue;
    if (typeof value === "function") el.addEventListener(key.replace(/^on/, "").toLowerCase(), value);
    else if (key === "class") el.className = String(value);
    else el.setAttribute(key, value === true ? "" : String(value));
  }
  for (const child of children) {
    if (child === null || child === undefined || child === false) continue;
    el.append(child);
  }
  return el;
}

const svgNs = "http://www.w3.org/2000/svg";

export function icon(name: "pasta" | "copia" | "ok" | "atencao" | "x" | "mbox", size = 16): SVGSVGElement {
  const svg = document.createElementNS(svgNs, "svg");
  const paths: Record<string, string> = {
    pasta: '<path fill="currentColor" d="M1.5 3.5A1.5 1.5 0 0 1 3 2h3.2l1.6 1.6H13A1.5 1.5 0 0 1 14.5 5v7.5A1.5 1.5 0 0 1 13 14H3a1.5 1.5 0 0 1-1.5-1.5z"/>',
    copia: '<path fill="none" stroke="currentColor" stroke-width="1.6" d="M5.5 5.5h7v8h-7zM3.5 10.5v-8h7"/>',
    ok: '<path fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" d="M3.5 8.5l3 3 6-7"/>',
    atencao: '<path fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" d="M8 4v5M8 11.5v.5"/><circle cx="8" cy="8" r="6.5" fill="none" stroke="currentColor" stroke-width="1.4"/>',
    x: '<path fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" d="M4.5 4.5l7 7M11.5 4.5l-7 7"/>',
    mbox: '<path fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" d="M3 7.5 12 13l9-5.5M4.5 5h15A1.5 1.5 0 0 1 21 6.5v11a1.5 1.5 0 0 1-1.5 1.5h-15A1.5 1.5 0 0 1 3 17.5v-11A1.5 1.5 0 0 1 4.5 5z"/>',
  };
  svg.setAttribute("viewBox", name === "mbox" ? "0 0 24 24" : "0 0 16 16");
  svg.setAttribute("width", String(size));
  svg.setAttribute("height", String(size));
  svg.setAttribute("aria-hidden", "true");
  svg.innerHTML = paths[name];
  return svg;
}
