// <themed-preview>: its Desktop and Phone buttons, marked data-preview-width, set the width of the
// <row-preview-frame> inside, which fits itself to it. data-shows-phone on itself narrows the box to the phone.
// Inside an island, Blazor leaves these attributes alone, since what it renders for them never changes.
// The choice is kept for the tab, since choosing a layout or a collection loads the page again, and an
// enhanced load patches the attributes back to Desktop rather than making a new element
const storageKey = "themed-preview-width";

/** @returns {string | null} */
function rememberedWidth() {
  try {
    return sessionStorage.getItem(storageKey);
  } catch {
    return null;
  }
}

/** @param {string} width */
function rememberWidth(width) {
  try {
    sessionStorage.setItem(storageKey, width);
  } catch {
    // a blocked storage only means the next load starts at Desktop
  }
}

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
    const width = rememberedWidth();
    const button = [...this.querySelectorAll("[data-preview-width]")].find(
      (candidate) => candidate instanceof HTMLElement && candidate.dataset.previewWidth === width
    );

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

    rememberWidth(chosen.dataset.previewWidth);
    this.#show(chosen);
  }

  /** @param {HTMLElement} chosen */
  #show(chosen) {
    const width = chosen.dataset.previewWidth;

    if (width === undefined) {
      return;
    }

    for (const button of this.querySelectorAll("[data-preview-width]")) {
      button.setAttribute("aria-pressed", String(button === chosen));
    }

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
