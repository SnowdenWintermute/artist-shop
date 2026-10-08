// A picture filling the screen, and the others it can be swiped or stepped to. It is told what to
// show rather than knowing where the pictures came from, so any page can hold one: a post hands it
// the pictures on the page, and a work hands it slides it is still fetching, filling each in
// as it arrives. A slide not in yet shows black.
//
// The picture on screen is PhotoSwipe's, for its swiping and pinch zoom, and the buttons over it
// are ours. PhotoSwipe asks for each slide by its number as it needs it, so slides can arrive in
// any order without renumbering the ones already there

import { lightboxFadeMilliseconds } from "/js/app-consts.js";

// every lightbox loads the same module, so the first to open is the one that loads it
/** @type {Promise<PhotoSwipeModule> | null} */
let photoSwipeLoaded = null;

// src is Assets' address, relative to the page's <base> ("lib/…"), which import() would read as a
// bare module name and refuse, so it's made a full address first. A load that fails is forgotten,
// so the next open tries again
/** @param {string} src */
function loadPhotoSwipe(src) {
  photoSwipeLoaded ??= import(new URL(src, document.baseURI).href).catch((error) => {
    photoSwipeLoaded = null;
    throw error;
  });
  return photoSwipeLoaded;
}

/** @type {PhotoSwipeSlide} */
const blankSlide = { html: "" };

// PhotoSwipe lays a slide out before any of it has loaded, so it's told the picture's full size:
// the widest file in the srcset, at the shape of the width and height written on the img
/**
 * @param {HTMLImageElement} picture
 * @returns {PhotoSwipeSlide}
 */
function slideFor(picture) {
  const widths = picture.srcset
    .split(",")
    .map((candidate) => Number.parseInt(candidate.trim().split(/\s+/)[1] ?? "", 10))
    .filter((width) => !Number.isNaN(width));
  const shapeWidth = Number(picture.getAttribute("width"));
  const shapeHeight = Number(picture.getAttribute("height"));

  if (widths.length === 0 || !(shapeWidth > 0) || !(shapeHeight > 0)) {
    throw new Error(`The lightbox needs a srcset with widths and a width and height on ${picture.src}.`);
  }

  const width = Math.max(...widths);

  return {
    src: picture.src,
    srcset: picture.srcset,
    alt: picture.alt,
    width,
    height: Math.round((width * shapeHeight) / shapeWidth),
    // the file the page already shows, stretched to fill in while the sharper one loads
    ...(picture.complete && picture.naturalWidth > 0 ? { msrc: picture.currentSrc } : {}),
  };
}

