// Firefox ESR 140 has no invoker commands (command/commandfor), so the buttons are handled here

// A button anywhere on the page with data-opens-dialog="<id>" opens the <dialog> with that id. On the
// document, so it outlives enhanced navigation, which replaces the page's elements
document.addEventListener("click", (event) => {
  if (!(event.target instanceof Element)) {
    return;
  }

  const id = event.target.closest("[data-opens-dialog]")?.getAttribute("data-opens-dialog");
  const dialog = id ? document.getElementById(id) : null;

  if (dialog instanceof HTMLDialogElement && !dialog.open) {
    dialog.showModal();
  }
});

customElements.define(
  "modal-dialog",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      this.addEventListener("click", (event) => this.#onClick(event), { signal });
      // Escape fires "cancel" on the dialog, and preventing it keeps the dialog open. It doesn't bubble,
      // so this listens on the way down (capture) instead
      this.addEventListener(
        "cancel",
        (event) => {
          if (event.target === this.#dialog() && this.#dialog()?.hasAttribute("data-keep-open")) {
            event.preventDefault();
          }
        },
        { signal, capture: true }
      );

      // Blazor adds an element to the page before its children. A microtask runs once the current
      // render has finished, when the dialog is inside
      if (!this.hasAttribute("data-starts-closed")) {
        queueMicrotask(() => this.#open());
      }
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      if (!(event.target instanceof Element)) {
        return;
      }

      const action = event.target.closest("[data-modal-dialog]")?.getAttribute("data-modal-dialog");

      if (action === "open") {
        this.#open();
      } else if (action === "close") {
        this.#dialog()?.close();
      }
    }

    // showModal, unlike the open attribute, adds the backdrop, keeps focus inside and closes on Escape
    #open() {
      const dialog = this.#dialog();

      if (dialog?.isConnected && !dialog.open) {
        dialog.showModal();
      }
    }

    #dialog() {
      const dialog = this.querySelector(":scope > dialog");
      return dialog instanceof HTMLDialogElement ? dialog : null;
    }
  }
);
