// A page of one of the pickers, shown in a frame inside the opening page's dialog. It passes on
// Escape, which a frame keeps to itself. Back links return to where a list was scrolled through
// js/navigation-scroll.js
import { sendToPickerOwner } from "/js/picker-frame.js";

customElements.define(
  "picker-frame-page",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      document.addEventListener(
        "keydown",
        (event) => {
          if (event.key === "Escape") {
            sendToPickerOwner({ action: "close" });
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
