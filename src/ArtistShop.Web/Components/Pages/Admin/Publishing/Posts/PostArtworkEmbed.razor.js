// The artwork embed: a block in a post showing one of an artwork's images. The Delta holds only
// what PostDocumentParser reads, and the public page looks up the title and image when it renders.
// Imported by PostBodyEditor.razor.js; nothing here runs until an editor calls it
import { attachEmbedToolbar } from "./PostEmbedToolbar.razor.js";

// the name Quill stores it under, and the parser and set_post_artworks read
export const ARTWORK_EMBED = "artshop-artwork";

const EMBED_CLASS = "artshop-artwork";

/**
 * @typedef {object} ArtworkEmbedValue
 * @property {number} artworkId
 * @property {string} storageKey
 * @property {"small" | "medium"} size
 * @property {string} layout one of EmbedLayoutNames, which the toolbar's buttons carry
 * @property {string} [caption] as typed; the parser trims it, and counts a blank one as none
 */

/** @param {HTMLElement} node */
function readValue(node) {
  const { artworkId, storageKey, size, layout, caption } = node.dataset;

  /** @type {ArtworkEmbedValue} */
  const value = {
    artworkId: Number(artworkId),
    storageKey: storageKey ?? "",
    size: size === "small" ? "small" : "medium",
    // the parser centres a layout it doesn't know
    layout: layout ?? "",
    ...(caption === undefined ? {} : { caption }),
  };

  return value;
}

// Called once, when Quill has loaded. Every editor's toolbar carries the same image addresses, so
// the first one to connect is as good as any
/** @param {HTMLElement} toolbar */
export function registerArtworkEmbed(toolbar) {
  const {
    storageKeyPlaceholder,
    smallImageUrl,
    mediumImageUrl,
    smallWidth,
    mediumWidth,
    layoutClasses,
    captionClass,
  } = toolbar.dataset;

  if (
    storageKeyPlaceholder === undefined ||
    smallImageUrl === undefined ||
    mediumImageUrl === undefined ||
    smallWidth === undefined ||
    mediumWidth === undefined ||
    layoutClasses === undefined ||
    captionClass === undefined
  ) {
    throw new Error("The artwork embed toolbar is missing its image addresses, widths or classes.");
  }

  // the post page's own classes for each layout, so the editor places an embed as the page will
  const classesByLayout = /** @type {Record<string, string>} */ (JSON.parse(layoutClasses));
  // taken after the check above, which TypeScript doesn't carry into the class below
  const figcaptionClass = captionClass;

  /** @param {ArtworkEmbedValue} value */
  const imageUrl = (value) =>
    (value.size === "small" ? smallImageUrl : mediumImageUrl).replace(storageKeyPlaceholder, value.storageKey);

  const BlockEmbed = Quill.import("blots/block/embed");

  // A block embed takes a line of its own. An inline one would end its line in a "\n", which the
  // parser reads as an empty paragraph after every embed
  class ArtworkEmbed extends BlockEmbed {
    static blotName = ARTWORK_EMBED;
    static tagName = "div";
    static className = EMBED_CLASS;

    /** @param {ArtworkEmbedValue} value */
    static create(value) {
      const node = /** @type {HTMLElement} */ (super.create(value));
      // otherwise the cursor can land inside the embed, where there is nothing to type into
      node.contentEditable = "false";
      node.dataset.artworkId = String(value.artworkId);
      node.dataset.storageKey = value.storageKey;
      node.dataset.size = value.size;
      node.dataset.layout = value.layout;
      if (value.caption !== undefined) {
        node.dataset.caption = value.caption;
      }
      node.classList.add(...(classesByLayout[value.layout] ?? "").split(" ").filter(Boolean));

      // the image's width, as the post page's figure is, so a caption wraps under the image
      const figure = document.createElement("figure");
      figure.style.width = `${value.size === "small" ? smallWidth : mediumWidth}px`;
      figure.style.maxWidth = "100%";

      const image = document.createElement("img");
      image.src = imageUrl(value);
      image.alt = "";

      // both are made now, so a failed load only switches which one shows
      const missing = document.createElement("span");
      missing.textContent = "This image was removed.";
      missing.hidden = true;
      image.addEventListener(
        "error",
        () => {
          image.hidden = true;
          missing.hidden = false;
        },
        { once: true }
      );

      figure.append(image, missing);

      if (value.caption !== undefined && value.caption.trim() !== "") {
        const caption = document.createElement("figcaption");
        caption.className = figcaptionClass;
        caption.textContent = value.caption;
        figure.append(caption);
      }

      node.append(figure);
      return node;
    }

    /** @param {HTMLElement} node */
    static value(node) {
      return readValue(node);
    }
  }

  Quill.register(ArtworkEmbed);
}

