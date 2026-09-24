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

      this.addEventListener("change", () => this.#allowWrapOnlyBesideText(), { signal });
      this.querySelector('[data-part="insert"]')?.addEventListener("click", () => this.#insert(), { signal });
      this.#allowWrapOnlyBesideText();
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    // a centred embed has nothing beside it to wrap
    #allowWrapOnlyBesideText() {
      const wrap = this.#input('input[name="wrap"]');

      if (wrap !== null) {
        wrap.disabled = this.#input('input[name="alignment"]:checked')?.dataset.wrappedLayout === undefined;
      }
    }

    // Changing an embed's image renders no size or layout controls, and sends only the image
    #insert() {
      const { artworkId, storageKey } = this.dataset;

      if (artworkId === undefined || storageKey === undefined) {
        return;
      }

      const size = this.#input('input[name="size"]:checked')?.value;
      const alignment = this.#input('input[name="alignment"]:checked');
      const wrap = this.#input('input[name="wrap"]');
      const wrappedLayout = alignment?.dataset.wrappedLayout;

      sendToArtworkPickerOwner({
        action: "choose",
        choice: {
          artworkId: Number(artworkId),
          storageKey,
          ...(size === "small" || size === "medium" ? { size } : {}),
          ...(alignment === null
            ? {}
            : { layout: wrap?.checked && wrappedLayout !== undefined ? wrappedLayout : alignment.value }),
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