customElements.define(
  "image-lightbox",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    /** @type {PhotoSwipe | null} */
    #viewer = null;
    // counts opens, so a PhotoSwipe that finishes loading after its dialog has closed, or closed
    // and opened again, is never shown
    #opens = 0;
    // lightboxchange is raised once per picture reached, not again when a slide is filled in
    /** @type {number | null} */
    #changedTo = null;
    // the picture the arrows and counter were last set for
    /** @type {number | null} */
    #controlsFor = null;

    connectedCallback() {
      this.#listeners = new AbortController();
      const { signal } = this.#listeners;

      this.addEventListener("click", (event) => this.#onClick(event), { signal });
      this.addEventListener("keydown", (event) => this.#onKeyDown(event), { signal });
      // close doesn't bubble, so it's caught on its way down to the dialog. Escape closes the
      // dialog without asking anyone
      this.addEventListener("close", () => this.#onDialogClose(), { signal, capture: true });
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    /**
     * Opens on pictures already on the page
     * @param {HTMLImageElement[]} pictures
     * @param {number} index which of them to start on
     */
    open(pictures, index) {
      const slides = pictures.map(slideFor);

      this.openSlides(
        {
          itemCount: slides.length,
          slideAt: (at) => slides[at] ?? null,
          counterAt: (at) => (slides.length < 2 ? null : `${at + 1} of ${slides.length}`),
        },
        index
      );
    }

    /**
     * @param {LightboxSlides} slides
     * @param {number} index which to start on
     */
    openSlides(slides, index) {
      const dialog = this.#dialog();
      const src = this.dataset.photoswipeSrc;

      if (!dialog || dialog.open || src === undefined) {
        return;
      }

      dialog.showModal();
      const open = ++this.#opens;

      loadPhotoSwipe(src).then(
        (photoSwipe) => {
          if (open === this.#opens && dialog.open) {
            this.#start(photoSwipe, dialog, slides, index);
          }
        },
        // with nothing to show, the open dialog would only be buttons over the page
        (error) => {
          console.error(error);

          if (open === this.#opens) {
            dialog.close();
          }
        }
      );
    }

    // a slide that was still coming has arrived. PhotoSwipe keeps what it made for each number,
    // the black stand-in too, so it's told to make that one again
    /** @param {number} index */
    refreshSlide(index) {
      this.#viewer?.refreshSlideContent(index);
    }

    /**
     * @param {PhotoSwipeModule} photoSwipe
     * @param {HTMLDialogElement} dialog
     * @param {LightboxSlides} slides
     * @param {number} index
     */
    #start({ default: PhotoSwipe }, dialog, slides, index) {
      const viewer = new PhotoSwipe({
        index,
        appendToEl: dialog,
        loop: false,
        bgOpacity: 1,
        showHideAnimationType: "fade",
        showAnimationDuration: lightboxFadeMilliseconds,
        hideAnimationDuration: lightboxFadeMilliseconds,
        // the buttons and the counter are ours, and so are the keys, which the dialog holds
        arrowPrev: false,
        arrowNext: false,
        close: false,
        zoom: false,
        counter: false,
        arrowKeys: false,
        escKey: false,
        trapFocus: false,
        returnFocus: false,
        tapAction: "close",
        // the dialog's padding is the notch's, which only CSS can read
        paddingFn: () => {
          const style = getComputedStyle(dialog);

          return {
            top: Number.parseFloat(style.paddingTop),
            right: Number.parseFloat(style.paddingRight),
            bottom: Number.parseFloat(style.paddingBottom),
            left: Number.parseFloat(style.paddingLeft),
          };
        },
      });

      // PhotoSwipe's own names for how many slides there are and what each one is
      viewer.addFilter("numItems", () => slides.itemCount);
      viewer.addFilter("itemData", (_, at) => slides.slideAt(at) ?? blankSlide);

      viewer.on("change", () => this.#onChange(viewer, slides));
      // a released swipe springs on into place, and "change" only comes once it has settled. The
      // picture it's heading for is known from the first frame, so the controls follow that
      viewer.on("moveMainScroll", () => {
        if (viewer.potentialIndex !== this.#controlsFor) {
          this.#showControls(viewer, slides);
        }
      });
      // PhotoSwipe closing itself, from a tap or a pinch, closes the dialog with it. When the
      // dialog closed first, it's closed already and this does nothing
      viewer.on("destroy", () => {
        this.#viewer = null;
        dialog.close();
      });

      this.#viewer = viewer;
      this.#changedTo = null;
      this.#controlsFor = null;
      viewer.init();
    }

    /** @param {MouseEvent} event */
    #onClick(event) {
      if (!(event.target instanceof Element)) {
        return;
      }

      const step = event.target.closest("[data-lightbox-step]");

      if (step instanceof HTMLElement) {
        this.#step(Number(step.dataset.lightboxStep));
      } else if (event.target.closest("[data-lightbox-close]")) {
        this.#close();
      }
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

    // with PhotoSwipe's fade when it's showing. Before it has loaded there is only the dialog
    #close() {
      if (this.#viewer) {
        this.#viewer.close();
      } else {
        this.#dialog()?.close();
      }
    }

    /** @param {number} direction */
    #step(direction) {
      if (direction < 0) {
        this.#viewer?.prev();
      } else {
        this.#viewer?.next();
      }
    }

    #onDialogClose() {
      this.#opens++;
      const viewer = this.#viewer;
      this.#viewer = null;
      viewer?.destroy();
      this.#hideControls();
      this.dispatchEvent(new CustomEvent("lightboxclose", { bubbles: true }));
    }

    /**
     * @param {PhotoSwipe} viewer
     * @param {LightboxSlides} slides
     */
    #onChange(viewer, slides) {
      // also when a slide that was still coming is filled in, which can change the counter
      this.#showControls(viewer, slides);

      const index = viewer.currIndex;

      if (index === this.#changedTo) {
        return;
      }

      this.#changedTo = index;
      // whoever opened this follows the picture, so closing leaves the page on the one being
      // looked at
      this.dispatchEvent(new CustomEvent("lightboxchange", { detail: { index }, bubbles: true }));
    }

    /**
     * For the picture being moved to, which is the one showing once a move has settled
     * @param {PhotoSwipe} viewer
     * @param {LightboxSlides} slides
     */
    #showControls(viewer, slides) {
      const index = viewer.potentialIndex;
      this.#controlsFor = index;

      // the ends hold rather than wrap, which is what the disabled buttons say
      for (const step of this.querySelectorAll("[data-lightbox-step]")) {
        if (!(step instanceof HTMLButtonElement)) {
          continue;
        }

        const next = index + Number(step.dataset.lightboxStep);
        step.hidden = slides.itemCount < 2;
        step.disabled = next < 0 || next >= slides.itemCount;
      }

      const counter = this.querySelector("[data-lightbox-counter]");
      const counterText = slides.counterAt(index);

      if (counter instanceof HTMLElement) {
        counter.textContent = counterText ?? "";
        counter.hidden = counterText === null;
      }
    }

    // the lightbox outlives the page it opened on, so the next opening starts without the last
    // one's arrows and counter, which the first change shows again
    #hideControls() {
      for (const control of this.querySelectorAll("[data-lightbox-step], [data-lightbox-counter]")) {
        if (control instanceof HTMLElement) {
          control.hidden = true;
        }
      }
    }

    #dialog() {
      const dialog = this.querySelector("[data-lightbox-dialog]");
      return dialog instanceof HTMLDialogElement ? dialog : null;
    }
  }
);
