import {
  WORK_EMBED,
  addWorkEmbed,
  attachWorkEmbedToolbar,
  attachWorkPicker,
  registerWorkEmbed,
} from "./PostWorkEmbed.razor.js";
import {
  IMAGE_EMBED,
  UPLOAD_PLACEHOLDER,
  acceptedImageTypes,
  addPostImageEmbed,
  attachPostImageEmbedToolbar,
  createImagePicker,
  createImageUploads,
  isUploadPlaceholder,
  registerPostImageEmbed,
} from "./PostImageEmbed.razor.js";
import { VIDEO_EMBED, addVideoEmbed, attachVideoEmbedToolbar, registerVideoEmbed } from "./PostVideoEmbed.razor.js";
import { VideoAddressDialog } from "./VideoAddressDialog.razor.js";
import { attachEmbedLines, quillEmbedArrowBindingsOff } from "/js/embed-lines.js";
import { attachEmbedSelection } from "/js/embed-selection.js";
import { attachEmbedDrag } from "/js/embed-drag.js";

// Must stay within what PostDocumentParser reads. Leaving out indent also turns off Tab-to-indent,
// since the parser would flatten a nested list anyway
const FORMATS = [
  "header",
  "bold",
  "italic",
  "underline",
  "link",
  // no toolbar button: Quill makes a list when "1. " or "- " is typed, if the format is allowed
  "list",
  "blockquote",
  WORK_EMBED,
  IMAGE_EMBED,
  UPLOAD_PLACEHOLDER,
  VIDEO_EMBED,
];

