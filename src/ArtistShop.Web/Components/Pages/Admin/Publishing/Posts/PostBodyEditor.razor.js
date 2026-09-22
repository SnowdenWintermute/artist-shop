// Must stay within what PostDocumentParser reads. Leaving out indent also turns off Tab-to-indent,
// since the parser would flatten a nested list anyway
const FORMATS = ["header", "bold", "italic", "underline", "link", "list", "blockquote"];

const TOOLBAR = [
  [{ header: [2, 3, false] }],
  ["bold", "italic", "underline", "link"],
  [{ list: "ordered" }, { list: "bullet" }],
  ["blockquote"],
  ["clean"],
];

const HAS_SCHEME = /^[a-z][a-z0-9+.-]*:/i;
const EMAIL_ADDRESS = /^[^\s@/]+@[^\s@/]+\.[^\s@/]+$/;

// The parser drops any link that isn't an absolute http, https or mailto address, so "example.com"
// would vanish from the public page without a word. An address starting with / or # is left
// alone: there is no scheme to guess for it
/** @param {string} url */
function withScheme(url) {
  const trimmed = url.trim();

  if (trimmed === "" || HAS_SCHEME.test(trimmed) || trimmed.startsWith("/") || trimmed.startsWith("#")) {
    return trimmed;
  }

  return EMAIL_ADDRESS.test(trimmed) ? `mailto:${trimmed}` : `https://${trimmed}`;
}

// Quill is about 200 KB, so only the editor page loads it: the first editor to connect adds the
// script, and an editor on a later page, reached by enhanced navigation, reuses the same load
/** @type {Promise<void> | null} */
let quillLoaded = null;

/** @param {string} src */
function loadQuill(src) {
  quillLoaded ??= new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = src;
    script.onload = () => {
      registerFormats();
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

    async connectedCallback() {
      const input = this.querySelector('input[type="hidden"]');
      const quillSource = this.dataset.quillSrc;

      // Moving the element in the page connects it again, and Quill is already in it
      if (this.#isMounted || !(input instanceof HTMLInputElement) || quillSource === undefined) {
        return;
      }

      this.#isMounted = true;
      await loadQuill(quillSource);

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
        modules: { toolbar: TOOLBAR },
      });

      // from the input, not the server's copy, so a save that came back with an error keeps what
      // was typed. "silent" raises no text-change, so loading isn't taken for an edit
      quill.setContents(JSON.parse(input.value), "silent");

      // Written on every change rather than on submit, so the order of submit listeners never
      // matters. The input event tells the page the form changed, as typing in a field would
      quill.on("text-change", () => {
        input.value = JSON.stringify(quill.getContents());
        input.dispatchEvent(new Event("input", { bubbles: true }));
      });

      this.#labelEditingArea(input, quill);
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
     */
    #labelEditingArea(input, quill) {
      const label = document.querySelector(`label[for="${CSS.escape(input.id)}"]`);

      if (label === null) {
        return;
      }

      quill.root.setAttribute("aria-label", label.textContent?.trim() ?? "");
      this.#listeners = new AbortController();
      label.addEventListener("click", () => quill.focus(), { signal: this.#listeners.signal });
    }
  }
);