/**
 * @param {Quill} quill
 * @param {ArtworkEmbedValue} value
 * @param {number} index
 */
function insertArtworkEmbed(quill, value, index) {
  quill.insertEmbed(index, ARTWORK_EMBED, value, "user");
  quill.setSelection(index + 1, 0, "user");
}

// marks the picker's messages, since any page in any frame can post to this window
const ARTWORK_PICKER_MESSAGE = "artshop-artwork-picker";

/**
 * What the artist picked. Size and layout come only when adding: changing an embed's image keeps
 * the size and layout it has
 * @typedef {object} ArtworkPickerChoice
 * @property {number} artworkId
 * @property {string} storageKey
 * @property {"small" | "medium"} [size]
 * @property {string} [layout]
 */

/** @typedef {{ action: "close" } | { action: "choose", choice: ArtworkPickerChoice }} ArtworkPickerMessage */

/**
 * @typedef {object} ArtworkPicker
 * @property {string} addUrl the picker's first page for adding an embed
 * @property {(artworkId: number) => string} changeImageUrl its page of an artwork's images, for
 *   changing an embed's image
 * @property {(url: string, onChosen: (choice: ArtworkPickerChoice) => void) => void} open
 */

// Called by the picker's pages inside the frame. A picker page opened on its own, outside a frame,
// has no editor to tell
/** @param {ArtworkPickerMessage} message */
export function sendToArtworkPickerOwner(message) {
  if (window.parent !== window) {
    window.parent.postMessage({ type: ARTWORK_PICKER_MESSAGE, ...message }, location.origin);
  }
}

// Wires up the dialog holding the picker's frame. Its addresses come from the server, with a
// placeholder where an artwork's id goes
/**
 * @param {HTMLDialogElement} dialog
 * @param {AbortSignal} signal
 * @returns {ArtworkPicker}
 */
export function attachArtworkPicker(dialog, signal) {
  const frame = dialog.querySelector("iframe");
  const { addSrc, changeImageSrc, artworkIdPlaceholder } = frame?.dataset ?? {};

  if (frame === null || addSrc === undefined || changeImageSrc === undefined || artworkIdPlaceholder === undefined) {
    throw new Error("The artwork picker is missing its frame or the frame's addresses.");
  }

  // what the current opening does with the choice
  /** @type {((choice: ArtworkPickerChoice) => void) | null} */
  let onChosen = null;

  window.addEventListener(
    "message",
    (event) => {
      // only this editor's own frame, showing a page of this site
      if (event.origin !== location.origin || event.source !== frame.contentWindow) {
        return;
      }

      /** @type {(ArtworkPickerMessage & { type: unknown }) | null} */
      const message = event.data;

      if (message?.type !== ARTWORK_PICKER_MESSAGE) {
        return;
      }

      const chosen = onChosen;
      // closed first, so the focus it hands back doesn't land after the cursor is placed
      dialog.close();

      if (message.action === "choose") {
        chosen?.(message.choice);
      }
    },
    { signal }
  );

  // the next opening starts again from its first page, and the page left behind stops running
  dialog.addEventListener(
    "close",
    () => {
      onChosen = null;
      frame.setAttribute("src", "about:blank");
    },
    { signal }
  );

  return {
    addUrl: addSrc,
    changeImageUrl: (artworkId) => changeImageSrc.replace(artworkIdPlaceholder, String(artworkId)),
    open(url, handleChoice) {
      onChosen = handleChoice;
      frame.src = url;
      dialog.showModal();
    },
  };
}

