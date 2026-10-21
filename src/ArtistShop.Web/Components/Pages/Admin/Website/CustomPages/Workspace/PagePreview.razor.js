// <page-preview>: a frame showing the public page, drawn data-preview-width wide (the workspace's Desktop and Phone
// buttons, in [data-part=preview-widths]) and shrunk to fit, as tall as fits at that zoom so a row that fills the screen fills the preview. It reloads after a
// save: on an enhanced load of the editor, and when an island's <preview-reload> changes its stamp. A reload is an
// enhanced navigation inside the frame, which patches the page in place rather than blanking it, and replaces its
// history entry so Back still leaves the editor. It keeps the frame's scroll, even when what's being edited changes
// (Mike: scrolling to it was jarring); a [data-scrolls-preview] button scrolls to it when asked. What's being edited (data-row-id, data-component-id) is outlined. Forms in the frame do nothing, and a link opens the
// editor of the page it leads to, found by that page's own admin Edit page link (EditPageLink), or does nothing.
// An enhanced load patches this element's attributes and the frame's back to the server's, so they're set again: the
// width chosen on enhancedload, and the frame's size whenever its style changes, whatever the order things run in
import { pressWidthButton, rememberedWidthButton, rememberPreviewWidth } from "/js/preview-width.js";

// the link clicked, null when none; the frame's elements aren't instances of this window's Element, so it's told by name
/**
 * @param {Event} event
 * @returns {HTMLAnchorElement | null}
 */
function linkOf(event) {
  const link = event.composedPath().find((node) => "nodeName" in node && node.nodeName === "A");
  return link ? /** @type {HTMLAnchorElement} */ (link) : null;
}

// how long a reload holds the frame's scroll while its images load in, unless the person scrolls first
const scrollHoldMilliseconds = 3000;

// the page above what's scrolled to when it's taller than the frame, in the page's own pixels
const scrollMarginPixels = 24;

class PagePreview extends HTMLElement {
  /** @type {AbortController | null} */
  #listeners = null;
  /** @type {ResizeObserver | null} */
  #viewportObserver = null;
  /** @type {ResizeObserver | null} */
  #pageObserver = null;
  /** @type {MutationObserver | null} */
  #patchObserver = null;
  #fitFrame = 0;
  // the frame whose loads this listens to, as a patch could put a new one in
  /** @type {HTMLIFrameElement | null} */
  #listenedFrame = null;
  /** @type {number | null} */
  #keptScroll = null;
  // After a reload, the scroll kept while the page settles. Patching in moved components can bring new images,
  // which take no height until they load, so the page is briefly shorter and the browser clamps the scroll
  /** @type {number | null} */
  #heldScroll = null;
  #holdTimer = 0;
  // a frame still on its first load needs no reload, as when an enhanced load brought this element
  #hasLoaded = false;
  /** @type {HTMLElement | null} */
  #outlineBox = null;

