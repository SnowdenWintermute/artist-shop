// Types for what the browser scripts use without importing it. Written by hand, covering only what
// the scripts call, since the real types come in npm packages and this project has no Node toolchain

// defined by blazor.web.js, which App.razor loads before every script
declare const Blazor: {
  addEventListener(name: "enhancedload" | "enhancednavigationstart", callback: () => void): void;
  reconnect(): Promise<boolean>;
  resumeCircuit(): Promise<boolean>;
};

// Quill is defined by wwwroot/lib/quill/quill.js, which PostBodyEditor.razor.js loads
type QuillSource = "api" | "user" | "silent";

type QuillOperation = {
  insert?: string | Record<string, unknown>;
  delete?: number;
  retain?: number;
  attributes?: Record<string, unknown>;
};

declare class QuillDelta {
  constructor(ops?: QuillOperation[]);
  ops: QuillOperation[];
  retain(length: number): this;
  delete(length: number): this;
  insert(value: string | Record<string, unknown>): this;
  // where index ends up once this change is applied
  transformPosition(index: number): number;
}

// a piece of the document, which Quill keeps in step with its DOM node
declare class QuillBlot {
  static blotName: string;
  static tagName: string;
  static className: string;
  static create(value?: unknown): Node;
  static value(node: Node): unknown;
  domNode: Node;
}

declare class QuillLink extends QuillBlot {
  static sanitize(url: string): string;
}

type QuillToolbar = {
  container: HTMLElement;
};

declare class Quill {
  constructor(
    container: HTMLElement,
    options: {
      theme?: string;
      formats?: string[];
      modules?: {
        toolbar?: { container: unknown[]; handlers?: Record<string, () => void> };
        // a dropped or pasted file of one of mimetypes goes to handler, with where it goes
        history?: { userOnly: boolean };
        uploader?: { mimetypes: string[]; handler: (range: { index: number; length: number }, files: File[]) => void };
      };
    }
  );
  root: HTMLElement;
  setContents(delta: QuillDelta | { ops: QuillOperation[] }, source?: QuillSource): void;
  getContents(): QuillDelta;
  updateContents(delta: QuillDelta, source?: QuillSource): void;
  insertEmbed(index: number, type: string, value: unknown, source?: QuillSource): void;
  on(event: "text-change", handler: (change: QuillDelta, old: QuillDelta, source: QuillSource) => void): this;
  off(event: "text-change", handler: (change: QuillDelta, old: QuillDelta, source: QuillSource) => void): this;
  focus(): void;
  // with focus true, the editor takes the focus first, so there is always a selection
  getSelection(focus: true): { index: number; length: number };
  setSelection(index: number, length: number, source?: QuillSource): void;
  getIndex(blot: QuillBlot): number;
  getLine(index: number): [QuillBlot | null, number];
  getModule(name: "toolbar"): QuillToolbar;
  static import(path: "formats/link"): typeof QuillLink;
  static import(path: "blots/block/embed"): typeof QuillBlot;
  static import(path: "delta"): typeof QuillDelta;
  static register(blot: typeof QuillBlot, overwrite?: boolean): void;
  static find(node: Node): QuillBlot | Quill | null;
}

// wwwroot/lib/floating-ui, which PostArtworkEmbed.razor.js imports when an editor connects
type FloatingMiddleware = { name: string };

type FloatingUi = {
  computePosition(
    reference: Element,
    floating: HTMLElement,
    options: {
      placement?: "top" | "bottom" | "left" | "right";
      strategy?: "absolute" | "fixed";
      middleware?: FloatingMiddleware[];
    }
  ): Promise<{ x: number; y: number }>;
  // calls update now and whenever the reference moves or resizes; returns what stops it
  autoUpdate(reference: Element, floating: HTMLElement, update: () => void): () => void;
  offset(distance: number): FloatingMiddleware;
  flip(): FloatingMiddleware;
  shift(options?: { padding?: number }): FloatingMiddleware;
};
