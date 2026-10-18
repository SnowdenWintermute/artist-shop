// <row-preview-frame data-width="1200">: shrinks its child, drawn that many pixels wide, to fit its
// own width, and never enlarges it. It sets a variable on itself rather than the child's style, since
// inside an island Blazor owns the child's attributes
customElements.define(
  "row-preview-frame",
  class extends HTMLElement {
    static observedAttributes = ["data-width"];

    /** @type {ResizeObserver | null} */
    #observer = null;

    connectedCallback() {
      // on the next frame: the zoom resizes the child within this box, and changing layout inside the
      // callback makes the browser report a ResizeObserver loop error
      this.#observer = new ResizeObserver(() => requestAnimationFrame(() => this.#fit()));
      this.#observer.observe(this);
    }

    disconnectedCallback() {
      this.#observer?.disconnect();
    }

    // the Desktop and Phone buttons change the width
    attributeChangedCallback() {
      this.#fit();
    }

    // Firefox draws a zoom in steps of 1/64 (140 ESR, 2026-10-17), so the child can come out a few
    // pixels narrower or wider than this box. Measured rather than assumed, it's drawn that much wider
    // or narrower to fill the box exactly
    #fit() {
      const width = Number(this.dataset.width);
      const child = this.firstElementChild;

      if (!(width > 0 && this.clientWidth > 0 && child instanceof HTMLElement)) {
        return;
      }

      const zoom = Math.min(1, this.clientWidth / width);
      this.style.setProperty("--preview-zoom", String(zoom));
      this.style.setProperty("--preview-width", `${width}px`);

      const drawn = child.getBoundingClientRect().width;

      if (zoom < 1 && drawn > 0) {
        this.style.setProperty("--preview-width", `${(width * this.clientWidth) / drawn}px`);
      }
    }
  }
);
