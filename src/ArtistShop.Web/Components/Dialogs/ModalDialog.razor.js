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

// A click on the backdrop lands on the dialog itself, outside the box it's drawn in
/**
 * @param {MouseEvent} event
 * @param {HTMLDialogElement} dialog
 */
function isOnBackdrop(event, dialog) {
  const box = dialog.getBoundingClientRect();

  return (
    event.target === dialog &&
    (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom)
  );
}

// a link that opens in this tab; a modifier key opens it in another tab or window
/**
 * @param {MouseEvent} event
 * @param {Element} target
 */
function isLinkFollowed(event, target) {
  const link = target.closest("a[href]");
  const opensElsewhere = link instanceof HTMLAnchorElement && link.target === "_blank";
  const modified = event.ctrlKey || event.metaKey || event.shiftKey;

  return link !== null && !opensElsewhere && !modified;
}

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

      const dialog = this.#dialog();

      if (event.target.closest("[data-modal-dialog]")?.getAttribute("data-modal-dialog") === "close") {
        dialog?.close();
      } else if (dialog?.hasAttribute("data-menu") && (isOnBackdrop(event, dialog) || isLinkFollowed(event, event.target))) {
        // enhanced navigation keeps the page's layout, and a menu in it would stay open on the next page
        dialog.close();
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
