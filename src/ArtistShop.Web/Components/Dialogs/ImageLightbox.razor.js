// A picture filling the screen, and the images it was handed to step through. It is told what to
// show rather than knowing where the pictures came from, so any page can hold one
customElements.define(
  "image-lightbox",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    /** @type {HTMLImageElement[]} */
    #pictures = [];
    #current = 0;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      this.addEventListener("click", (event) => this.#onClick(event), { signal });
      this.addEventListener("keydown", (event) => this.#onKeyDown(event), { signal });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    /**
     * @param {HTMLImageElement[]} pictures the images on the page this shows a copy of
     * @param {number} index which of them to start on
     */
    open(pictures, index) {
      this.#pictures = pictures;
      this.#show(index);
      this.#dialog()?.showModal();
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      if (!(event.target instanceof Element)) {
        return;
      }

      const step = event.target.closest("[data-lightbox-step]");

      if (step instanceof HTMLElement) {
        this.#step(Number(step.dataset.lightboxStep));
        return;
      }

      // anywhere else closes, the close button and the black either side of the picture alike.
      // The whole screen carries the cursor that says so
      this.#dialog()?.close();
    }

    /** @param {KeyboardEvent} event */
    #onKeyDown(event) {
      // only reachable while open: a closed dialog holds no focus. Escape is the browser's own
      if (event.key === "ArrowLeft") {
        this.#step(-1);
      } else if (event.key === "ArrowRight") {
        this.#step(1);
      } else {
        return;
      }

      event.preventDefault();
    }

    /** @param {number} direction */
    #step(direction) {
      const next = this.#current + direction;

      // the ends hold rather than wrap, which is what the counter and the disabled buttons say
      if (next >= 0 && next < this.#pictures.length) {
        this.#show(next);
      }
    }

    /** @param {number} index */
    #show(index) {
      this.#current = index;

      const source = this.#pictures[index];
      const picture = this.querySelector("[data-lightbox-picture]");

      if (picture instanceof HTMLImageElement) {
        // the same srcset the page is already using, copied rather than built, so the browser
        // reuses the file it has and asks for a wider one only because sizes here says 100vw
        picture.srcset = source.srcset;
        picture.src = source.src;
        picture.alt = source.alt;
      }

      const count = this.#pictures.length;
      const counter = this.querySelector("[data-lightbox-counter]");

      if (counter instanceof HTMLElement) {
        counter.textContent = `${index + 1} of ${count}`;
        counter.hidden = count < 2;
      }

      for (const step of this.querySelectorAll("[data-lightbox-step]")) {
        const next = index + Number(step.dataset.lightboxStep);
        step.hidden = count < 2;
        step.disabled = next < 0 || next >= count;
      }

      // whoever opened this follows the picture, so closing leaves the page on the one being
      // looked at
      this.dispatchEvent(
        new CustomEvent("lightboxchange", { detail: { index }, bubbles: true })
      );
    }

    #dialog() {
      const dialog = this.querySelector("[data-lightbox-dialog]");
      return dialog instanceof HTMLDialogElement ? dialog : null;
    }
  }
);
