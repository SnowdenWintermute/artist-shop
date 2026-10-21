// <editor-workspace>: its divider (data-part="divider") is dragged, or moved with the arrow keys, to share the width
// between the controls and the preview. The controls' width is kept for this viewer, so every editor opens with it.
// It's set in a sheet of the document's own rather than the element's style attribute, which an enhanced load
// patches away; set again only on enhancedload, the controls sometimes flashed at the starting width
const storageKey = "editor-workspace-controls-width";

const widthSheet = new CSSStyleSheet();
document.adoptedStyleSheets = [...document.adoptedStyleSheets, widthSheet];

// neither side narrower than this
const minimumPixels = 320;

// how far an arrow key moves the divider
const keyStep = 32;

/** @returns {number | null} */
function rememberedWidth() {
  try {
    const width = Number(localStorage.getItem(storageKey));
    return width > 0 ? width : null;
  } catch {
    return null;
  }
}

/** @param {number} width */
function rememberWidth(width) {
  try {
    localStorage.setItem(storageKey, String(Math.round(width)));
  } catch {
    // a blocked storage only means the next editor opens at the starting split
  }
}

class EditorWorkspace extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    const signal = this.#listeners.signal;
    this.addEventListener("pointerdown", (event) => this.#grab(event), { signal });
    this.addEventListener("keydown", (event) => this.#nudge(event), { signal });
    this.#restore();
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  #restore() {
    const width = rememberedWidth();

    if (width !== null) {
      this.#set(width);
    }
  }

  get #controls() {
    return this.querySelector("main")?.getBoundingClientRect().width ?? 0;
  }

  // kept within the workspace as it is now, so a narrower window never hides a side
  /** @param {number} width */
  #set(width) {
    const fitted = Math.max(minimumPixels, Math.min(width, this.getBoundingClientRect().width - minimumPixels));
    widthSheet.replaceSync(`editor-workspace { --controls-width: ${fitted}px; }`);
    return fitted;
  }

  /** @param {PointerEvent} event */
  #grab(event) {
    const divider = event.target instanceof Element ? event.target.closest("[data-part=divider]") : null;

    if (!(divider instanceof HTMLElement) || event.button !== 0) {
      return;
    }

    // captured, so the pointer passing over the preview's frame doesn't take the moves away
    event.preventDefault();
    divider.setPointerCapture(event.pointerId);
    const startX = event.clientX;
    const startWidth = this.#controls;
    const moves = new AbortController();

    divider.addEventListener("pointermove", (move) => this.#set(startWidth + move.clientX - startX), { signal: moves.signal });
    const release = () => {
      rememberWidth(this.#controls);
      moves.abort();
    };
    divider.addEventListener("pointerup", release, { signal: moves.signal });
    divider.addEventListener("pointercancel", release, { signal: moves.signal });
  }

  /** @param {KeyboardEvent} event */
  #nudge(event) {
    const isDivider = event.target instanceof Element && event.target.matches("[data-part=divider]");
    const step = event.key === "ArrowLeft" ? -keyStep : event.key === "ArrowRight" ? keyStep : 0;

    if (!isDivider || step === 0) {
      return;
    }

    event.preventDefault();
    rememberWidth(this.#set(this.#controls + step));
  }
}

customElements.define("editor-workspace", EditorWorkspace);
