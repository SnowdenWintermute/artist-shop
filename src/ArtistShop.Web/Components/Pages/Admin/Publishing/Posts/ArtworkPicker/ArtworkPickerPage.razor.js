// The pages of the post editor's artwork picker, which run in a frame inside the editor's dialog.
// They tell the editor what was chosen, and pass on Escape, which a frame keeps to itself. Their
// Back links return to where the list was scrolled through js/scroll-restoration.js
import { sendToArtworkPickerOwner } from "../PostArtworkEmbed.razor.js";

customElements.define(
  "artwork-picker-page",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      document.addEventListener(
        "keydown",
        (event) => {
          if (event.key === "Escape") {
            sendToArtworkPickerOwner({ action: "close" });
          }
        },
        { signal: this.#listeners.signal }
      );
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }
  }
);

customElements.define(
  "artwork-embed-choice",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      this.querySelector('[data-part="insert"]')?.addEventListener("click", () => this.#insert(), { signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    // Changing an embed's image renders no size or layout controls, and sends only the image
    #insert() {
      const { artworkId, storageKey } = this.dataset;

      if (artworkId === undefined || storageKey === undefined) {
        return;
      }

      const size = this.#input('input[name="size"]:checked')?.value;
      const layout = this.#input('input[name="layout"]:checked')?.value;

      sendToArtworkPickerOwner({
        action: "choose",
        choice: {
          artworkId: Number(artworkId),
          storageKey,
          ...(size === "small" || size === "medium" ? { size } : {}),
          ...(layout === undefined ? {} : { layout }),
        },
      });
    }

    /** @param {string} selector */
    #input(selector) {
      const input = this.querySelector(selector);
      return input instanceof HTMLInputElement ? input : null;
    }
  }
);
