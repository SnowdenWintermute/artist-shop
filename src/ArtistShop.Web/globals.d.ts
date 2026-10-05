// Types for what the browser scripts use without importing it. Written by hand, covering only what
// the scripts call

// defined by blazor.web.js, which App.razor loads before every script
declare const Blazor: {
  addEventListener(
    name: "enhancedload" | "enhancednavigationstart" | "enhancednavigationend",
    callback: () => void
  ): void;
  // an enhanced navigation on a static page, as following a link would be
  navigateTo(url: string): void;
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
        history?: { userOnly: boolean };
        // a dropped or pasted file of one of mimetypes goes to handler, with where it goes
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
  hasFocus(): boolean;
  // with focus true, the editor takes the focus first, so there is always a selection
  getSelection(focus: true): { index: number; length: number };
  setSelection(index: number, length: number, source?: QuillSource): void;
  getIndex(blot: QuillBlot): number;
  getLine(index: number): [QuillBlot | null, number];
  getModule(name: "toolbar"): QuillToolbar;
  static import(path: "formats/link"): typeof QuillLink;
  static import(path: "blots/block/embed"): typeof QuillBlot;
  static import(path: "delta"): typeof QuillDelta;
  // each format's toolbar icon as SVG markup, or one per value, such as align's "", "center" and "right"
  static import(path: "ui/icons"): { image: string; video: string; [format: string]: string | Record<string, string> };
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

// wwwroot/lib/photoswipe, which ImageLightbox.razor.js imports the first time a lightbox opens.
// Only what the lightbox uses
type PhotoSwipeSlide =
  | { src: string; srcset: string; width: number; height: number; alt: string; msrc?: string }
  | { html: string };

type PhotoSwipeOptions = {
  index: number;
  appendToEl: HTMLElement;
  loop: boolean;
  bgOpacity: number;
  showHideAnimationType: "fade";
  showAnimationDuration: number;
  hideAnimationDuration: number;
  arrowPrev: boolean;
  arrowNext: boolean;
  close: boolean;
  zoom: boolean;
  counter: boolean;
  arrowKeys: boolean;
  escKey: boolean;
  trapFocus: boolean;
  returnFocus: boolean;
  tapAction: "close";
  paddingFn: () => { top: number; right: number; bottom: number; left: number };
};

type PhotoSwipe = {
  currIndex: number;
  // where a move is heading, set as soon as it starts, where currIndex waits for it to settle
  potentialIndex: number;
  init(): void;
  prev(): void;
  next(): void;
  // with PhotoSwipe's closing fade, where destroy is at once. Either ends in "destroy"
  close(): void;
  destroy(): void;
  refreshSlideContent(index: number): void;
  addFilter(name: "numItems", filter: () => number): void;
  addFilter(name: "itemData", filter: (itemData: PhotoSwipeSlide, index: number) => PhotoSwipeSlide): void;
  on(name: "change" | "destroy" | "moveMainScroll", callback: () => void): void;
};

type PhotoSwipeModule = { default: new (options: PhotoSwipeOptions) => PhotoSwipe };

// <image-lightbox>, from Components/Dialogs/ImageLightbox.razor.js, as the pages that hold one
// call it. Slides are numbered, and not all of them need be known yet: slideAt is null for one
// still coming, and counterAt null hides the counter
type LightboxSlides = {
  itemCount: number;
  slideAt(index: number): PhotoSwipeSlide | null;
  counterAt(index: number): string | null;
};

interface ImageLightboxElement extends HTMLElement {
  open(pictures: HTMLImageElement[], index: number): void;
  openSlides(slides: LightboxSlides, index: number): void;
  refreshSlide(index: number): void;
}

interface HTMLElementTagNameMap {
  "image-lightbox": ImageLightboxElement;
}

// blazor.web.js raises this on ReconnectModal.razor's dialog as the connection comes and goes
interface HTMLElementEventMap {
  "components-reconnect-state-changed": CustomEvent<{ state: string }>;
}
