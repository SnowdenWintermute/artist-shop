// Moves the nav bar's links into the drawer when they don't fit beside the logo. Below md the CSS
// does that alone, and the links' room has no width, so this never does. The links stay laid out
// while hidden, so hiding them doesn't change what's measured
customElements.define(
  "nav-bar",
  class extends HTMLElement {
    /** @type {ResizeObserver | null} */
    #observer = null;

    connectedCallback() {
      // Blazor adds an element to the page before its children, as ModalDialog.razor.js notes
      queueMicrotask(() => this.#watch());
    }

    disconnectedCallback() {
      this.#observer?.disconnect();
    }

    #watch() {
      if (!this.isConnected) {
        return;
      }

      const room = this.querySelector('[data-part="room"]');
      const links = this.querySelector('[data-part="links"]');

      if (!(room instanceof HTMLElement) || !(links instanceof HTMLElement)) {
        throw new Error("The nav bar is missing its links or their room.");
      }

      // the links' width changes with who is signed in, and when the font arrives
      this.#observer = new ResizeObserver(() => {
        this.toggleAttribute("data-collapsed", links.offsetWidth > room.clientWidth);
      });
      this.#observer.observe(room);
      this.#observer.observe(links);
    }
  }
);
