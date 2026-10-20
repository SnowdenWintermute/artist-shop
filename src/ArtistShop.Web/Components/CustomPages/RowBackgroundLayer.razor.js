// <background-slideshow>: a row's background images crossfading behind its content, from the first one
// RowBackgroundLayer drew. Each waiting image is added from its <template> one step before it shows, so it has a
// whole interval to load. Inside the editor's inert preview it stays on the first image.
//
// It plays only while the visitor hasn't paused it, the tab shows and the row is on screen, and never under
// reduced motion, where its button stays hidden. The pause is one choice for the whole website, kept in local
// storage as a visitor's preference: pausing one slideshow pauses every other, here and in other tabs, and on the
// pages they open later. The button is the row's (PageRowView), found by the slideshow's id.
//
// Enhanced navigation puts the server's markup back when it patches a page, which drops the added images and
// data-ready, so a slideshow left without them afterwards is built again

const pausedKey = "background-slideshows-paused";
const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

// storage can be refused, as in some private windows: the slideshow then plays, and a pause lasts the page
let pausedWithoutStorage = false;

function isPausedByVisitor() {
  try {
    return localStorage.getItem(pausedKey) === "true";
  } catch {
    return pausedWithoutStorage;
  }
}

/** @param {boolean} isPaused */
function rememberPaused(isPaused) {
  pausedWithoutStorage = isPaused;

  try {
    if (isPaused) {
      localStorage.setItem(pausedKey, "true");
    } else {
      localStorage.removeItem(pausedKey);
    }
  } catch {
    // kept for this page only, above
  }

  eachSlideshow((slideshow) => slideshow.refresh());
}

/** @param {(slideshow: BackgroundSlideshow) => void} act */
function eachSlideshow(act) {
  document.querySelectorAll("background-slideshow").forEach((element) => {
    if (element instanceof BackgroundSlideshow) {
      act(element);
    }
  });
}

class BackgroundSlideshow extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  /** @type {IntersectionObserver | null} */
  #observer = null;
  #timer = 0;
  #isOnScreen = true;

  connectedCallback() {
    this.build();
  }

  disconnectedCallback() {
    this.#teardown();
  }

  build() {
    this.#teardown();

    if (this.closest("[inert]") !== null || reducedMotion.matches || this.#slides().length + this.#waiting().length < 2) {
      return;
    }

    this.#listeners = new AbortController();
    const { signal } = this.#listeners;
    const button = this.#button();

    button?.addEventListener("click", () => rememberPaused(!isPausedByVisitor()), { signal });
    document.addEventListener("visibilitychange", () => this.refresh(), { signal });
    window.addEventListener("storage", (event) => event.key === pausedKey && this.refresh(), { signal });
    this.#observer = new IntersectionObserver(([entry]) => {
      this.#isOnScreen = entry?.isIntersecting ?? true;
      this.refresh();
    });
    this.#observer.observe(this);

    this.#addNext();
    this.dataset.ready = "";
    button?.setAttribute("data-ready", "");
    this.refresh();
  }

  // whether it runs with its button wired up, or doesn't run at all, as in the inert preview or under reduced motion
  isBuiltWithButton() {
    return this.dataset.ready === undefined
      ? this.closest("[inert]") !== null || reducedMotion.matches || this.#slides().length + this.#waiting().length < 2
      : this.#button()?.hasAttribute("data-ready") ?? true;
  }

  // the button shows the visitor's choice; the images change only while it's playing and can be seen
  refresh() {
    if (this.dataset.ready === undefined) {
      return;
    }

    const isPlaying = !isPausedByVisitor();
    const button = this.#button();
    button?.toggleAttribute("data-playing", isPlaying);
    button?.setAttribute("aria-label", isPlaying ? "Pause background slideshow" : "Play background slideshow");

    const runs = isPlaying && document.visibilityState === "visible" && this.#isOnScreen;

    if (runs && this.#timer === 0) {
      this.#timer = window.setInterval(() => this.#advance(), Number(this.dataset.intervalMilliseconds));
    } else if (!runs && this.#timer !== 0) {
      window.clearInterval(this.#timer);
      this.#timer = 0;
    }
  }

  #advance() {
    const slides = this.#slides();
    const shown = slides.findIndex((slide) => slide.hasAttribute("data-shown"));
    // there's always one after the shown one, added a step ahead, until they're all added and it wraps round
    const next = slides[(shown + 1) % slides.length];
    slides[shown]?.removeAttribute("data-shown");
    next?.setAttribute("data-shown", "");
    this.#addNext();
  }

  // the first waiting image joins the stack after the ones added, hidden until it's its turn
  #addNext() {
    const waiting = this.#waiting()[0];
    waiting?.replaceWith(waiting.content.cloneNode(true));
  }

  /** @returns {HTMLElement[]} */
  #slides() {
    return [...this.querySelectorAll(':scope > [data-part="slide"]')].filter((slide) => slide instanceof HTMLElement);
  }

  /** @returns {HTMLTemplateElement[]} */
  #waiting() {
    return [...this.querySelectorAll(':scope > template[data-part="later"]')].filter(
      (template) => template instanceof HTMLTemplateElement
    );
  }

  #button() {
    return document.querySelector(`[data-pauses="${this.id}"]`);
  }

  #teardown() {
    this.#listeners?.abort();
    this.#listeners = null;
    this.#observer?.disconnect();
    this.#observer = null;
    window.clearInterval(this.#timer);
    this.#timer = 0;
    delete this.dataset.ready;
    const button = this.#button();
    button?.removeAttribute("data-ready");
    button?.removeAttribute("data-playing");
  }
}

customElements.define("background-slideshow", BackgroundSlideshow);

// turned on or off while the page is open
reducedMotion.addEventListener("change", () => eachSlideshow((slideshow) => slideshow.build()));

// Also built again when its button wasn't there yet as it connected, as when enhanced navigation added the
// slideshow before the row holding the button
Blazor.addEventListener("enhancedload", () =>
  eachSlideshow((slideshow) => {
    if (!slideshow.isBuiltWithButton()) {
      slideshow.build();
    }
  })
);
