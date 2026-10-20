// <image-carousel>: Embla over the slides CarouselView drew, with Fade or its own slide, autoplay, and the
// accessibility plugin's roles and labels. Embla's files are imported the first time a carousel connects, from
// the addresses on the element, so pages without one never load them. Inside the editor's inert preview it
// stays on the first slide.
//
// Autoplay pauses while the pointer is over the carousel and stops for good once the visitor swipes, uses the
// controls or moves focus into it. The live region announces slides only once the visitor has used the controls,
// and goes quiet again when they start autoplay. Under reduced motion slides
// change at once and never on their own. Enhanced navigation puts the server's attributes back when it patches
// a page, wiping Embla's styles and data-ready, so a carousel without data-ready afterwards is built again

/** @type {Promise<EmblaModules> | null} */
let emblaLoaded = null;

// The addresses are Assets' ones, relative to the page's <base>, which import() would read as bare module names,
// so they're made full addresses first. A load that fails is forgotten, so the next carousel tries again
/** @param {HTMLElement} element */
function loadEmbla(element) {
  /** @param {string} name */
  const load = (name) => {
    const src = element.dataset[name];

    if (src === undefined) {
      throw new Error(`The carousel is missing data-${name}.`);
    }

    return import(new URL(src, document.baseURI).href);
  };

  emblaLoaded ??= Promise.all([load("emblaSrc"), load("fadeSrc"), load("autoplaySrc"), load("accessibilitySrc")])
    .then(([core, fade, autoplay, accessibility]) => ({
      EmblaCarousel: core.default,
      Fade: fade.default,
      Autoplay: autoplay.default,
      Accessibility: accessibility.default,
    }))
    .catch((error) => {
      emblaLoaded = null;
      throw error;
    });

  return emblaLoaded;
}

const reducedMotion = "(prefers-reduced-motion: reduce)";

class ImageCarousel extends HTMLElement {
  /** @type {EmblaCarouselApi | null} */
  #embla = null;
  /** @type {AbortController | null} */
  #listeners = null;
  // the visitor took over, so autoplay doesn't come back on its own
  #isStopped = false;
  #isPausedByHover = false;

  connectedCallback() {
    if (this.closest("[inert]") === null) {
      void this.build();
    }
  }

  disconnectedCallback() {
    this.#teardown();
  }

