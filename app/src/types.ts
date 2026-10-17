export type DecisionKind = "folder" | "mark" | "ignoreLabel" | "exclude";
export type CopyPolicy = "eachFolder" | "firstFolderOnly";
export type LabelKind =
  | "inbox" | "sent" | "drafts" | "archived" | "spam" | "trash" | "category"
  | "unread" | "starred" | "forwarded" | "opened" | "important" | "user";

export interface DecisionDto {
  kind: DecisionKind;
  folderPath: string[];
}

export interface MappingDto {
  storeName: string;
  copyPolicy: CopyPolicy;
  decisions: Record<string, DecisionDto>;
  userPriority?: string[];
}

export interface FolderDto {
  name: string;
  path: string[];
  messages: number;
  children: FolderDto[];
}

export interface PreviewDto {
  root: FolderDto;
  sourceMessages: number;
  messagesInPst: number;
  extraCopies: number;
  messagesInMoreThanOneFolder: number;
  excluded: number;
  estimatedBytes: number;
  folderCount: number;
}

export interface LabelDto {
  name: string;
  kind: LabelKind;
  count: number;
}

export interface FolderCheckDto {
  path: string[];
  expected: number;
  found: number;
}

export interface SkippedItemDto {
  identifier: string;
  reason: string;
}

export type Command =
  | { cmd: "scan"; path: string }
  | { cmd: "preview"; mapping: MappingDto }
  | { cmd: "convert"; outputDirectory: string; outputName: string; mapping: MappingDto; maxPartMB: number }
  | { cmd: "cancel" };

export type CoreEvent =
  | { event: "scanProgress"; bytesRead: number; sourceBytes: number }
  | { event: "scanDone"; path: string; fileName: string; messages: number; sourceBytes: number; labels: LabelDto[]; mapping: MappingDto; preview: PreviewDto }
  | { event: "preview"; preview: PreviewDto }
  | { event: "convertProgress"; done: number; total: number; bytesRead: number; sourceBytes: number; megabytesPerSecond: number; remainingSeconds: number | null }
  | {
      event: "convertDone";
      outputFiles: string[];
      outputSizes?: number[];
      reportPath: string;
      converted: number;
      excluded: number;
      skipped: number;
      skippedItems: SkippedItemDto[];
      warnings: string[];
      checks: FolderCheckDto[];
      verified: boolean;
      elapsedSeconds: number;
    }
  | { event: "cancelled" }
  | { event: "error"; message: string };
