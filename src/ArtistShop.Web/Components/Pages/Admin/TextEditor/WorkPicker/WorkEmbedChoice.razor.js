// The work picker's last step, which sends the choice to the editor around the frame
import { sendToPickerOwner } from "/js/picker-frame.js";

customElements.define(
  "work-embed-choice",
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
      const { workId, storageKey } = this.dataset;

      if (workId === undefined || storageKey === undefined) {
        return;
      }

      const size = this.#input('input[name="size"]:checked')?.value;
      const layout = this.#input('input[name="layout"]:checked')?.value;

      sendToPickerOwner({
        action: "choose",
        choice: {
          workId: Number(workId),
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