  connectedCallback() {
    this.#listeners = new AbortController();
    const signal = this.#listeners.signal;

    document.addEventListener("click", (event) => this.#choose(event), { signal });
    document.addEventListener("click", (event) => this.#toggle(event), { signal });
    document.addEventListener("click", (event) => this.#scrollOnClick(event), { signal });
    document.addEventListener("page-preview-stale", () => this.reload(), { signal });

    this.#viewportObserver = new ResizeObserver(() => this.#fitSoon());
    const viewport = this.querySelector("[data-part=viewport]");

    if (viewport) {
      this.#viewportObserver.observe(viewport);
    }

    // a patch that takes the frame's size away, or puts in a new frame
    this.#patchObserver = new MutationObserver(() => {
      this.#listenToFrame();
      this.#fitSoon();
    });
    this.#patchObserver.observe(this, { subtree: true, childList: true, attributes: true, attributeFilter: ["style"] });

    if (this.#widths) {
      this.#patchObserver.observe(this.#widths, { subtree: true, attributes: true, attributeFilter: ["aria-pressed"] });
    }
    this.#listenToFrame();

    this.restore();

    // loaded before this script ran
    if (this.frame?.contentDocument?.readyState === "complete") {
      this.#prepare();
    }
  }

  disconnectedCallback() {
    this.#listeners?.abort();
    this.#viewportObserver?.disconnect();
    this.#pageObserver?.disconnect();
    this.#patchObserver?.disconnect();
    cancelAnimationFrame(this.#fitFrame);
  }

  #listenToFrame() {
    const frame = this.frame;

    if (!frame || frame === this.#listenedFrame || !this.#listeners) {
      return;
    }

    this.#listenedFrame = frame;
    frame.addEventListener("load", () => this.#prepare(), { signal: this.#listeners.signal });
  }

  // once per frame, however many changes ask for it
  #fitSoon() {
    cancelAnimationFrame(this.#fitFrame);
    this.#fitFrame = requestAnimationFrame(() => this.#fit());
  }

  /** @returns {HTMLIFrameElement | null} */
  get frame() {
    const frame = this.querySelector("iframe");
    return frame instanceof HTMLIFrameElement ? frame : null;
  }

  // the width buttons, beside the workspace's Up link
  get #widths() {
    return this.closest("[data-editor-workspace]")?.querySelector("[data-part=preview-widths]") ?? null;
  }

  get #width() {
    return Number(this.#widths?.querySelector("[data-preview-width][aria-pressed=true]")?.getAttribute("data-preview-width"));
  }

  // after an enhanced load: the width chosen, and the frame's size, which the patch took away
  restore() {
    const widths = this.#widths;
    const button = widths ? rememberedWidthButton(widths) : null;

    if (widths && button) {
      pressWidthButton(widths, button);
    }

    this.#fit();
  }

  reload() {
    const frame = this.frame;

    if (!frame?.contentWindow || !this.#hasLoaded) {
      return;
    }

    // a new address, saved on the page editor, is already loading
    if (frame.contentWindow.location.pathname !== new URL(frame.src).pathname) {
      return;
    }

    const page = frame.contentWindow;
    this.#keptScroll = page.scrollY;
    // the frame's own Blazor, which a page still starting may not have yet
    const pageBlazor = /** @type {{ Blazor?: typeof Blazor }} */ (/** @type {unknown} */ (page)).Blazor;

    // Not the options object: Blazor tells one by instanceof Object, which an object made in this window fails in
    // the frame's, so it would take it for forceLoad and load the page whole, starting at the top
    if (pageBlazor) {
      pageBlazor.navigateTo(page.location.href, false, true);
    } else {
      page.location.reload();
    }
  }

  /** @param {MouseEvent} event */
  #choose(event) {
    const widths = this.#widths;
    const chosen = event.target instanceof Element ? event.target.closest("[data-preview-width]") : null;
    const width = chosen?.getAttribute("data-preview-width");

    if (!widths || !chosen || !widths.contains(chosen) || !width) {
      return;
    }

    rememberPreviewWidth(width);
    pressWidthButton(widths, chosen);
    this.#fit();
  }

  // on phones, the workspace's Preview button opens it over the editor
  /** @param {MouseEvent} event */
  #toggle(event) {
    if (!(event.target instanceof Element)) {
      return;
    }

