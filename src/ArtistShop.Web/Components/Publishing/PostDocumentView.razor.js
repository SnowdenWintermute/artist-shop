// A post's images that open the full-screen view. <image-lightbox> is handed every one of them in
// the post, in order, so the visitor can step through them all from whichever was clicked
customElements.define(
  "post-document",
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
      const lightbox = this.querySelector("image-lightbox");

      if (trigger === null || !(lightbox instanceof HTMLElement) || !("open" in lightbox) || typeof lightbox.open !== "function") {
        return;
      }

      // looked up on each click, since an enhanced navigation to another post patches this
      // element in place
      const triggers = [...this.querySelectorAll("[data-lightbox-open]")];
      const pictures = triggers
        .map((each) => each.querySelector("img"))
        .filter((picture) => picture instanceof HTMLImageElement);

      lightbox.open(pictures, triggers.indexOf(trigger));
    }
  }
);
