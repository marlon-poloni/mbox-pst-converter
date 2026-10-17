import { h } from "../dom";

export interface Option<T extends string> {
  value: T;
  label: string;
}

export function segmented<T extends string>(options: Option<T>[], value: T, onChange: (value: T) => void, label?: string): HTMLElement {
  const box = h("span", { class: "seg", role: "group", "aria-label": label });
  const buttons = options.map((o) =>
    h("button", { type: "button", class: o.value === value ? "on" : "", "aria-pressed": String(o.value === value) }, o.label),
  );
  buttons.forEach((button, i) =>
    button.addEventListener("click", () => {
      buttons.forEach((b, j) => {
        b.classList.toggle("on", i === j);
        b.setAttribute("aria-pressed", String(i === j));
      });
      onChange(options[i].value);
    }),
  );
  box.append(...buttons);
  return box;
}