  async build() {
    this.#teardown();
    const viewport = this.querySelector('[data-part="viewport"]');

    if (!(viewport instanceof HTMLElement) || this.querySelectorAll('[data-part="slide"]').length < 2) {
      return;
    }

    const { EmblaCarousel, Fade, Autoplay, Accessibility } = await loadEmbla(this);

    // disconnected, or built again, while the files loaded
    if (!this.isConnected || this.#embla !== null) {
      return;
    }

    // Embla measures in whole pixels, so at a width with a fraction each slide would stop a little short, leaving
    // its neighbour's edge in view, more the further it is from the first. The viewport is held to a whole number
    // of pixels instead, centred, and resized with the carousel, which Embla follows
    this.#listeners = new AbortController();
    const fitWholePixels = () => (viewport.style.width = `${Math.floor(this.getBoundingClientRect().width)}px`);
    const resizes = new ResizeObserver(() => {
      fitWholePixels();
      this.#fitDots();
    });
    resizes.observe(this);
    this.#listeners.signal.addEventListener("abort", () => resizes.disconnect());
    fitWholePixels();

    const interval = Number(this.dataset.intervalMilliseconds);
    const plugins = [
      Accessibility({
        carouselAriaLabel: "Carousel",
        previousButtonAriaLabel: "Previous slide",
        nextButtonAriaLabel: "Next slide",
        // on once the visitor takes over, so nothing is announced as the page loads or while autoplay runs
        announceChanges: false,
      }),
      ...(this.dataset.transition === "Fade" ? [Fade()] : []),
      ...(interval > 0
        ? [Autoplay({ delay: interval, defaultInteraction: false, breakpoints: { [reducedMotion]: { active: false } } })]
        : []),
    ];
    const embla = EmblaCarousel(viewport, { loop: true, breakpoints: { [reducedMotion]: { duration: 0 } } }, plugins);
    this.#embla = embla;
    this.#connectControls(embla, this.#listeners.signal);
    this.dataset.ready = "";
    this.#fitDots();

    const autoplay = embla.plugins().autoplay;

    if (autoplay !== undefined) {
      embla.on("autoplay:play", () => this.#showPlaying(true)).on("autoplay:stop", () => this.#showPlaying(autoplay.isPlaying()));
      embla.on("autoplay:interaction", (_, event) => this.#onInteraction(autoplay, event.detail.interaction));
      autoplay.play();
    }
  }

  /**
   * @param {EmblaCarouselApi} embla
   * @param {AbortSignal} signal
   */
  #connectControls(embla, signal) {
    const accessibility = embla.plugins().accessibility;
    const previous = this.querySelector('[data-action="previous"]');
    const next = this.querySelector('[data-action="next"]');
    const dots = this.querySelector('[data-part="dots"]');
    const pause = this.querySelector('[data-action="pause"]');
    const liveRegion = this.querySelector('[data-part="live-region"]');

    if (previous instanceof HTMLElement && next instanceof HTMLElement) {
      accessibility?.setupPrevAndNextButtons(previous, next);
      previous.addEventListener("click", () => this.#takeOver(() => embla.goToPrev()), { signal });
      next.addEventListener("click", () => this.#takeOver(() => embla.goToNext()), { signal });
    }

    if (dots instanceof HTMLElement) {
      accessibility?.setupDotButtons(dots);
      [...dots.children].forEach((dot, index) =>
        dot.addEventListener("click", () => this.#takeOver(() => embla.goTo(index)), { signal })
      );
      const counter = this.querySelector('[data-part="counter"]');
      embla.on("select", () => {
        if (counter !== null) {
          counter.textContent = `${embla.selectedSnap() + 1} / ${dots.children.length}`;
        }
      });
    }

    if (liveRegion instanceof HTMLElement) {
      accessibility?.setupLiveRegion(liveRegion);
    }

    pause?.addEventListener(
      "click",
      () => {
        // as the button shows it, since a pause while the pointer is over the slides still shows as playing
        if (this.hasAttribute("data-playing")) {
          this.#takeOver(() => {});
        } else {
          this.#isStopped = false;
          embla.plugins().accessibility?.setUpdateLiveRegion(false);
          embla.plugins().autoplay?.play();
        }
      },
      { signal }
    );
  }

  // The counter stands in for the dots wherever they'd be wider than the controls' room. Measured with the dots
  // showing, each time, since the room and the controls beside them change with the carousel's width
  #fitDots() {
    const dots = this.querySelector('[data-part="dots"]');
    const controls = this.querySelector('[data-part="controls"]');
    const bar = dots?.parentElement;

    if (!(dots instanceof HTMLElement) || !(controls instanceof HTMLElement) || !(bar instanceof HTMLElement)) {
      return;
    }

    this.removeAttribute("data-compact");
    this.toggleAttribute("data-compact", bar.scrollWidth > controls.clientWidth);
  }

  // the visitor chose a slide or stopped autoplay: it stays stopped, and the live region speaks from now on
  /** @param {() => void} move */
  #takeOver(move) {
    this.#isStopped = true;
    this.#embla?.plugins().autoplay?.stop();
    this.#embla?.plugins().accessibility?.setUpdateLiveRegion(true);
    move();
  }

  /**
   * @param {EmblaAutoplay} autoplay
   * @param {string} interaction
   */
  #onInteraction(autoplay, interaction) {
    if (interaction === "pointerdown" || interaction === "slidefocus") {
      this.#takeOver(() => {});
    } else if (interaction === "mouseenter" && autoplay.isPlaying()) {
      this.#isPausedByHover = true;
      autoplay.pause();
    } else if (interaction === "mouseleave" && this.#isPausedByHover) {
      this.#isPausedByHover = false;

      if (!this.#isStopped) {
        autoplay.play();
      }
    }
  }

  // a pause while the pointer is over it still shows as playing, as it carries on once the pointer leaves
  /** @param {boolean} isPlaying */
  #showPlaying(isPlaying) {
    const shown = isPlaying || (this.#isPausedByHover && !this.#isStopped);
    this.toggleAttribute("data-playing", shown);
    this.querySelector('[data-action="pause"]')?.setAttribute("aria-label", shown ? "Pause slides" : "Play slides");
  }

  #teardown() {
    this.#listeners?.abort();
    this.#listeners = null;
    this.#embla?.destroy();
    this.#embla = null;
    this.#isStopped = false;
    this.#isPausedByHover = false;
    delete this.dataset.ready;
    this.removeAttribute("data-playing");
    this.removeAttribute("data-compact");
  }
}

customElements.define("image-carousel", ImageCarousel);

Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("image-carousel:not([data-ready])").forEach((carousel) => {
    if (carousel instanceof ImageCarousel && carousel.closest("[inert]") === null) {
      void carousel.build();
    }
  })
);
