customElements.define(
  "static-modal-dialog",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    // a full page load upgrades this element after parsing, so its dialog is already inside it
    connectedCallback() {
      this.#listeners = new AbortController();

      this.addEventListener(
        "click",
        (event) => {
          if (event.target instanceof Element && event.target.closest('[data-part="open"]')) {
            this.#dialog()?.showModal();
          }
        },
        { signal: this.#listeners.signal }
      );

      // showModal, unlike the open attribute, adds the backdrop, keeps focus inside and closes on Escape
      this.#dialog()?.showModal();
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    #dialog() {
      const dialog = this.querySelector("dialog");
      return dialog instanceof HTMLDialogElement ? dialog : null;
    }
  }
);
