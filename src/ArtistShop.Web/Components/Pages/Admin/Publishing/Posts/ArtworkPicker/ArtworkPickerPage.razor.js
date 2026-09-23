// The pages of the post editor's artwork picker, which run in a frame inside the editor's dialog.
// They tell the editor what was chosen, pass on Escape, which a frame keeps to itself, and return
// to where the list was scrolled
import { sendToArtworkPickerOwner } from "../PostArtworkEmbed.razor.js";

// How far down each page was scrolled when a link out of it was followed, so a Back link to that
// same address lands where the artist left it rather than at the top. Moving between the steps
// is enhanced navigation, which keeps this module running, and closing the dialog empties the
// frame, so a new opening starts with none
/** @type {Map<string, number>} */
const scrollPositions = new Map();

const currentAddress = () => location.pathname + location.search;

Blazor.addEventListener("enhancedload", () => {
  const scrollY = scrollPositions.get(currentAddress());

  if (scrollY === undefined) {
    return;
  }

  // once only: following a link here later is a fresh visit
  scrollPositions.delete(currentAddress());
  // a frame later, after Blazor has scrolled the new page to the top
  requestAnimationFrame(() => window.scrollTo(0, scrollY));
});

customElements.define(
  "artwork-picker-page",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      this.addEventListener(
        "click",
        (event) => {
          if (event.target instanceof Element && event.target.closest("a[href]") !== null) {
            scrollPositions.set(currentAddress(), window.scrollY);
          }
        },
        { signal: this.#listeners.signal }
      );
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
