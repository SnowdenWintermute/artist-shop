// An image component's lightbox, which shows only its own picture
customElements.define(
  "image-view",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      this.addEventListener("click", (event) => this.#onClick(event), { signal: this.#listeners.signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      const trigger = event.target instanceof Element ? event.target.closest("[data-lightbox-open]") : null;
      const picture = trigger?.querySelector("img");
      const lightbox = this.querySelector("image-lightbox");

      if (
        !(picture instanceof HTMLImageElement) ||
        !(lightbox instanceof HTMLElement) ||
        !("open" in lightbox) ||
        typeof lightbox.open !== "function"
      ) {
        return;
      }

      lightbox.open([picture], 0);
    }
  }
);
