import {
  ARTWORK_EMBED,
  attachArtworkEmbedToolbar,
  insertArtworkEmbed,
  registerArtworkEmbed,
} from "./PostArtworkEmbed.razor.js";

// Must stay within what PostDocumentParser reads. Leaving out indent also turns off Tab-to-indent,
// since the parser would flatten a nested list anyway
const FORMATS = [
  "header",
  "bold",
  "italic",
  "underline",
  "link",
  "list",
  "blockquote",
  ARTWORK_EMBED,
];

const TOOLBAR = [
  [{ header: [2, 3, false] }],
  ["bold", "italic", "underline", "link"],
  [{ list: "ordered" }, { list: "bullet" }],
  ["blockquote"],
  [ARTWORK_EMBED],
  ["clean"],
];

// TEMPORARY until the picker is built: an artwork and image from the dev database
const PLACEHOLDER_ARTWORK = {
  artworkId: 2,
  storageKey: "01a0cb0f75db70f48a7e36534091cb7d",
  size: /** @type {const} */ ("medium"),
  layout: /** @type {const} */ ("center"),
};

const HAS_SCHEME = /^[a-z][a-z0-9+.-]*:/i;
const EMAIL_ADDRESS = /^[^\s@/]+@[^\s@/]+\.[^\s@/]+$/;

// The parser keeps only http, https and mailto addresses and paths on this site, so "example.com"
// would vanish from the public page without a word, and so would "//example.com", which names
// another site without a scheme. A path starting with a single / or # is left alone: there is no
// scheme to guess for it
/** @param {string} url */
function withScheme(url) {
  const trimmed = url.trim();

  if (trimmed.startsWith("//")) {
    return `https:${trimmed}`;
  }

  if (
    trimmed === "" ||
    HAS_SCHEME.test(trimmed) ||
    trimmed.startsWith("/") ||
    trimmed.startsWith("#")
  ) {
    return trimmed;
  }

  return EMAIL_ADDRESS.test(trimmed)
    ? `mailto:${trimmed}`
    : `https://${trimmed}`;
}

// Quill is about 200 KB, so only the editor page loads it: the first editor to connect adds the
// script, and an editor on a later page, reached by enhanced navigation, reuses the same load
/** @type {Promise<void> | null} */
let quillLoaded = null;

/**
 * @param {string} src
 * @param {HTMLElement} embedToolbar
 */
function loadQuill(src, embedToolbar) {
  quillLoaded ??= new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = src;
    script.onload = () => {
      registerFormats();
      registerArtworkEmbed(embedToolbar);
      resolve();
    };
    script.onerror = () => {
      // lets the next editor to connect try again
      quillLoaded = null;
      reject(new Error(`Couldn't load the editor from ${src}`));
    };
    document.head.append(script);
  });

  return quillLoaded;
}

function registerFormats() {
  const Link = Quill.import("formats/link");

  // Quill passes every link through sanitize, whether typed into the link box or pasted, and the
  // stored Delta holds what it returns
  class LinkWithScheme extends Link {
    /** @param {string} url */
    static sanitize(url) {
      return super.sanitize(withScheme(url));
    }
  }

  Quill.register(LinkWithScheme, true);
}

customElements.define(
  "post-body-editor",
  class extends HTMLElement {
    /** @type {AbortController | null} */
    #listeners = null;
    #isMounted = false;
    // resolves once Quill is mounted, so a restore clicked while Quill is loading still lands
    /** @type {PromiseWithResolvers<Quill>} */
    #mounted = Promise.withResolvers();

    async connectedCallback() {
      const input = this.querySelector('input[type="hidden"]');
      const embedToolbar = this.querySelector('[data-part="artwork-embed-toolbar"]');
      const quillSource = this.dataset.quillSrc;

      // Moving the element in the page connects it again, and Quill is already in it
      if (
        this.#isMounted ||
        !(input instanceof HTMLInputElement) ||
        !(embedToolbar instanceof HTMLElement) ||
        quillSource === undefined
      ) {
        return;
      }

      this.#isMounted = true;
      await loadQuill(quillSource, embedToolbar);

      // the artist left the page while Quill was loading
      if (!this.isConnected) {
        this.#isMounted = false;
        return;
      }

      // the snow theme puts its toolbar just before this, so both stay inside the element
      const editingArea = document.createElement("div");
      this.append(editingArea);

      const quill = new Quill(editingArea, {
        theme: "snow",
        formats: FORMATS,
        modules: {
          toolbar: {
            container: TOOLBAR,
            handlers: { [ARTWORK_EMBED]: () => insertArtworkEmbed(quill, PLACEHOLDER_ARTWORK) },
          },
        },
      });

      // the post page's text width, so the artist sees the lines and wrapping the page will show
      quill.root.classList.add(...(this.dataset.columnClass ?? "").split(" ").filter(Boolean));

      // Quill draws its own buttons' icons and leaves ours empty
      const artworkButton = quill.getModule("toolbar").container.querySelector(`.ql-${ARTWORK_EMBED}`);
      if (artworkButton !== null) {
        artworkButton.textContent = "Artwork";
        artworkButton.setAttribute("aria-label", "Add an artwork");
      }

      // from the input, not the server's copy, so a save that came back with an error keeps what
      // was typed. "silent" raises no text-change, so loading isn't taken for an edit
      quill.setContents(JSON.parse(input.value), "silent");

      // Written on every change rather than on submit, so the order of submit listeners never
      // matters. The input event tells the page the form changed, as typing in a field would
      quill.on("text-change", () => {
        input.value = JSON.stringify(quill.getContents());
        input.dispatchEvent(new Event("input", { bubbles: true }));
      });

      this.#listeners = new AbortController();
      this.#labelEditingArea(input, quill, this.#listeners.signal);
      attachArtworkEmbedToolbar(quill, embedToolbar, this.#listeners.signal);
      this.#mounted.resolve(quill);
    }

    // Replaces the text as if the artist had made the change, so it can be undone, and the
    // text-change handler writes it to the input like any other edit
    /** @param {string} json */
    async load(json) {
      const quill = await this.#mounted.promise;
      quill.setContents(JSON.parse(json), "user");
    }

    disconnectedCallback() {
      this.#listeners?.abort();
    }

    // The field's label points at the hidden input, which can't take focus or be read out. The
    // label is found now, because enhanced navigation gives it a new "for" that no longer matches
    // the input it kept
    /**
     * @param {HTMLInputElement} input
     * @param {Quill} quill
     * @param {AbortSignal} signal
     */
    #labelEditingArea(input, quill, signal) {
      const label = document.querySelector(
        `label[for="${CSS.escape(input.id)}"]`
      );

      if (label === null) {
        return;
      }

      quill.root.setAttribute("aria-label", label.textContent?.trim() ?? "");
      label.addEventListener("click", () => quill.focus(), { signal });
    }
  }
);
