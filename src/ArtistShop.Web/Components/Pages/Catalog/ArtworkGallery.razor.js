// Which of an artwork's images is showing. They are all in the page already, so this moves a
// marker rather than building any url: the server wrote each picture's srcset and blur once.
// The full-screen view is <image-lightbox>, which knows nothing about artworks: it is handed the
// pictures and says which one it moved to.
//
// Left and right step through the images, on the page and in the full-screen view alike, and past
// either end go on to the neighbouring artwork: back to its last image, or on to its first. The
// server wrote those two addresses on the element. The series bar's Previous and Next links take
// the same steps

// set when the full-screen view steps past an end. The next artwork's page hands it the new images
// on enhancedload, which Blazor raises in the same task as it patches the page in, so the view
// never shows closed. It stays open because it is data-permanent: the patch leaves it alone
let carryLightbox = false;

// one step to another artwork at a time: a held arrow key would otherwise start a navigation on
// every repeat before the first had loaded
let navigating = false;

class ArtworkGallery extends HTMLElement {
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
    this.addEventListener(
      "lightboxbeyond",
      (event) => this.#goBeyond(event.detail.direction, { carryLightbox: true }),
      { signal }
    );
    document.addEventListener("keydown", (event) => this.#onKeyDown(event), { signal });
    // capture, so this sees a click on Previous or Next before Blazor's own listener on the
    // document, which leaves alone a click that has been handled already
    document.addEventListener("click", (event) => this.#onStepLinkClick(event), {
      signal,
      capture: true,
    });
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
      this.openLightbox();
    }
  }

  /** @param {KeyboardEvent} event */
  #onKeyDown(event) {
    if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") {
      return;
    }

    // a held modifier is the browser's own shortcut, usually going back or forward
    if (event.metaKey || event.ctrlKey || event.altKey || event.shiftKey) {
      return;
    }

    // a dialog keeps its own keys while it is open, the full-screen view's among them, and inside
    // a field the arrows belong to the text
    if (document.querySelector("dialog[open]")) {
      return;
    }

    const from = event.target;

    if (
      from instanceof HTMLElement &&
      (from.isContentEditable || from.closest("input, textarea, select"))
    ) {
      return;
    }

    event.preventDefault();
    this.#step(event.key === "ArrowLeft" ? -1 : 1);
  }

  /** @param {MouseEvent} event */
  #onStepLinkClick(event) {
    if (!(event.target instanceof Element)) {
      return;
    }

    const link = event.target.closest("a[data-artwork-step]");

    if (!(link instanceof HTMLAnchorElement) || !link.hasAttribute("href")) {
      return;
    }

    // a held modifier opens the step in a new tab, which is what the link's href already does
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
      return;
    }

    event.preventDefault();
    this.#step(Number(link.dataset.artworkStep));
  }

  /** @param {number} direction */
  #step(direction) {
    const next = this.#currentIndex() + direction;

    if (next >= 0 && next < this.#images().length) {
      this.#show(next);
    } else {
      this.#goBeyond(direction, { carryLightbox: false });
    }
  }

  /**
   * @param {number} direction
   * @param {{ carryLightbox: boolean }} options
   */
  #goBeyond(direction, options) {
    const url = direction < 0 ? this.dataset.previousUrl : this.dataset.nextUrl;

    if (url === undefined || navigating) {
      return;
    }

    navigating = true;
    carryLightbox = options.carryLightbox;
    // an enhanced navigation, the same as following the series' Previous or Next link
    Blazor.navigateTo(url);
  }

  openLightbox() {
    const lightbox = this.querySelector("image-lightbox");

    if (lightbox instanceof HTMLElement && "open" in lightbox) {
      const pictures = [...this.#images()].map((box) => box.querySelector("img"));

      lightbox.open(pictures, this.#currentIndex(), {
        continuesBefore: this.dataset.previousUrl !== undefined,
        continuesAfter: this.dataset.nextUrl !== undefined,
      });
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

    this.#moveStepLinks(index);
  }

  // Previous and Next point at the steps from the image now showing: a thumbnail's own address
  // for the image beside it, or past either end the neighbouring artwork. Without an href a link
  // grays out
  /** @param {number} index */
  #moveStepLinks(index) {
    for (const link of document.querySelectorAll("a[data-artwork-step]")) {
      if (!(link instanceof HTMLAnchorElement)) {
        continue;
      }

      const direction = Number(link.dataset.artworkStep);
      const beside = this.querySelector(`a[data-artwork-thumbnail="${index + direction}"]`);
      const url =
        beside instanceof HTMLAnchorElement
          ? beside.href
          : direction < 0
            ? this.dataset.previousUrl
            : this.dataset.nextUrl;

      if (url === undefined) {
        link.removeAttribute("href");
      } else {
        link.href = url;
      }
    }
  }

  /** @returns {NodeListOf<HTMLElement>} */
  #images() {
    return this.querySelectorAll("[data-artwork-image]");
  }
}

customElements.define("artwork-gallery", ArtworkGallery);

Blazor.addEventListener("enhancedload", () => {
  navigating = false;

  if (!carryLightbox) {
    return;
  }

  carryLightbox = false;
  const gallery = document.querySelector("artwork-gallery");

  if (gallery instanceof ArtworkGallery) {
    gallery.openLightbox();
  }
});