// The toolbar's Artwork button. The cursor's place is kept now, since the dialog takes the focus
/**
 * @param {Quill} quill
 * @param {ArtworkPicker} picker
 */
export function addArtworkEmbed(quill, picker) {
  const { index } = quill.getSelection(true);

  picker.open(picker.addUrl, ({ artworkId, storageKey, size, layout }) => {
    // the adding steps always send both
    if (size !== undefined && layout !== undefined) {
      insertArtworkEmbed(quill, { artworkId, storageKey, size, layout }, index);
    }
  });
}

// The toolbar's caption field and buttons for an artwork embed. Opening the picker for Change image closes the
// toolbar, and the embed keeps its size and layout
/**
 * @param {Quill} quill
 * @param {HTMLElement} toolbar
 * @param {ArtworkPicker} picker
 * @param {AbortSignal} signal
 */
export function attachArtworkEmbedToolbar(quill, toolbar, picker, signal) {
  const alignmentButtons = [...toolbar.querySelectorAll("button[data-layout]")].filter(
    (button) => button instanceof HTMLButtonElement
  );
  const wrapButton = toolbar.querySelector('button[data-action="wrap"]');
  const captionField = toolbar.querySelector('input[data-part="caption"]');

  if (!(captionField instanceof HTMLInputElement)) {
    throw new Error("The artwork embed toolbar is missing its caption field.");
  }

  // the alignment button a layout belongs to, whether or not its text wraps
  /** @param {string} layout */
  function alignmentOf(layout) {
    return alignmentButtons.find(
      (button) => button.dataset.layout === layout || button.dataset.wrappedLayout === layout
    );
  }

  /** @param {string} layout */
  function isWrapped(layout) {
    return alignmentOf(layout)?.dataset.wrappedLayout === layout;
  }

  attachEmbedToolbar(
    quill,
    toolbar,
    {
      blotName: ARTWORK_EMBED,
      className: EMBED_CLASS,
      readValue,

      show(value) {
        // only when it differs: setting a field's value moves the cursor to its end, which would
        // happen on every keystroke as the embed is replaced under it
        if (captionField.value !== (value.caption ?? "")) {
          captionField.value = value.caption ?? "";
        }

        toolbar.querySelectorAll("button[data-size]").forEach((button) => {
          button.setAttribute(
            "aria-pressed",
            String(button instanceof HTMLElement && button.dataset.size === value.size)
          );
        });

        const alignment = alignmentOf(value.layout);
        alignmentButtons.forEach((button) => button.setAttribute("aria-pressed", String(button === alignment)));

        if (wrapButton instanceof HTMLButtonElement) {
          wrapButton.setAttribute("aria-pressed", String(isWrapped(value.layout)));
          // centred has nothing beside it to wrap
          wrapButton.disabled = alignment?.dataset.wrappedLayout === undefined;
        }
      },

      onButton(button, embed) {
        const { size, layout, wrappedLayout, action } = button.dataset;
        const current = embed.value;

        if (action === "change-image") {
          picker.open(picker.changeImageUrl(current.artworkId), ({ artworkId, storageKey }) => {
            embed.replace({ artworkId, storageKey });
          });
        } else if (action === "wrap") {
          const alignment = alignmentOf(current.layout)?.dataset;
          const toggled = isWrapped(current.layout) ? alignment?.layout : alignment?.wrappedLayout;

          if (toggled !== undefined) {
            embed.update({ layout: toggled });
          }
        } else if (size === "small" || size === "medium") {
          embed.update({ size });
        } else if (layout !== undefined) {
          // a new alignment keeps whether the text wraps, where it can
          embed.update({ layout: isWrapped(current.layout) ? wrappedLayout ?? layout : layout });
        }
      },

      // applied as it's typed, so no way of closing the toolbar can lose it. An emptied field
      // leaves no caption key behind
      onInput(field, embed) {
        if (field === captionField) {
          embed.update({ caption: field.value === "" ? undefined : field.value });
        }
      },
    },
    signal
  );
}
