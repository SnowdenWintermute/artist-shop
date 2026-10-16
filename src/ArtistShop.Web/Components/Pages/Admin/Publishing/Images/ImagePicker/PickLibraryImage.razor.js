// The image picker's library step: a click on an image sends it to the page around the frame, in
// the shape the upload endpoint answers with, so the page takes either the same way
import { sendToPickerOwner } from "/js/picker-frame.js";

customElements.define(
  "library-image-choice",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      this.addEventListener("click", (event) => this.#choose(event.target), { signal: this.#listeners.signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    /** @param {EventTarget | null} target */
    #choose(target) {
      const button = target instanceof Element ? target.closest("button[data-storage-key]") : null;

      if (!(button instanceof HTMLButtonElement)) {
        return;
      }

      const { storageKey, width, height, blur, alt } = button.dataset;

      if (storageKey === undefined || blur === undefined || alt === undefined) {
        return;
      }

      sendToPickerOwner({
        action: "choose",
        choice: { storageKey, width: Number(width), height: Number(height), blurDataUri: blur, altText: alt },
      });
    }
  }
);
