import {
  ARTWORK_EMBED,
  addArtworkEmbed,
  attachArtworkEmbedToolbar,
  attachArtworkPicker,
  registerArtworkEmbed,
} from "./PostArtworkEmbed.razor.js";
import { VIDEO_EMBED, addVideoEmbed, attachVideoEmbedToolbar, registerVideoEmbed } from "./PostVideoEmbed.razor.js";
import { VideoAddressDialog } from "./VideoAddressDialog.razor.js";

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
  VIDEO_EMBED,
];

const TOOLBAR = [
  [{ header: [2, 3, false] }],
  ["bold", "italic", "underline", "link"],
  [{ list: "ordered" }, { list: "bullet" }],
  ["blockquote"],
  [ARTWORK_EMBED, VIDEO_EMBED],
  ["clean"],
];

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
 * @param {() => void} registerEmbeds
 */
function loadQuill(src, registerEmbeds) {
  quillLoaded ??= new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = src;
    script.onload = () => {
      registerFormats();
      registerEmbeds();
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
      const artworkToolbar = this.querySelector('[data-part="artwork-embed-toolbar"]');
      const videoToolbar = this.querySelector('[data-part="video-embed-toolbar"]');
      const artworkPicker = this.querySelector('dialog[data-part="artwork-picker"]');
      const quillSource = this.dataset.quillSrc;

      // Moving the element in the page connects it again, and Quill is already in it
      if (this.#isMounted) {
        return;
      }

      if (
        !(input instanceof HTMLInputElement) ||
        !(artworkToolbar instanceof HTMLElement) ||
        !(videoToolbar instanceof HTMLElement) ||
        !(artworkPicker instanceof HTMLDialogElement) ||
        quillSource === undefined
      ) {
        throw new Error("The post body editor is missing its input, an embed toolbar, the artwork picker or Quill's address.");
      }

      this.#isMounted = true;
      await loadQuill(quillSource, () => {
        registerArtworkEmbed(artworkToolbar);
        registerVideoEmbed(videoToolbar);
      });

      // the artist left the page while Quill was loading
      if (!this.isConnected) {
        this.#isMounted = false;
        return;
      }

      // the snow theme puts its toolbar just before this, so both stay inside the element
      const editingArea = document.createElement("div");
      this.append(editingArea);

      this.#listeners = new AbortController();
      const picker = attachArtworkPicker(artworkPicker, this.#listeners.signal);

      const quill = new Quill(editingArea, {
        theme: "snow",
        formats: FORMATS,
        modules: {
          toolbar: {
            container: TOOLBAR,
            // only ever called once quill below is set
            handlers: {
              [ARTWORK_EMBED]: () => addArtworkEmbed(quill, picker),
              [VIDEO_EMBED]: () => addVideoEmbed(quill, videoToolbar, this.#videoAddressDialog()),
            },
          },
        },
      });

      // the post page's text width, so the artist sees the lines and wrapping the page will show
      quill.root.classList.add(...(this.dataset.columnClass ?? "").split(" ").filter(Boolean));

      // Quill draws its own buttons' icons and leaves ours empty
      for (const [embedName, text, label] of [
        [ARTWORK_EMBED, "Artwork", "Add an artwork"],
        [VIDEO_EMBED, "Video", "Add a video"],
      ]) {
        const button = quill.getModule("toolbar").container.querySelector(`.ql-${embedName}`);
        if (button !== null) {
          button.textContent = text;
          button.setAttribute("aria-label", label);
        }
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

      this.#labelEditingArea(input, quill, this.#listeners.signal);
      attachArtworkEmbedToolbar(quill, artworkToolbar, picker, this.#listeners.signal);
      attachVideoEmbedToolbar(quill, videoToolbar, () => this.#videoAddressDialog(), this.#listeners.signal);
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
    // Looked up each time, not kept: it's outside this element, so an enhanced navigation can
    // replace it where it can't replace this
    #videoAddressDialog() {
      const dialog = document.getElementById(this.dataset.videoAddressDialog ?? "");

      if (!(dialog instanceof VideoAddressDialog)) {
        throw new Error("The post body editor can't find its video address dialog.");
      }

      return dialog;
    }

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