// Undo and Redo first, as a phone's browser has no undo of its own for the text
const TOOLBAR = [
  ["undo", "redo"],
  [{ header: [2, 3, false] }],
  ["bold", "italic", "underline", "link"],
  ["blockquote"],
  [WORK_EMBED, IMAGE_EMBED, VIDEO_EMBED],
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
      const workToolbar = this.querySelector('[data-part="work-embed-toolbar"]');
      const imageToolbar = this.querySelector('[data-part="image-embed-toolbar"]');
      const imageFileInput = this.querySelector('input[data-part="image-file"]');
      const videoToolbar = this.querySelector('[data-part="video-embed-toolbar"]');
      const workPicker = this.querySelector('dialog[data-part="work-picker"]');
      const undoIcon = this.querySelector('template[data-part="undo-icon"]');
      const quillSource = this.dataset.quillSrc;

      // Only a move would connect this element again, and Blazor never moves one: it keeps a
      // data-permanent element where it is (checked in blazor.web.js, .NET 10.0.11). This only
      // stops a second Quill if something else ever does; that would also need the listeners
      // disconnectedCallback drops attached again
      if (this.#isMounted) {
        return;
      }

      if (
        !(input instanceof HTMLInputElement) ||
        !(workToolbar instanceof HTMLElement) ||
        !(imageToolbar instanceof HTMLElement) ||
        !(imageFileInput instanceof HTMLInputElement) ||
        !(videoToolbar instanceof HTMLElement) ||
        !(workPicker instanceof HTMLDialogElement) ||
        !(undoIcon instanceof HTMLTemplateElement) ||
        quillSource === undefined
      ) {
        throw new Error(
          "The post body editor is missing its input, an embed toolbar, the image file input, the work picker, the undo icon or Quill's address."
        );
      }

      this.#isMounted = true;
      await loadQuill(quillSource, () => {
        registerWorkEmbed(workToolbar);
        registerPostImageEmbed(imageToolbar);
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
      const picker = attachWorkPicker(workPicker, this.#listeners.signal);
      const imagePicker = createImagePicker(imageFileInput);

      const quill = new Quill(editingArea, {
        theme: "snow",
        formats: FORMATS,
        modules: {
          toolbar: {
            container: TOOLBAR,
            // only ever called once quill below is set
            handlers: {
              undo: () => quill.getModule("history").undo(),
              redo: () => quill.getModule("history").redo(),
              [WORK_EMBED]: () => addWorkEmbed(quill, picker),
              [IMAGE_EMBED]: () => addPostImageEmbed(quill, imagePicker, imageUploads),
              [VIDEO_EMBED]: () => addVideoEmbed(quill, videoToolbar, this.#videoAddressDialog()),
            },
          },
          // Only the artist's own edits are undone. Anything else, such as an upload's placeholder
          // coming and going, is left out, and the steps kept are adjusted around it
          history: { userOnly: true },
          // embed-lines.js and embed-selection.js move the cursor past embeds in their place
          keyboard: { bindings: quillEmbedArrowBindingsOff },
          // Quill's own drop and paste handling, which hands over the files of a type it takes.
          // Its default puts each image in the Delta as a data: address. Also only ever called
          // once imageUploads below is set
          uploader: {
            mimetypes: acceptedImageTypes(imageToolbar),
            handler: (range, files) => imageUploads.insert(range.index, files),
          },
        },
      });
      const imageUploads = createImageUploads(quill, imageToolbar);

      // the post page's text width, so the artist sees the lines and wrapping the page will show
      quill.root.classList.add(...(this.dataset.columnClass ?? "").split(" ").filter(Boolean));

      const toolbar = quill.getModule("toolbar").container;

      // Quill draws its own buttons' icons and leaves ours empty. Image and Video take its own image
      // and video icons; it has none for Undo and Redo, and none for a work, so that one says what
      // it is
      const icons = Quill.import("ui/icons");
      for (const [name, content] of [
        ["undo", undoIcon.innerHTML],
        ["redo", undoIcon.innerHTML],
        [IMAGE_EMBED, icons.image],
        [VIDEO_EMBED, icons.video],
      ]) {
        const button = toolbar.querySelector(`button.ql-${name}`);
        if (button !== null) {
          button.innerHTML = content;
        }
      }

      // the website's own word, which the artist typed, so as text rather than markup
      const workButton = toolbar.querySelector(`button.ql-${WORK_EMBED}`);
      if (workButton !== null) {
        workButton.textContent = this.dataset.workButtonText ?? "";
      }

      // Quill gives its controls no tooltip, and names them to screen readers by their format, such
      // as "bold". The heading picker's label is what takes the clicks
      for (const [selector, label] of [
        ["button.ql-undo", "Undo"],
        ["button.ql-redo", "Redo"],
        [".ql-header .ql-picker-label", "Heading"],
        ["button.ql-bold", "Bold"],
        ["button.ql-italic", "Italic"],
        ["button.ql-underline", "Underline"],
        ["button.ql-link", "Link"],
        ["button.ql-blockquote", "Quote"],
        [`button.ql-${WORK_EMBED}`, this.dataset.workButtonLabel ?? ""],
        [`button.ql-${IMAGE_EMBED}`, "Upload an image"],
        [`button.ql-${VIDEO_EMBED}`, "Add a video"],
        ["button.ql-clean", "Clear formatting"],
      ]) {
        const control = toolbar.querySelector(selector);
        if (control instanceof HTMLElement) {
          control.setAttribute("aria-label", label);
          control.title = label;
        }
      }

      // from the input, not the server's copy, so a save that came back with an error keeps what
      // was typed. "silent" raises no text-change, so loading isn't taken for an edit
      quill.setContents(JSON.parse(input.value), "silent");

      // Written on every change rather than on submit, so the order of submit listeners never
      // matters. The input event tells the page the form changed, as typing in a field would.
      // An upload's placeholder is left out, so a save while it uploads never stores one
      quill.on("text-change", () => {
        const json = JSON.stringify({ ops: quill.getContents().ops.filter((op) => !isUploadPlaceholder(op)) });

        if (json !== input.value) {
          input.value = json;
          input.dispatchEvent(new Event("input", { bubbles: true }));
        }
      });

      this.#labelEditingArea(input, quill, this.#listeners.signal);
      const lines = attachEmbedLines(quill, this.#listeners.signal);
      attachEmbedSelection(quill);
      attachEmbedDrag(quill, this.#listeners.signal);
      attachWorkEmbedToolbar(quill, workToolbar, picker, lines, this.#listeners.signal);
      attachPostImageEmbedToolbar(quill, imageToolbar, imagePicker, imageUploads, lines, this.#listeners.signal);
      attachVideoEmbedToolbar(quill, videoToolbar, () => this.#videoAddressDialog(), lines, this.#listeners.signal);
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

    // Looked up each time, not kept: it's outside this element, so an enhanced navigation can
    // replace it where it can't replace this
    #videoAddressDialog() {
      const dialog = document.getElementById(this.dataset.videoAddressDialog ?? "");

      if (!(dialog instanceof VideoAddressDialog)) {
        throw new Error("The post body editor can't find its video address dialog.");
      }

      return dialog;
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
