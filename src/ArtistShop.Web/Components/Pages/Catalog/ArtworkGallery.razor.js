// Which of an artwork's images is showing. They are all in the page already, so this moves a
// marker rather than building any url: the server wrote each picture's srcset and blur once.
// The full-screen view is <image-lightbox>, which knows nothing about artworks: it is handed an
// ArtworkWalk's slides, which run on through the neighbouring artworks, and says which one it
// moved to. The page behind follows, once the visitor rests on another artwork's picture.
//
// Left and right step through the images, on the page and in the full-screen view alike, and past
// either end go on to the neighbouring artwork: back to its last image, or on to its first. The
// server wrote those two addresses on the element. The series bar's Previous and Next links take
// the same steps

import { ArtworkWalk } from "/js/artwork-walk.js";
import { lightboxPageCatchUpDelayMilliseconds } from "/js/app-consts.js";

/** @typedef {import("/js/artwork-walk.js").ArtworkWalkStep} ArtworkWalkStep */

// the walk the open lightbox is swiping through. Kept here rather than on the element: the
// lightbox is data-permanent, so it stays open while the page behind it changes
/** @type {ArtworkWalk | null} */
let walk = null;

// where the page behind goes once the visitor stops swiping, when the lightbox is resting on
// another artwork's picture
/** @type {string | null} */
let catchUpUrl = null;
/** @type {ReturnType<typeof setTimeout> | undefined} */
let catchUpTimer;

// one page load at a time: a held arrow key would otherwise start a navigation on every repeat
// before the first had loaded, and a page catching up to the lightbox waits for the last one
let navigating = false;

// an enhanced navigation, the same as following the series' Previous or Next link
function catchUp() {
  if (catchUpUrl === null || navigating) {
    return;
  }

  navigating = true;
  Blazor.navigateTo(catchUpUrl);
  catchUpUrl = null;
}

class ArtworkGallery extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    const { signal } = this.#listeners;

    this.addEventListener("click", (event) => this.#onClick(event), { signal });
    this.addEventListener(
      "lightboxchange",
      (event) => {
        if (event instanceof CustomEvent) {
          walk?.moveTo(event.detail.index);
          this.followLightbox();
        }
      },
      { signal }
    );
    this.addEventListener("lightboxclose", () => this.#onLightboxClose(), { signal });
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
      this.#goBeyond(direction);
    }
  }

  /**
   * @param {number} direction
   */
  #goBeyond(direction) {
    const url = direction < 0 ? this.dataset.previousUrl : this.dataset.nextUrl;

    if (url === undefined || navigating) {
      return;
    }

    navigating = true;
    Blazor.navigateTo(url);
  }

  // the walk starts from this artwork's step, which the server wrote on the element, so the
  // lightbox opens at once and fetches only what lies either side
  openLightbox() {
    const lightbox = this.querySelector("image-lightbox");
    const { walkUrl, walkStep } = this.dataset;

    if (lightbox === null || walkUrl === undefined || walkStep === undefined) {
      return;
    }

    /** @type {ArtworkWalkStep} */
    const step = JSON.parse(walkStep);

    walk = new ArtworkWalk(walkUrl, step, this.#currentIndex(), (indexes) => {
      for (const index of indexes) {
        lightbox.refreshSlide(index);
      }
    });

    // While the lightbox fades in, the picture the page already shows stands in for the one it
    // opens on, rather than the blur: it's as sharp as the page's, and the same file when the
    // page took the largest. PhotoSwipe uses a stand-in only on the slide it opens on
    const slides = walk.slides();
    const openedAt = walk.index;
    const shown = this.querySelector("[data-artwork-image]:not([hidden]) img");
    const shownSrc = shown instanceof HTMLImageElement && shown.complete && shown.naturalWidth > 0 ? shown.currentSrc : null;

    lightbox.openSlides(
      {
        ...slides,
        slideAt: (index) => {
          const slide = slides.slideAt(index);
          return slide !== null && index === openedAt && shownSrc !== null ? { ...slide, msrc: shownSrc } : slide;
        },
      },
      openedAt
    );
  }

  // the page follows the lightbox: this artwork's own pictures are shown where they stand, and
  // another's is gone to once the visitor has rested on it. Also asked after a page load, since
  // the visitor may have swiped on while it was loading
  followLightbox() {
    clearTimeout(catchUpTimer);
    catchUpUrl = null;

    const place = walk?.placeAt(walk.index) ?? null;

    // a picture still coming has no page yet
    if (place === null) {
      return;
    }

    if (place.walkUrl === this.dataset.walkUrl) {
      this.#show(place.imageIndex);
      return;
    }

    catchUpUrl = place.pageUrl;
    catchUpTimer = setTimeout(catchUp, lightboxPageCatchUpDelayMilliseconds);
  }

  // closing is resting: the page goes straight to the picture the visitor closed on
  #onLightboxClose() {
    clearTimeout(catchUpTimer);
    walk?.discard();
    walk = null;
    catchUp();
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
      if (!(thumbnail instanceof HTMLElement)) {
        continue;
      }

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
  const gallery = document.querySelector("artwork-gallery");

  if (walk !== null && gallery instanceof ArtworkGallery) {
    gallery.followLightbox();
  } else {
    // a catch-up that came while the last page was loading, after the lightbox closed
    catchUp();
  }
});
