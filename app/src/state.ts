import type { CopyPolicy, CoreEvent, DecisionDto, LabelDto, MappingDto, PreviewDto } from "./types";

export type Step = "arquivo" | "pastas" | "converter" | "resultado";

export type ConvertResult = Extract<CoreEvent, { event: "convertDone" }>;
export type ConvertProgress = Extract<CoreEvent, { event: "convertProgress" }>;

export interface WizardState {
  step: Step;
  file?: { path: string; name: string };
  scanning?: { bytesRead: number; sourceBytes: number };
  scan?: { path: string; fileName: string; messages: number; sourceBytes: number; labels: LabelDto[] };
  mapping?: MappingDto;
  defaults?: MappingDto;
  preview?: PreviewDto;
  previewPending: boolean;
  selected?: string;
  maxPartGB: number;
  output?: { directory: string; name: string };
  progress?: ConvertProgress;
  result?: ConvertResult;
  failure?: string;
  error?: string;
}

export type UiAction =
  | { type: "chooseFile"; path: string }
  | { type: "continue" }
  | { type: "selectLabel"; label: string }
  | { type: "setDecision"; label: string; decision: DecisionDto }
  | { type: "restoreDecision"; label: string }
  | { type: "setCopyPolicy"; policy: CopyPolicy }
  | { type: "setMaxPartGB"; gb: number }
  | { type: "startConvert"; directory: string; name: string }
  | { type: "backToFolders" }
  | { type: "changeFile" }
  | { type: "reset" };

export const initialState = (): WizardState => ({ step: "arquivo", previewPending: false, maxPartGB: 20 });

const fileName = (path: string) => path.split(/[\\/]/).pop() ?? path;

function withDecision(state: WizardState, label: string, decision: DecisionDto): WizardState {
  if (!state.mapping) return state;
  return { ...state, mapping: { ...state.mapping, decisions: { ...state.mapping.decisions, [label]: decision } }, previewPending: true };
}

export function reduce(state: WizardState, input: UiAction | CoreEvent): WizardState {
  if ("type" in input) {
    switch (input.type) {
      case "chooseFile":
        return { ...initialState(), maxPartGB: state.maxPartGB, file: { path: input.path, name: fileName(input.path) }, scanning: { bytesRead: 0, sourceBytes: 0 } };
      case "continue":
        return state.scan ? { ...state, step: "pastas", error: undefined } : state;
      case "selectLabel":
        return { ...state, selected: input.label };
      case "setDecision":
        return withDecision(state, input.label, input.decision);
      case "restoreDecision": {
        const original = state.defaults?.decisions[input.label];
        return original ? withDecision(state, input.label, original) : state;
      }
      case "setCopyPolicy":
        return state.mapping ? { ...state, mapping: { ...state.mapping, copyPolicy: input.policy }, previewPending: true } : state;
      case "setMaxPartGB":
        return { ...state, maxPartGB: input.gb };
      case "startConvert":
        return { ...state, step: "converter", output: { directory: input.directory, name: input.name }, progress: undefined, result: undefined, failure: undefined, error: undefined };
      case "backToFolders":
        return { ...state, step: "pastas", failure: undefined, progress: undefined };
      case "changeFile":
        return { ...initialState(), maxPartGB: state.maxPartGB };
      case "reset":
        return initialState();
    }
  }

  switch (input.event) {
    case "scanProgress":
      return { ...state, scanning: { bytesRead: input.bytesRead, sourceBytes: input.sourceBytes } };
    case "scanDone":
      return {
        ...state,
        scanning: undefined,
        scan: { path: input.path, fileName: input.fileName, messages: input.messages, sourceBytes: input.sourceBytes, labels: input.labels },
        mapping: input.mapping,
        defaults: input.mapping,
        preview: input.preview,
        previewPending: false,
      };
    case "preview":
      return { ...state, preview: input.preview, previewPending: false, error: undefined };
    case "convertProgress":
      return { ...state, progress: input };
    case "convertDone":
      return { ...state, step: "resultado", result: input, progress: undefined };
    case "cancelled":
      if (state.step === "converter") return { ...state, step: "pastas", progress: undefined };
      return { ...state, file: undefined, scanning: undefined };
    case "error":
      if (state.step === "converter") return { ...state, failure: input.message, progress: undefined };
      if (state.scanning) return { ...state, scanning: undefined, file: undefined, error: input.message };
      return { ...state, error: input.message };
  }
}
