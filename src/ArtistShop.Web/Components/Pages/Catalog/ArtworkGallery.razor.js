// Which of an artwork's images is showing. They are all in the page already, so this moves a
// marker rather than building any url: the server wrote each picture's srcset and blur once.
// The full-screen view is <image-lightbox>, which knows nothing about artworks: it is handed the
// pictures and says which one it moved to
customElements.define(
  "artwork-gallery",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      this.addEventListener("click", (event) => this.#onClick(event), { signal });
      this.addEventListener(
        "lightboxchange",
        (event) => this.#show(event.detail.index),
        { signal }
      );
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      if (!(event.target instanceof Element)) {
        return;
      }

      const thumbnail = event.target.closest("[data-artwork-thumbnail]");

      if (thumbnail instanceof HTMLAnchorElement) {
        // a held modifier means the visitor asked for the page in a new tab or window, which is
        // what the link already does. A middle click raises auxclick, never this
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
          return;
        }

        event.preventDefault();
        this.#show(Number(thumbnail.dataset.artworkThumbnail));
        return;
      }

      if (event.target.closest("[data-lightbox-open]")) {
        this.#openLightbox();
      }
    }

    #openLightbox() {
      const lightbox = this.querySelector("image-lightbox");

      if (lightbox instanceof HTMLElement && "open" in lightbox) {
        const pictures = [...this.#images()].map((box) => box.querySelector("img"));

        lightbox.open(pictures, this.#currentIndex());
      }
    }

    // read off the page each time rather than kept: an enhanced navigation to another artwork
    // patches this element in place without connecting it again, and the server's hidden
    // attributes are the only thing that stays true across that
    #currentIndex() {
      const shown = this.querySelector("[data-artwork-image]:not([hidden])");
      return shown instanceof HTMLElement ? Number(shown.dataset.artworkImage) : 0;
    }

    /** @param {number} index */
    #show(index) {
      const wanted = String(index);

      for (const image of this.#images()) {
        image.toggleAttribute("hidden", image.dataset.artworkImage !== wanted);
      }

      for (const thumbnail of this.querySelectorAll("[data-artwork-thumbnail]")) {
        if (thumbnail.dataset.artworkThumbnail !== wanted) {
          thumbnail.removeAttribute("aria-current");
          continue;
        }

        // spelled out rather than toggled: an empty aria-current reads as false, so the attribute
        // has to carry the word
        thumbnail.setAttribute("aria-current", "true");

        // the address follows the picture, so it can still be copied and sent, without asking the
        // server for anything. replaceState rather than pushState: five thumbnails should not be
        // five presses of the back button. The link's own href is the address the server already
        // wrote, and the current state is handed back rather than wiped, since Blazor keeps its
        // own navigation state there
        if (thumbnail instanceof HTMLAnchorElement) {
          history.replaceState(history.state, "", thumbnail.href);
        }
      }
    }

    /** @returns {NodeListOf<HTMLElement>} */
    #images() {
      return this.querySelectorAll("[data-artwork-image]");
    }
  }
);
