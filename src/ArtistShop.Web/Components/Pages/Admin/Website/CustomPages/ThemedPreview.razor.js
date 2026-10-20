// <themed-preview>: its Desktop and Phone buttons, marked data-preview-width, set the width of the
// <row-preview-frame> inside, which fits itself to it. data-shows-phone on itself narrows the box to the phone.
// Inside an island, Blazor leaves these attributes alone, since what it renders for them never changes.
// An enhanced load patches the attributes back to Desktop rather than making a new element, so the width
// remembered is pressed again
import { pressWidthButton, rememberedWidthButton, rememberPreviewWidth } from "/js/preview-width.js";

class ThemedPreview extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    this.addEventListener("click", (event) => this.#choose(event), { signal: this.#listeners.signal });
    this.showRememberedWidth();
  }

  disconnectedCallback() {
    this.#listeners?.abort();
  }

  showRememberedWidth() {
    const button = rememberedWidthButton(this);

    if (button instanceof HTMLElement) {
      this.#show(button);
    }
  }

  /** @param {MouseEvent} event */
  #choose(event) {
    const chosen = event.target instanceof Element ? event.target.closest("[data-preview-width]") : null;

    if (!(chosen instanceof HTMLElement) || chosen.dataset.previewWidth === undefined) {
      return;
    }

    rememberPreviewWidth(chosen.dataset.previewWidth);
    this.#show(chosen);
  }

  /** @param {HTMLElement} chosen */
  #show(chosen) {
    const width = chosen.dataset.previewWidth;

    if (width === undefined) {
      return;
    }

    pressWidthButton(this, chosen);
    this.toggleAttribute("data-shows-phone", chosen.hasAttribute("data-phone"));
    this.querySelector("row-preview-frame")?.setAttribute("data-width", width);
  }
}

customElements.define("themed-preview", ThemedPreview);

Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("themed-preview").forEach((element) => {
    if (element instanceof ThemedPreview) {
      element.showRememberedWidth();
    }
  })
);
