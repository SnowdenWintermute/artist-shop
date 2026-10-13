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

    #fit() {
      const width = Number(this.dataset.width);

      if (width > 0 && this.clientWidth > 0) {
        this.style.setProperty("--preview-zoom", String(Math.min(1, this.clientWidth / width)));
      }
    }
  }
);