    if (event.target.closest("[data-opens-preview]")) {
      this.toggleAttribute("data-open", true);
    } else if (event.target.closest("[data-closes-preview]")) {
      this.removeAttribute("data-open");
    }
  }

  /** @param {MouseEvent} event */
  #scrollOnClick(event) {
    if (!(event.target instanceof Element) || !event.target.closest("[data-scrolls-preview]")) {
      return;
    }

    // on phones it's hidden until opened, and only then has a size to scroll in
    if (getComputedStyle(this).display === "none") {
      this.toggleAttribute("data-open", true);
      this.#fit();
    }

    this.#scrollToEdited();
  }

  // what's being edited, centred when it fits, else its top a little below the frame's
  #scrollToEdited() {
    const page = this.frame?.contentWindow;
    const document = this.frame?.contentDocument;

    if (!page || !document) {
      return;
    }

    const targets = this.#targets(document);

    if (targets.length === 0) {
      return;
    }

    const bounds = this.#bounds(targets, page);
    const room = page.innerHeight - (bounds.bottom - bounds.top);
    this.#heldScroll = null;
    page.scrollTo({ top: Math.max(0, bounds.top - Math.max(scrollMarginPixels, room / 2)), behavior: "smooth" });
  }

  // drawn at the chosen width and shrunk to the viewport's, never enlarged; a narrower page is centred
  #fit() {
    const frame = this.frame;
    const viewport = frame?.parentElement;
    const width = this.#width;

    if (!frame || !viewport || !(width > 0) || viewport.clientWidth === 0) {
      return;
    }

    // rounded, so each value reads back as written and the comparison below settles
    const zoom = Math.round(Math.min(1, viewport.clientWidth / width) * 10000) / 10000;
    const size = {
      width: `${width}px`,
      height: `${Math.round((viewport.clientHeight / zoom) * 100) / 100}px`,
      left: `${Math.round(Math.max(0, (viewport.clientWidth - width) / 2) * 100) / 100}px`,
      transform: `scale(${zoom})`,
    };

    // only what differs, so writing it doesn't wake the patch observer for nothing
    for (const [property, value] of Object.entries(size)) {
      if (frame.style.getPropertyValue(property) !== value) {
        frame.style.setProperty(property, value);
      }
    }
  }

  // each time the frame loads a document: links off, and what an enhanced load inside it patches away shown again
  #prepare() {
    const page = this.frame?.contentWindow;

    if (!page) {
      return;
    }

    // before Blazor's own listeners on the document, so an enhanced navigation never starts
    for (const type of ["click", "submit"]) {
      page.addEventListener(
        type,
        (event) => {
          const link = type === "click" ? linkOf(event) : null;

          if (type === "submit" || link) {
            event.preventDefault();
            event.stopImmediatePropagation();
          }

          if (link) {
            this.#openEditorOf(link, page);
          }
        },
        { capture: true }
      );
    }

    // the person's own scrolling ends any hold
    for (const type of ["wheel", "touchstart", "keydown", "pointerdown"]) {
      page.addEventListener(type, () => (this.#heldScroll = null), { passive: true });
    }

    const pageBlazor = /** @type {{ Blazor?: typeof Blazor }} */ (/** @type {unknown} */ (page)).Blazor;
    pageBlazor?.addEventListener("enhancedload", () => this.#show());
    this.#show();
  }

  // Another page of this site, fetched for the admin Edit page link it draws, which the editor follows. Nothing for a
  // link elsewhere, one to the page shown (a card grid's page links), or a page with nothing to edit
  /**
   * @param {HTMLAnchorElement} link
   * @param {Window} page
   */
  async #openEditorOf(link, page) {
    const target = new URL(link.href, page.location.href);

    if (target.origin !== window.location.origin || target.pathname === page.location.pathname) {
      return;
    }

    const response = await fetch(target);

    if (!response.ok || !response.headers.get("content-type")?.startsWith("text/html")) {
      return;
    }

    const linked = new DOMParser().parseFromString(await response.text(), "text/html");
    const editHref = linked.querySelector("a[data-edit-page]")?.getAttribute("href");

    if (editHref) {
      Blazor.navigateTo(editHref);
    }
  }

  // the admin's footer links hidden, the scroll, and the outline
  #show() {
    const page = this.frame?.contentWindow;
    const document = this.frame?.contentDocument;

    if (!page || !document?.body) {
      return;
    }

    for (const links of document.querySelectorAll("[data-admin-footer-links]")) {
      links.toggleAttribute("hidden", true);
    }

    const outline = this.#outline(document);
    const targets = this.#targets(document);

    // the browser's scroll anchoring follows a node, which may be one that moved
    document.documentElement.style.overflowAnchor = "none";

    if (this.#keptScroll !== null) {
      page.scrollTo(0, this.#keptScroll);
      this.#heldScroll = this.#keptScroll;
      clearTimeout(this.#holdTimer);
      this.#holdTimer = window.setTimeout(() => (this.#heldScroll = null), scrollHoldMilliseconds);
    }

    this.#hasLoaded = true;
    this.#keptScroll = null;

    // images loading, a font arriving or a carousel starting moves what's outlined
    this.#pageObserver?.disconnect();
    this.#pageObserver = new ResizeObserver(() => {
      if (this.#heldScroll !== null) {
        page.scrollTo(0, this.#heldScroll);
      }

      this.#place(outline, targets, page);
    });
    this.#pageObserver.observe(document.body);
    this.#place(outline, targets, page);
  }

  // the row, or the component in it; a component is a box with display: contents, so its children
  /** @param {Document} document */
  #targets(document) {
    const row = this.dataset.rowId ? document.querySelector(`[data-row-id="${this.dataset.rowId}"]`) : null;

    if (!row) {
      return [];
    }

    if (!this.dataset.componentId) {
      return [row];
    }

    const component = row.querySelector(`[data-component-id="${this.dataset.componentId}"]`);
    return component ? [...component.children] : [];
  }

  // one at a time, though an enhanced load inside the frame usually takes the last away itself
  /** @param {Document} document */
  #outline(document) {
    this.#outlineBox?.remove();
    const outline = document.createElement("div");
    this.#outlineBox = outline;
    const accent = getComputedStyle(this).getPropertyValue("--theme-accent");
    outline.style.cssText = `position: absolute; pointer-events: none; z-index: 2147483647; box-sizing: border-box;
      border: 3px solid ${accent}; box-shadow: 0 0 0 2px white; display: none;`;
    document.body.append(outline);
    return outline;
  }

  /**
   * @param {HTMLElement} outline
   * @param {Element[]} targets
   * @param {Window} page
   */
  #place(outline, targets, page) {
    if (targets.length === 0) {
      return;
    }

    const bounds = this.#bounds(targets, page);
    outline.style.display = "block";
    outline.style.top = `${bounds.top}px`;
    outline.style.left = `${bounds.left}px`;
    outline.style.width = `${bounds.right - bounds.left}px`;
    outline.style.height = `${bounds.bottom - bounds.top}px`;
  }

  // where the targets together sit on the page, not the screen
  /**
   * @param {Element[]} targets
   * @param {Window} page
   */
  #bounds(targets, page) {
    const rects = targets.map((target) => target.getBoundingClientRect());

    return {
      top: Math.min(...rects.map((rect) => rect.top)) + page.scrollY,
      left: Math.min(...rects.map((rect) => rect.left)) + page.scrollX,
      right: Math.max(...rects.map((rect) => rect.right)) + page.scrollX,
      bottom: Math.max(...rects.map((rect) => rect.bottom)) + page.scrollY,
    };
  }
}

customElements.define("page-preview", PagePreview);

// <preview-reload data-stamp="…">: an island saving something the preview shows changes the stamp
customElements.define(
  "preview-reload",
  class extends HTMLElement {
    static observedAttributes = ["data-stamp"];

    /**
     * @param {string} _name
     * @param {string | null} oldValue
     */
    attributeChangedCallback(_name, oldValue) {
      if (oldValue !== null) {
        document.dispatchEvent(new Event("page-preview-stale"));
      }
    }
  }
);

Blazor.addEventListener("enhancedload", () =>
  document.querySelectorAll("page-preview").forEach((element) => {
    if (element instanceof PagePreview) {
      element.restore();
      element.reload();
    }
  })
);
